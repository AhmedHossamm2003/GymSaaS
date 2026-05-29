// ================================================================
// Services/Reception/ReceptionService.cs
// ================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;

namespace GymSaaS.Services.Reception
{
    public class ReceptionService : IReceptionService
    {
        private readonly GymDbContext _db;

        // Package type codes that represent "non-class" gym access
        private static readonly HashSet<string> OpenGymTypeCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "OPEN_GYM", "SESSION", "SUBSCRIPTION", "BUNDLE"
        };

        public ReceptionService(GymDbContext db)
        {
            _db = db;
        }

        // ── GetDashboardAsync ─────────────────────────────────────
        public async Task<ReceptionDashboardDto?> GetDashboardAsync(Guid branchId, Guid tenantId)
        {
            var branch = await _db.Branches
                .Where(b => b.BranchId == branchId && b.TenantId == tenantId && b.IsActive)
                .FirstOrDefaultAsync();

            if (branch == null) return null;

            var now = DateTime.UtcNow;
            var todayUtc = now.Date;

            // Valid attendance status codes
            var validStatusCodes = new[] { "SUCCESS", "OVERRIDE_APPROVED", "MANUAL" };
            var validStatusIds = await _db.AttendanceStatuses
                .Where(s => validStatusCodes.Contains(s.StatusCode))
                .Select(s => s.AttendanceStatusId)
                .ToListAsync();

            // Currently inside: PresenceUntilUtc > now
            var currentlyPresent = await _db.AttendanceRecords
                .Where(a => a.BranchId == branchId
                         && a.TenantId == tenantId
                         && validStatusIds.Contains(a.AttendanceStatusId)
                         && a.PresenceUntilUtc > now)
                .Select(a => a.MemberId)
                .Distinct()
                .CountAsync();

            // Today's total entries
            var todayEntries = await _db.AttendanceRecords
                .Where(a => a.BranchId == branchId
                         && a.TenantId == tenantId
                         && validStatusIds.Contains(a.AttendanceStatusId)
                         && a.CheckInAtUtc >= todayUtc
                         && a.CheckInAtUtc < todayUtc.AddDays(1))
                .CountAsync();

            // Today's classes (use local time so day-of-week matches the schedule)
            var localNow  = DateTime.Now;
            var todayDow  = (int)localNow.DayOfWeek;
            var nowTime   = TimeOnly.FromDateTime(localNow);

            var rawClasses = await _db.GymClasses
                .Where(g => g.BranchId == branchId && g.TenantId == tenantId
                         && !g.IsDeleted && g.IsActive && g.DayOfWeek == todayDow)
                .OrderBy(g => g.StartTime)
                .Select(g => new { g.GymClassId, g.ClassName, g.StartTime, g.EndTime,
                                   g.CoachId, g.Capacity, g.PhotoUrl })
                .ToListAsync();

            var coachIds = rawClasses
                .Where(c => c.CoachId.HasValue).Select(c => c.CoachId!.Value).Distinct().ToList();
            var coachMap = coachIds.Count > 0
                ? await _db.Coaches
                    .Where(c => coachIds.Contains(c.CoachId))
                    .Select(c => new { c.CoachId, Name = c.FirstName + " " + c.LastName })
                    .ToDictionaryAsync(c => c.CoachId, c => c.Name.Trim())
                : new Dictionary<Guid, string>();

            var todayClasses = rawClasses.Select(g => new TodayClassItem
            {
                GymClassId  = g.GymClassId,
                ClassName   = g.ClassName,
                TimeDisplay = $"{g.StartTime:HH:mm} – {g.EndTime:HH:mm}",
                CoachName   = g.CoachId.HasValue && coachMap.TryGetValue(g.CoachId.Value, out var cn) ? cn : null,
                Capacity    = g.Capacity,
                PhotoUrl    = g.PhotoUrl,
                IsLive      = nowTime >= g.StartTime && nowTime <= g.EndTime,
                IsUpcoming  = nowTime < g.StartTime,
            }).ToList();

            return new ReceptionDashboardDto
            {
                BranchId              = branch.BranchId,
                BranchName            = branch.BranchName,
                MaxCapacity           = branch.Capacity ?? 0,
                CurrentlyPresentCount = currentlyPresent,
                TodayEntryCount       = todayEntries,
                TodayClassesCount     = todayClasses.Count,
                TodayClasses          = todayClasses,
            };
        }

