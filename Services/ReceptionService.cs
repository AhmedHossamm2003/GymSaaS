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

        // Re-entry cooldown: a member cannot check in again within this many hours
        // of their previous successful check-in at the same branch.
        private const int ReentryCooldownHours = 8;

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
      .Include(p => p.GymClass)
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

            // 5. Re-entry cooldown: block re-entry within 8 h of previous check-in
            //    at the same branch (separate from the 90-min "still inside" window).
            var now = DateTime.UtcNow;
            var cooldownStart = now.AddHours(-ReentryCooldownHours);
            var validStatusIds = await _db.AttendanceStatuses
                .Where(s => new[] { "SUCCESS", "OVERRIDE_APPROVED", "MANUAL" }.Contains(s.StatusCode))
                .Select(s => s.AttendanceStatusId)
                .ToListAsync();

            var alreadyInside = await _db.AttendanceRecords
                .AnyAsync(a => a.MemberId == member.MemberId
                            && a.BranchId == branchId
                            && validStatusIds.Contains(a.AttendanceStatusId)
                            && a.CheckInAtUtc > cooldownStart);

            // 6. Class-linked vs open access.
            //    A package counts as class-linked if it has a specific GymClassId
            //    OR the type code itself is CLASS. This catches:
            //      - CLASS-only packages (type CLASS, with GymClassId)
            //      - COMBINED package's SESSION row (type SESSION, with GymClassId)
            //    Everything else (OPEN_GYM, SUBSCRIPTION, BUNDLE, COMBINED's open-gym row) is open access.
            var classPackages    = accessiblePackages
                .Where(p => p.GymClassId.HasValue || p.PackageType.PackageTypeCode == "CLASS")
                .ToList();
            var nonClassPackages = accessiblePackages
                .Where(p => !p.GymClassId.HasValue && p.PackageType.PackageTypeCode != "CLASS")
                .ToList();

            // Conflict = the receptionist must choose. True whenever there's both a
            // class-linked option and an open-gym option (e.g. COMBINED package).
            bool hasConflict = classPackages.Any() && nonClassPackages.Any();

            // 7. Build package options for popup
            var options = accessiblePackages.Select(p =>
            {
                bool isClassLinked = p.GymClassId.HasValue || p.PackageType.PackageTypeCode == "CLASS";
                string label = isClassLinked
                    ? (p.GymClass != null ? $"Class · {p.GymClass.ClassName}" : "Class")
                    : p.PackageType.PackageTypeCode switch
                    {
                        "OPEN_GYM"     => "Open Gym",
                        "SESSION"      => "Session",
                        "SUBSCRIPTION" => "Subscription",
                        "BUNDLE"       => "Open Gym (Bundle)",
                        _              => p.PackageNameSnapshot
                    };

                return new PackageOptionDto
                {
                    MemberPackageId  = p.MemberPackageId,
                    PackageName      = p.PackageNameSnapshot,
                    PackageTypeCode  = isClassLinked ? "CLASS" : p.PackageType.PackageTypeCode,
                    SessionsRemaining = p.SessionCountRemaining.HasValue
                        ? $"{p.SessionCountRemaining} sessions left"
                        : null,
                    Label = label
                };
            }).ToList();

            var primaryPackage = accessiblePackages.OrderBy(p => p.ValidToDate).FirstOrDefault();
            string? expiryDisplay = primaryPackage?.ValidToDate.HasValue == true
                ? primaryPackage.ValidToDate!.Value.ToString("MMM d, yyyy")
                : null;

            var result = new ScanResultDto
            {
                Success               = true,
                MemberId              = member.MemberId,
                MemberName            = member.FullName ?? $"{member.FirstName} {member.LastName}",
                MembershipNumber      = member.MembershipNumber,
                PhotoUrl              = member.ProfileImageUrl,
                PhoneNumber           = member.PhoneNumber,
                ActivePackageName     = primaryPackage?.PackageNameSnapshot,
                ActivePackageExpiry   = expiryDisplay,
                HasConflict           = hasConflict,
                AlreadyInsideGym      = alreadyInside,
                PackageOptions        = options
            };

            // 7b. For CLASS packages: locate the relevant class (linked or currently live)
            //     and verify capacity before any auto check-in.
            GymClass? targetClass = null;
            var classCheckPackage = classPackages.FirstOrDefault();
            if (classCheckPackage != null)
            {
                targetClass = await ResolveTargetClassAsync(classCheckPackage, branchId, tenantId);
            }

            if (targetClass != null)
            {
                var classCounts = await CountClassAttendeesAsync(targetClass, tenantId, validStatusIds);

                string? coachName = null;
                if (targetClass.CoachId.HasValue)
                {
                    coachName = await _db.Coaches
                        .Where(c => c.CoachId == targetClass.CoachId.Value)
                        .Select(c => (c.FirstName + " " + c.LastName).Trim())
                        .FirstOrDefaultAsync();
                }

                result.TargetGymClassId     = targetClass.GymClassId;
                result.TargetClassName      = targetClass.ClassName;
                result.TargetClassTime      = $"{targetClass.StartTime:HH:mm}–{targetClass.EndTime:HH:mm}";
                result.TargetCoachName      = coachName;
                result.TargetClassCapacity  = targetClass.Capacity;
                result.TargetClassAttendees = classCounts;
                result.ClassIsFull          = targetClass.Capacity.HasValue && classCounts >= targetClass.Capacity.Value;
            }

            // 8. If no conflict — auto check-in immediately (skip if class is full — needs override)
            if (!hasConflict && !alreadyInside && !result.ClassIsFull)
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

            // 9. Non-attendance perks — surface PT sessions / InBody scans the member
            //    still has, so reception can record them (independent of gym entry).
            var ptPackage = activePackages
                .Where(p => p.PtSessionsRemaining.HasValue && p.PtSessionsRemaining.Value > 0)
                .OrderByDescending(p => p.PtSessionsRemaining)
                .FirstOrDefault();
            if (ptPackage != null)
            {
                result.PtSessionsRemaining = ptPackage.PtSessionsRemaining;
                result.PtPackageId         = ptPackage.MemberPackageId;
                result.PtAssignedCoachId   = ptPackage.CoachId?.ToString();
            }

            var inBodyPackage = activePackages
                .Where(p => p.InBodyRemaining.HasValue && p.InBodyRemaining.Value > 0)
                .OrderByDescending(p => p.InBodyRemaining)
                .FirstOrDefault();
            if (inBodyPackage != null)
            {
                result.InBodyRemaining = inBodyPackage.InBodyRemaining;
                result.InBodyPackageId = inBodyPackage.MemberPackageId;
            }

            // Coach picker (only needed when PT sessions are available).
            if (result.PtSessionsRemaining.HasValue)
            {
                result.CoachOptions = await _db.Coaches
                    .Where(c => c.TenantId == tenantId && c.BranchId == branchId
                             && c.IsActive && !c.IsDeleted)
                    .OrderBy(c => c.FirstName).ThenBy(c => c.LastName)
                    .Select(c => new CoachOptionDto
                    {
                        CoachId = c.CoachId,
                        Name    = (c.FirstName + " " + c.LastName).Trim()
                    })
                    .ToListAsync();
            }

            return result;
        }

        // ── Resolve the GymClass a CLASS-type member package check-in refers to.
        //    Priority: 1) the package's directly-linked GymClass (if it's today + at this branch),
        //              2) the live class right now at this branch.
        private async Task<GymClass?> ResolveTargetClassAsync(MemberPackage classPkg, Guid branchId, Guid tenantId)
        {
            var localNow = DateTime.Now;
            var todayDow = (int)localNow.DayOfWeek;
            var nowTime  = TimeOnly.FromDateTime(localNow);

            // Linked class — only accept if it's at this branch and runs today.
            if (classPkg.GymClass != null
                && classPkg.GymClass.BranchId == branchId
                && classPkg.GymClass.DayOfWeek == todayDow
                && !classPkg.GymClass.IsDeleted && classPkg.GymClass.IsActive)
            {
                return classPkg.GymClass;
            }

            // Otherwise find any class live RIGHT NOW at this branch.
            return await _db.GymClasses
                .Where(g => g.TenantId == tenantId
                         && g.BranchId == branchId
                         && g.IsActive && !g.IsDeleted
                         && g.DayOfWeek == todayDow
                         && g.StartTime <= nowTime
                         && g.EndTime >= nowTime)
                .OrderBy(g => g.StartTime)
                .FirstOrDefaultAsync();
        }

        // ── Count today's attendees that fall inside the class's time window.
        private async Task<int> CountClassAttendeesAsync(GymClass cls, Guid tenantId, List<Guid> validStatusIds)
        {
            var localDateNow = DateTime.Now;
            var classStart = new DateTime(localDateNow.Year, localDateNow.Month, localDateNow.Day,
                                          cls.StartTime.Hour, cls.StartTime.Minute, 0).ToUniversalTime();
            var classEnd   = new DateTime(localDateNow.Year, localDateNow.Month, localDateNow.Day,
                                          cls.EndTime.Hour, cls.EndTime.Minute, 0).ToUniversalTime();

            return await _db.AttendanceRecords
                .Where(a => a.TenantId == tenantId
                         && a.BranchId == cls.BranchId
                         && validStatusIds.Contains(a.AttendanceStatusId)
                         && a.CheckInAtUtc >= classStart
                         && a.CheckInAtUtc <= classEnd
                         && a.MemberPackage != null
                         && a.MemberPackage.GymClassId == cls.GymClassId)
                .Select(a => a.MemberId)
                .Distinct()
                .CountAsync();
        }

        // ── MarkAttendanceAsync ───────────────────────────────────
        public async Task<MarkAttendanceResult> MarkAttendanceAsync(
            MarkAttendanceRequest request, Guid tenantId)
        {
            // Load package to get HomeBranchId for cross-branch detection
            var package = await _db.MemberPackages
                .Include(p => p.PackageType)
                .Include(p => p.GymClass)
                .FirstOrDefaultAsync(p => p.MemberPackageId == request.SelectedMemberPackageId
                                       && p.TenantId == tenantId);

            if (package == null)
                return new MarkAttendanceResult
                {
                    Success = false,
                    ErrorMessage = "Package not found."
                };

            // CLASS check: enforce class capacity unless receptionist overrides.
            // Triggered for any class-linked package — including COMBINED's SESSION row.
            bool isClassLinked = package.GymClassId.HasValue
                              || package.PackageType.PackageTypeCode == "CLASS";

            if (isClassLinked && !request.OverrideClassCapacity)
            {
                var targetClass = await ResolveTargetClassAsync(package, request.BranchId, tenantId);
                if (targetClass != null && targetClass.Capacity.HasValue)
                {
                    var validStatusIds = await _db.AttendanceStatuses
                        .Where(s => new[] { "SUCCESS", "OVERRIDE_APPROVED", "MANUAL" }.Contains(s.StatusCode))
                        .Select(s => s.AttendanceStatusId)
                        .ToListAsync();

                    var attendeeCount = await CountClassAttendeesAsync(targetClass, tenantId, validStatusIds);
                    if (attendeeCount >= targetClass.Capacity.Value)
                    {
                        return new MarkAttendanceResult
                        {
                            Success = false,
                            ErrorMessage = $"Class \"{targetClass.ClassName}\" is full ({attendeeCount}/{targetClass.Capacity.Value})."
                        };
                    }
                }
            }

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
                Notes                    = request.OverrideClassCapacity
                    ? "Class-capacity override by receptionist"
                    : request.ReceptionistUserId != Guid.Empty
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

        // ── GetLatestCheckInAsync ─────────────────────────────────
        public async Task<LatestCheckInDto?> GetLatestCheckInAsync(
            Guid branchId, Guid tenantId, DateTime sinceUtc)
        {
            // Include PENDING so mobile scans awaiting a reception choice surface
            // in the same poll. We capture the status code to branch on it below.
            var statuses = await _db.AttendanceStatuses
                .Where(s => new[] { "SUCCESS", "OVERRIDE_APPROVED", "MANUAL", "PENDING" }.Contains(s.StatusCode))
                .Select(s => new { s.AttendanceStatusId, s.StatusCode })
                .ToListAsync();

            var statusIds   = statuses.Select(s => s.AttendanceStatusId).ToList();
            var pendingId   = statuses.FirstOrDefault(s => s.StatusCode == "PENDING")?.AttendanceStatusId;

            var record = await _db.AttendanceRecords
                .Where(a => a.BranchId == branchId
                         && a.TenantId == tenantId
                         && statusIds.Contains(a.AttendanceStatusId)
                         && a.CheckInAtUtc > sinceUtc)
                .OrderByDescending(a => a.CheckInAtUtc)
                .Select(a => new
                {
                    a.AttendanceRecordId,
                    a.MemberId,
                    a.CheckInAtUtc,
                    a.MemberPackageId,
                    a.AttendanceStatusId,
                    PackageName = a.MemberPackage != null ? a.MemberPackage.PackageNameSnapshot : null,
                    SessionsRemaining = a.MemberPackage != null ? a.MemberPackage.SessionCountRemaining : null,
                })
                .FirstOrDefaultAsync();

            if (record == null) return null;

            var member = await _db.Members
                .Where(m => m.MemberId == record.MemberId)
                .Select(m => new
                {
                    m.FullName,
                    m.FirstName,
                    m.LastName,
                    m.MembershipNumber,
                    m.ProfileImageUrl,
                    m.PhoneNumber,
                })
                .FirstOrDefaultAsync();

            if (member == null) return null;

            bool isPending = pendingId.HasValue && record.AttendanceStatusId == pendingId.Value;

            var dto = new LatestCheckInDto
            {
                AttendanceRecordId = record.AttendanceRecordId,
                MemberId           = record.MemberId,
                MemberName         = member.FullName ?? $"{member.FirstName} {member.LastName}",
                MembershipNumber   = member.MembershipNumber,
                PhotoUrl           = member.ProfileImageUrl,
                PhoneNumber        = member.PhoneNumber,
                PackageName        = record.PackageName,
                SessionsRemaining  = record.SessionsRemaining,
                CheckInAtUtc       = record.CheckInAtUtc.ToString("O"),
                RequiresChoice     = isPending,
            };

            if (isPending)
                dto.PackageOptions = await BuildPackageOptionsForMemberAsync(record.MemberId, branchId, tenantId);

            return dto;
        }

        // ── BuildPackageOptionsForMemberAsync ─────────────────────
        // Returns the class/session + open-gym options a member is eligible for
        // at a branch — used to populate the receptionist's pending-choice popup.
        private async Task<List<PackageOptionDto>> BuildPackageOptionsForMemberAsync(
            Guid memberId, Guid branchId, Guid tenantId)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var activePackages = await _db.MemberPackages
                .Include(p => p.PackageType)
                .Include(p => p.GymClass)
                .Where(p => p.MemberId == memberId
                         && p.TenantId == tenantId
                         && p.Status == "ACTIVE"
                         && p.ValidFromDate <= today
                         && (p.ValidToDate == null || p.ValidToDate >= today))
                .ToListAsync();

            var accessible = new List<MemberPackage>();
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

                if (allowed) accessible.Add(pkg);
            }

            return accessible.Select(p =>
            {
                bool isClassLinked = p.GymClassId.HasValue || p.PackageType.PackageTypeCode == "CLASS";
                string label = isClassLinked
                    ? (p.GymClass != null ? $"Class · {p.GymClass.ClassName}" : "Class")
                    : p.PackageType.PackageTypeCode switch
                    {
                        "OPEN_GYM"     => "Open Gym",
                        "SESSION"      => "Session",
                        "SUBSCRIPTION" => "Subscription",
                        "BUNDLE"       => "Open Gym (Bundle)",
                        _              => p.PackageNameSnapshot
                    };

                return new PackageOptionDto
                {
                    MemberPackageId   = p.MemberPackageId,
                    PackageName       = p.PackageNameSnapshot,
                    PackageTypeCode   = isClassLinked ? "CLASS" : p.PackageType.PackageTypeCode,
                    SessionsRemaining = p.SessionCountRemaining.HasValue
                        ? $"{p.SessionCountRemaining} sessions left"
                        : null,
                    Label = label
                };
            }).ToList();
        }

        // ── ConfirmPendingAsync ───────────────────────────────────
        public async Task<ConfirmPendingResult> ConfirmPendingAsync(
            Guid attendanceRecordId, Guid selectedMemberPackageId, Guid receptionistUserId, Guid tenantId)
        {
            var record = await _db.AttendanceRecords
                .FirstOrDefaultAsync(a => a.AttendanceRecordId == attendanceRecordId
                                       && a.TenantId == tenantId);

            if (record == null)
                return new ConfirmPendingResult { Success = false, ErrorMessage = "Scan record not found." };

            var pendingId = await _db.AttendanceStatuses
                .Where(s => s.StatusCode == "PENDING")
                .Select(s => s.AttendanceStatusId)
                .FirstOrDefaultAsync();

            // Only a still-pending record can be confirmed (idempotency guard).
            if (record.AttendanceStatusId != pendingId)
                return new ConfirmPendingResult { Success = false, ErrorMessage = "This scan was already confirmed." };

            var package = await _db.MemberPackages
                .Include(p => p.PackageType)
                .FirstOrDefaultAsync(p => p.MemberPackageId == selectedMemberPackageId
                                       && p.TenantId == tenantId);

            if (package == null)
                return new ConfirmPendingResult { Success = false, ErrorMessage = "Package not found." };

            var manualStatusId = await _db.AttendanceStatuses
                .Where(s => s.StatusCode == "MANUAL")
                .Select(s => s.AttendanceStatusId)
                .FirstOrDefaultAsync();

            // Deduct a session if the chosen package is session-based.
            bool deductSession = package.SessionCountRemaining.HasValue
                              && package.PackageType.PackageTypeCode is "SESSION" or "CLASS";

            if (deductSession && package.SessionCountRemaining <= 0)
                return new ConfirmPendingResult
                {
                    Success = false,
                    ErrorMessage = "No sessions remaining on the selected package."
                };

            var branch = await _db.Branches.FirstOrDefaultAsync(b => b.BranchId == record.BranchId);
            var now = DateTime.UtcNow;

            record.MemberPackageId        = package.MemberPackageId;
            record.AttendanceStatusId     = manualStatusId;
            record.SessionDeducted        = deductSession;
            record.SessionsDeductedCount  = deductSession ? 1 : 0;
            record.IsCrossBranchVisit     = package.HomeBranchId != record.BranchId;
            record.ReceptionistDecisionUserId = receptionistUserId == Guid.Empty ? null : receptionistUserId;
            record.PresenceUntilUtc       = now.AddMinutes(branch?.MemberPresenceWindowMinutes ?? 90);
            record.Notes                  = deductSession
                ? "Class attendance (reception-confirmed mobile scan)"
                : "Open gym attendance (reception-confirmed mobile scan)";

            if (deductSession)
                package.SessionCountRemaining -= 1;

            if (record.IsCrossBranchVisit)
                package.CrossBranchVisitsUsed += 1;

            await _db.SaveChangesAsync();

            return new ConfirmPendingResult
            {
                Success = true,
                SessionsRemaining = package.SessionCountRemaining,
                PackageName = package.PackageNameSnapshot
            };
        }

        // ── RecordPtSessionAsync ──────────────────────────────────
        public async Task<PerkUsageResult> RecordPtSessionAsync(
            Guid memberId, Guid memberPackageId, Guid coachId, Guid branchId,
            Guid receptionistUserId, Guid tenantId)
        {
            var package = await _db.MemberPackages
                .FirstOrDefaultAsync(p => p.MemberPackageId == memberPackageId
                                       && p.MemberId == memberId
                                       && p.TenantId == tenantId);

            if (package == null)
                return new PerkUsageResult { Success = false, ErrorMessage = "Package not found." };

            if (!package.PtSessionsRemaining.HasValue || package.PtSessionsRemaining.Value <= 0)
                return new PerkUsageResult { Success = false, ErrorMessage = "No PT sessions remaining." };

            var coachExists = await _db.Coaches.AnyAsync(c => c.CoachId == coachId
                                                           && c.TenantId == tenantId);
            if (!coachExists)
                return new PerkUsageResult { Success = false, ErrorMessage = "Please select a valid coach." };

            package.PtSessionsRemaining -= 1;

            _db.MemberPerkUsages.Add(new MemberPerkUsage
            {
                PerkUsageId      = Guid.NewGuid(),
                TenantId         = tenantId,
                MemberId         = memberId,
                MemberPackageId  = memberPackageId,
                PerkType         = "PT",
                CoachId          = coachId,
                BranchId         = branchId,
                UsedAtUtc        = DateTime.UtcNow,
                RecordedByUserId = receptionistUserId == Guid.Empty ? null : receptionistUserId,
            });

            await _db.SaveChangesAsync();

            return new PerkUsageResult { Success = true, Remaining = package.PtSessionsRemaining };
        }

        // ── RecordInBodyAsync ─────────────────────────────────────
        public async Task<PerkUsageResult> RecordInBodyAsync(
            Guid memberId, Guid memberPackageId, Guid branchId,
            Guid receptionistUserId, Guid tenantId)
        {
            var package = await _db.MemberPackages
                .FirstOrDefaultAsync(p => p.MemberPackageId == memberPackageId
                                       && p.MemberId == memberId
                                       && p.TenantId == tenantId);

            if (package == null)
                return new PerkUsageResult { Success = false, ErrorMessage = "Package not found." };

            if (!package.InBodyRemaining.HasValue || package.InBodyRemaining.Value <= 0)
                return new PerkUsageResult { Success = false, ErrorMessage = "No InBody scans remaining." };

            package.InBodyRemaining -= 1;

            _db.MemberPerkUsages.Add(new MemberPerkUsage
            {
                PerkUsageId      = Guid.NewGuid(),
                TenantId         = tenantId,
                MemberId         = memberId,
                MemberPackageId  = memberPackageId,
                PerkType         = "INBODY",
                CoachId          = null,
                BranchId         = branchId,
                UsedAtUtc        = DateTime.UtcNow,
                RecordedByUserId = receptionistUserId == Guid.Empty ? null : receptionistUserId,
            });

            await _db.SaveChangesAsync();

            return new PerkUsageResult { Success = true, Remaining = package.InBodyRemaining };
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