        // ── ProcessScanAsync ──────────────────────────────────────
        public async Task<ScanResultDto> ProcessScanAsync(
            string membershipNumber, Guid branchId, Guid tenantId)
        {
            // 1. Find member by membership number or phone number.
            //    Normalise the input to digits-only for a robust phone match
            //    (handles inputs like "+20 123 456 789" or "050-123-4567").
            var digitsOnly = new string(membershipNumber.Where(char.IsDigit).ToArray());
            bool couldBePhone = digitsOnly.Length >= 7;

            // First pass: exact match on membership number OR exact match on raw phone
            var member = await _db.Members.Include(m => m.MemberStatus)
                .Where(m => m.TenantId == tenantId && !m.IsDeleted &&
                            (m.MembershipNumber == membershipNumber ||
                             (couldBePhone && m.PhoneNumber == membershipNumber)))
                .FirstOrDefaultAsync();

            // Second pass: if not found, try normalised phone comparison (strips +, -, spaces)
            if (member == null && couldBePhone)
            {
                member = await _db.Members.Include(m => m.MemberStatus)
                    .Where(m => m.TenantId == tenantId && !m.IsDeleted && m.PhoneNumber != null &&
                                m.PhoneNumber.Replace("+", "").Replace("-", "")
                                             .Replace(" ", "").Replace("(", "").Replace(")", "") == digitsOnly)
                    .FirstOrDefaultAsync();
            }

            if (member == null)
                return Fail("MEMBER_NOT_FOUND", "Member not found.");

            // 2. Check member status — only ACTIVE allowed in
            if (member.MemberStatus.StatusCode != "ACTIVE")
                return Fail("MEMBER_INACTIVE",
                    $"Member account is {member.MemberStatus.StatusName}. Entry not allowed.");

            // 3. Get active packages valid today
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var activePackages = await _db.MemberPackages
      .Include(p => p.PackageType)
      .Where(p => p.MemberId == member.MemberId
               && p.TenantId == tenantId
               && p.Status == "ACTIVE"
               && p.ValidFromDate <= today
               && (p.ValidToDate == null || p.ValidToDate >= today))
      .ToListAsync();

            if (!activePackages.Any())
                return Fail("NO_ACTIVE_PACKAGE", "No active package found for this member.");

            // 4. Check branch access (skip for ALL_BRANCHES, validate HOME_ONLY etc.)
            // We load BranchAccessPolicyType codes for branch validation
            var branch = await _db.Branches
                .Where(b => b.BranchId == branchId && b.IsActive)
                .FirstOrDefaultAsync();

            if (branch == null)
                return Fail("BRANCH_NOT_FOUND", "Branch not found.");

            // Filter packages that allow access to this branch
            var accessiblePackages = new List<MemberPackage>();
            foreach (var pkg in activePackages)
            {
                var policyCode = await _db.BranchAccessPolicyTypes
                    .Where(b => b.BranchAccessPolicyTypeId == pkg.BranchAccessPolicyTypeId)
                    .Select(b => b.PolicyCode)
                    .FirstOrDefaultAsync();

                bool allowed = policyCode switch
                {
                    "ALL_BRANCHES"         => true,
                    "HOME_ONLY"            => pkg.HomeBranchId == branchId,
                    "SELECTED_BRANCHES"    => pkg.HomeBranchId == branchId
                                             || pkg.CrossBranchVisitsUsed < (pkg.CrossBranchVisitLimit ?? 0),
                    "HOME_PLUS_LIMITED"    => pkg.HomeBranchId == branchId
                                             || pkg.CrossBranchVisitsUsed < (pkg.CrossBranchVisitLimit ?? 0),
                    "CROSS_BRANCH_LIMITED" => pkg.HomeBranchId == branchId
                                             || pkg.CrossBranchVisitsUsed < (pkg.CrossBranchVisitLimit ?? 0),
                    "CUSTOM"               => true,
                    _                      => false
                };

                if (allowed) accessiblePackages.Add(pkg);
            }

            if (!accessiblePackages.Any())
                return Fail("BRANCH_ACCESS_DENIED",
                    "Member's package does not allow access to this branch.");

            // 5. Check if already inside gym right now
            var now = DateTime.UtcNow;
            var validStatusIds = await _db.AttendanceStatuses
                .Where(s => new[] { "SUCCESS", "OVERRIDE_APPROVED", "MANUAL" }.Contains(s.StatusCode))
                .Select(s => s.AttendanceStatusId)
                .ToListAsync();

            var alreadyInside = await _db.AttendanceRecords
                .AnyAsync(a => a.MemberId == member.MemberId
                            && a.BranchId == branchId
                            && validStatusIds.Contains(a.AttendanceStatusId)
                            && a.PresenceUntilUtc > now);

            // 6. Classify packages into CLASS vs non-CLASS
            var classPackages    = accessiblePackages.Where(p => p.PackageType.PackageTypeCode == "CLASS").ToList();
            var nonClassPackages = accessiblePackages.Where(p => p.PackageType.PackageTypeCode != "CLASS").ToList();

            bool hasConflict = classPackages.Any() && nonClassPackages.Any();

            // 7. Build package options for popup
            var options = accessiblePackages.Select(p => new PackageOptionDto
            {
                MemberPackageId  = p.MemberPackageId,
                PackageName      = p.PackageNameSnapshot,
                PackageTypeCode  = p.PackageType.PackageTypeCode,
                SessionsRemaining = p.SessionCountRemaining.HasValue
                    ? $"{p.SessionCountRemaining} sessions left"
                    : null,
                Label = p.PackageType.PackageTypeCode switch
                {
                    "CLASS"        => "Mark as Class",
                    "OPEN_GYM"     => "Open Gym",
                    "SESSION"      => "Session",
                    "SUBSCRIPTION" => "Subscription",
                    "BUNDLE"       => "Open Gym (Bundle)",
                    _              => p.PackageNameSnapshot
                }
            }).ToList();

            var result = new ScanResultDto
            {
                Success           = true,
                MemberId          = member.MemberId,
                MemberName        = member.FullName ?? $"{member.FirstName} {member.LastName}",
                MembershipNumber  = member.MembershipNumber,
                PhotoUrl          = member.ProfileImageUrl,
                HasConflict       = hasConflict,
                AlreadyInsideGym  = alreadyInside,
                PackageOptions    = options
            };

            // 8. If no conflict — auto check-in immediately
            if (!hasConflict && !alreadyInside)
            {
                var autoPackage = nonClassPackages.FirstOrDefault() ?? classPackages.First();
                var markResult  = await MarkAttendanceAsync(new MarkAttendanceRequest
                {
                    MemberId               = member.MemberId,
                    BranchId               = branchId,
                    SelectedMemberPackageId = autoPackage.MemberPackageId,
                    ReceptionistUserId     = Guid.Empty   // system auto
                }, tenantId);

                result.AutoCheckedInRecordId    = markResult.AttendanceRecordId;
                result.AutoCheckedInPackageName = autoPackage.PackageNameSnapshot;
            }

            return result;
        }

        // ── MarkAttendanceAsync ───────────────────────────────────
        public async Task<MarkAttendanceResult> MarkAttendanceAsync(
            MarkAttendanceRequest request, Guid tenantId)
        {
            // Load package to get HomeBranchId for cross-branch detection
            var package = await _db.MemberPackages
                .Include(p => p.PackageType)
                .FirstOrDefaultAsync(p => p.MemberPackageId == request.SelectedMemberPackageId
                                       && p.TenantId == tenantId);

            if (package == null)
                return new MarkAttendanceResult
                {
                    Success = false,
                    ErrorMessage = "Package not found."
                };

            // Load branch for presence window
            var branch = await _db.Branches
                .FirstOrDefaultAsync(b => b.BranchId == request.BranchId);

            if (branch == null)
                return new MarkAttendanceResult
                {
                    Success = false,
                    ErrorMessage = "Branch not found."
                };

            // Get SUCCESS status id
            var successStatusId = await _db.AttendanceStatuses
                .Where(s => s.StatusCode == "SUCCESS")
                .Select(s => s.AttendanceStatusId)
                .FirstOrDefaultAsync();

            var manualStatusId = await _db.AttendanceStatuses
                .Where(s => s.StatusCode == "MANUAL")
                .Select(s => s.AttendanceStatusId)
                .FirstOrDefaultAsync();

            var statusId = request.ReceptionistUserId == Guid.Empty
                ? successStatusId   // auto check-in
                : manualStatusId;   // receptionist confirmed

            var now = DateTime.UtcNow;
            bool isCrossBranch = package.HomeBranchId != request.BranchId;

            // Deduct session if package is session-based
            bool deductSession = package.SessionCountRemaining.HasValue
                              && package.PackageType.PackageTypeCode is "SESSION" or "CLASS";
            int deductedCount  = deductSession ? 1 : 0;

            var record = new AttendanceRecord
            {
                AttendanceRecordId       = Guid.NewGuid(),
                TenantId                 = tenantId,
                MemberId                 = request.MemberId,
                BranchId                 = request.BranchId,
                MemberPackageId          = request.SelectedMemberPackageId,
                AttendanceStatusId       = statusId,
                CheckInAtUtc             = now,
                PresenceUntilUtc         = now.AddMinutes(branch.MemberPresenceWindowMinutes),
                IsCrossBranchVisit       = isCrossBranch,
                SessionDeducted          = deductSession,
                SessionsDeductedCount    = deductedCount,
                OverrideApplied          = false,
                ReceptionistDecisionUserId = request.ReceptionistUserId == Guid.Empty
                    ? null
                    : request.ReceptionistUserId,
                CreatedAtUtc             = now,
                Notes                    = request.ReceptionistUserId != Guid.Empty
                    ? "Marked by receptionist"
                    : null
            };

            _db.AttendanceRecords.Add(record);

            // Deduct session count from package if applicable
            if (deductSession)
            {
                package.SessionCountRemaining -= 1;
                _db.MemberPackages.Update(package);
            }

            // Increment cross-branch visit counter
            if (isCrossBranch)
            {
                package.CrossBranchVisitsUsed += 1;
                _db.MemberPackages.Update(package);
            }

            await _db.SaveChangesAsync();

            return new MarkAttendanceResult
            {
                Success = true,
                AttendanceRecordId = record.AttendanceRecordId
            };
        }

        // ── GetBranchesAsync ──────────────────────────────────────
        public async Task<List<BranchOptionDto>> GetBranchesAsync(Guid tenantId)
        {
            return await _db.Branches
                .Where(b => b.TenantId == tenantId && b.IsActive)
                .OrderBy(b => b.BranchName)
                .Select(b => new BranchOptionDto
                {
                    BranchId   = b.BranchId,
                    BranchName = b.BranchName,
                    BranchCode = b.BranchCode
                })
                .ToListAsync();
        }

        // ── Helpers ───────────────────────────────────────────────
        private static ScanResultDto Fail(string code, string message) => new()
        {
            Success      = false,
            ErrorCode    = code,
            ErrorMessage = message
        };
    }
}
