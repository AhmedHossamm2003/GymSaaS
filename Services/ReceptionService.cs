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
using GymSaaS.Models;

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

            // 6. Split ordinary entry choices from PT plans. A PT visit must
            //    always go through RecordPtSessionAsync so reception confirms
            //    the actual coach and the balance is deducted only once.
            var ptPlanPackages = accessiblePackages
                .Where(p => p.PackageType.PackageTypeCode == "PERSONAL_TRAINING"
                         && p.SessionCountRemaining > 0)
                .ToList();
            var standardPackages = accessiblePackages
                .Where(p => p.PackageType.PackageTypeCode != "PERSONAL_TRAINING")
                .Where(p => !p.SessionCountRemaining.HasValue
                         || p.SessionCountRemaining.Value > 0)
                .ToList();

            var classPackages    = standardPackages
                .Where(p => p.GymClassId.HasValue || p.PackageType.PackageTypeCode == "CLASS")
                .ToList();
            var nonClassPackages = standardPackages
                .Where(p => !p.GymClassId.HasValue && p.PackageType.PackageTypeCode != "CLASS")
                .ToList();

            // A package "consumes a session" on check-in when it is session-based
            // (a SESSION or CLASS package carrying a remaining-session count). Open-gym,
            // subscription and bundle packages grant entry without deducting anything.
            // Mirrors the deduction rule in MarkAttendanceAsync.
            static bool DeductsSession(MemberPackage p) =>
                p.SessionCountRemaining.HasValue
                && p.PackageType.PackageTypeCode is "SESSION" or "CLASS";

            var deductingPackages    = standardPackages.Where(DeductsSession).ToList();
            var nonDeductingPackages = standardPackages.Where(p => !DeductsSession(p)).ToList();

            // Session packages (SESSION type with a remaining count, not tied to a
            // specific class): checking in on one requires reception to say which
            // class the member is attending.
            var sessionPackages = nonClassPackages.Where(DeductsSession).ToList();

            bool hasPtBalance = ptPlanPackages.Any();
            if (!standardPackages.Any() && !hasPtBalance)
                return Fail("NO_AVAILABLE_VISITS", "No gym or personal-training sessions remain.");

            // Ask reception to choose whenever the visit is ambiguous, so a session is
            // never silently deducted when the member may be here for open-gym access:
            //   • a class package alongside a non-class one, OR
            //   • a session-deducting package alongside a non-deducting (open-gym) one, OR
            //   • any PT balance alongside a standard package.
            bool hasConflict = (classPackages.Any() && nonClassPackages.Any())
                || (deductingPackages.Any() && nonDeductingPackages.Any())
                || (hasPtBalance && standardPackages.Any());

            // 7. Build package options for popup
            var options = standardPackages.Select(p =>
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
                PackageOptions        = options,
                RequiresClassChoice   = sessionPackages.Any(),
            };

            // Session-package check-ins need the class picker populated with today's
            // classes at this branch (with live attendee counts / full flags).
            if (sessionPackages.Any())
                result.ClassOptions = await BuildTodayClassOptionsAsync(branchId, tenantId, validStatusIds);

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

            // 8. If no conflict — auto check-in immediately (skip if class is full — needs override).
            //    Session packages always go through the popup so reception can pick the class.
            if (!hasConflict && !sessionPackages.Any() && !alreadyInside && !result.ClassIsFull && standardPackages.Any())
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

            // 9. Surface standalone Personal Training plans only.
            result.PtPackageOptions = accessiblePackages
                .Where(p => p.PackageType.PackageTypeCode == "PERSONAL_TRAINING"
                         && p.SessionCountRemaining > 0)
                .Select(p =>
                {
                    return new PtPackageOptionDto
                    {
                        MemberPackageId = p.MemberPackageId,
                        PackageName = p.PackageNameSnapshot,
                        SourceLabel = "Personal Training plan",
                        SessionsRemaining = p.SessionCountRemaining!.Value,
                        AssignedCoachId = p.CoachId,
                    };
                })
                .OrderByDescending(p => p.SessionsRemaining)
                .ToList();

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
            if (result.PtPackageOptions.Any())
            {
                result.CoachOptions = await _db.Coaches
                    .Where(c => c.TenantId == tenantId
                             && (c.BranchId == branchId
                                 || (c.UserId.HasValue && _db.UserBranches.Any(ub =>
                                     ub.UserId == c.UserId.Value && ub.BranchId == branchId && ub.IsActive)))
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
            var dayStart = localDateNow.Date.ToUniversalTime();
            var dayEnd = localDateNow.Date.AddDays(1).ToUniversalTime();
            var classStart = new DateTime(localDateNow.Year, localDateNow.Month, localDateNow.Day,
                                          cls.StartTime.Hour, cls.StartTime.Minute, 0).ToUniversalTime();
            var classEnd   = new DateTime(localDateNow.Year, localDateNow.Month, localDateNow.Day,
                                          cls.EndTime.Hour, cls.EndTime.Minute, 0).ToUniversalTime();

            return await _db.AttendanceRecords
                .Where(a => a.TenantId == tenantId
                         && a.BranchId == cls.BranchId
                         && validStatusIds.Contains(a.AttendanceStatusId)
                         && ((a.GymClassId == cls.GymClassId
                              && a.CheckInAtUtc >= dayStart && a.CheckInAtUtc < dayEnd)
                             || (a.GymClassId == null
                                 && a.MemberPackage != null
                                 && a.MemberPackage.GymClassId == cls.GymClassId
                                 && a.CheckInAtUtc >= classStart
                                 && a.CheckInAtUtc <= classEnd)))
                .Select(a => a.MemberId)
                .Distinct()
                .CountAsync();
        }

        // ── Build the list of today's classes at a branch for the session-package
        //    class picker, with live attendee counts and full flags.
        private async Task<List<ClassOptionDto>> BuildTodayClassOptionsAsync(
            Guid branchId, Guid tenantId, List<Guid> validStatusIds)
        {
            var todayDow = (int)DateTime.Now.DayOfWeek;

            var classes = await _db.GymClasses
                .Where(g => g.TenantId == tenantId
                         && g.BranchId == branchId
                         && g.IsActive && !g.IsDeleted
                         && g.DayOfWeek == todayDow)
                .OrderBy(g => g.StartTime)
                .ToListAsync();

            var coachIds = classes.Where(c => c.CoachId.HasValue).Select(c => c.CoachId!.Value).Distinct().ToList();
            var coachMap = coachIds.Count > 0
                ? await _db.Coaches
                    .Where(c => coachIds.Contains(c.CoachId))
                    .Select(c => new { c.CoachId, Name = c.FirstName + " " + c.LastName })
                    .ToDictionaryAsync(c => c.CoachId, c => c.Name.Trim())
                : new Dictionary<Guid, string>();

            var options = new List<ClassOptionDto>();
            foreach (var g in classes)
            {
                var attendees = await CountClassAttendeesAsync(g, tenantId, validStatusIds);
                options.Add(new ClassOptionDto
                {
                    GymClassId    = g.GymClassId,
                    ClassName     = g.ClassName,
                    TimeDisplay   = $"{g.StartTime:HH:mm}–{g.EndTime:HH:mm}",
                    CoachName     = g.CoachId.HasValue && coachMap.TryGetValue(g.CoachId.Value, out var cn) ? cn : null,
                    Capacity      = g.Capacity,
                    AttendeeCount = attendees,
                    IsFull        = g.Capacity.HasValue && attendees >= g.Capacity.Value,
                });
            }

            return options;
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

            if (package.PackageType.PackageTypeCode == "PERSONAL_TRAINING")
                return new MarkAttendanceResult
                {
                    Success = false,
                    ErrorMessage = "Select the coach and use Record PT for a personal-training visit."
                };

            // CLASS check: enforce class capacity unless receptionist overrides.
            // Triggered for any class-linked session package.
            bool isClassLinked = package.GymClassId.HasValue
                              || package.PackageType.PackageTypeCode == "CLASS";

            // A plain session package (SESSION type with a remaining count) is not tied
            // to a class, so reception must pick which class the member is attending.
            bool isSessionPackage = package.SessionCountRemaining.HasValue
                                 && package.PackageType.PackageTypeCode == "SESSION";

            GymClass? targetClass;
            if (isClassLinked)
            {
                targetClass = await ResolveTargetClassAsync(package, request.BranchId, tenantId);
            }
            else if (isSessionPackage)
            {
                if (!request.SelectedGymClassId.HasValue)
                    return new MarkAttendanceResult
                    {
                        Success = false,
                        ErrorMessage = "Select the class the member is attending."
                    };

                var todayDow = (int)DateTime.Now.DayOfWeek;
                targetClass = await _db.GymClasses.FirstOrDefaultAsync(g =>
                    g.GymClassId == request.SelectedGymClassId.Value
                    && g.TenantId == tenantId
                    && g.BranchId == request.BranchId
                    && g.IsActive && !g.IsDeleted);

                if (targetClass == null)
                    return new MarkAttendanceResult
                    {
                        Success = false,
                        ErrorMessage = "The selected class was not found at this branch."
                    };

                if (targetClass.DayOfWeek != todayDow)
                    return new MarkAttendanceResult
                    {
                        Success = false,
                        ErrorMessage = "The selected class is not scheduled today."
                    };
            }
            else
            {
                targetClass = null;
            }

            if (targetClass != null && !request.OverrideClassCapacity)
            {
                if (targetClass.Capacity.HasValue)
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

            if (deductSession && package.SessionCountRemaining <= 0)
                return new MarkAttendanceResult
                {
                    Success = false,
                    ErrorMessage = "No sessions remaining on the selected package."
                };

            var record = new AttendanceRecord
            {
                AttendanceRecordId       = Guid.NewGuid(),
                TenantId                 = tenantId,
                MemberId                 = request.MemberId,
                BranchId                 = request.BranchId,
                MemberPackageId          = request.SelectedMemberPackageId,
                GymClassId               = targetClass?.GymClassId,
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
            })
            .Where(p => p.PackageTypeCode != "PERSONAL_TRAINING")
            .ToList();
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

            if (package.PackageType.PackageTypeCode == "PERSONAL_TRAINING")
                return new ConfirmPendingResult
                {
                    Success = false,
                    ErrorMessage = "Personal-training visits must be confirmed with a coach."
                };

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

        // ── GetPendingOptionsAsync ────────────────────────────────
        public async Task<PendingOptionsDto> GetPendingOptionsAsync(
            Guid attendanceRecordId, Guid tenantId)
        {
            var record = await _db.AttendanceRecords
                .Where(a => a.AttendanceRecordId == attendanceRecordId
                         && a.TenantId == tenantId)
                .Select(a => new
                {
                    a.MemberId,
                    a.BranchId,
                    a.AttendanceStatusId,
                    MemberName = a.Member.FullName ?? (a.Member.FirstName + " " + a.Member.LastName),
                    BranchName = a.Branch.BranchName,
                })
                .FirstOrDefaultAsync();

            if (record == null)
                return new PendingOptionsDto { Found = false };

            var pendingId = await _db.AttendanceStatuses
                .Where(s => s.StatusCode == "PENDING")
                .Select(s => s.AttendanceStatusId)
                .FirstOrDefaultAsync();

            var stillPending = record.AttendanceStatusId == pendingId;

            return new PendingOptionsDto
            {
                Found        = true,
                StillPending = stillPending,
                MemberName   = record.MemberName,
                BranchName   = record.BranchName,
                Options      = stillPending
                    ? await BuildPackageOptionsForMemberAsync(record.MemberId, record.BranchId, tenantId)
                    : new List<PackageOptionDto>(),
            };
        }

        // ── RecordPtSessionAsync ──────────────────────────────────
        public async Task<PerkUsageResult> RecordPtSessionAsync(
            Guid memberId, Guid memberPackageId, Guid coachId, Guid branchId,
            Guid receptionistUserId, Guid tenantId)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var package = await _db.MemberPackages
                .Include(p => p.PackageType)
                .FirstOrDefaultAsync(p => p.MemberPackageId == memberPackageId
                                       && p.MemberId == memberId
                                       && p.TenantId == tenantId
                                       && p.Status == "ACTIVE"
                                       && p.ValidFromDate <= today
                                       && (p.ValidToDate == null || p.ValidToDate >= today));

            if (package == null)
                return new PerkUsageResult { Success = false, ErrorMessage = "No active PT package was found." };

            var branch = await _db.Branches
                .FirstOrDefaultAsync(b => b.BranchId == branchId
                                       && b.TenantId == tenantId
                                       && b.IsActive);
            if (branch == null)
                return new PerkUsageResult { Success = false, ErrorMessage = "Branch not found." };

            if (package.PackageType.PackageTypeCode != "PERSONAL_TRAINING")
                return new PerkUsageResult
                {
                    Success = false,
                    ErrorMessage = "Select an active Personal Training plan."
                };

            var remaining = package.SessionCountRemaining;

            if (!remaining.HasValue || remaining.Value <= 0)
                return new PerkUsageResult { Success = false, ErrorMessage = "No PT sessions remaining." };

            var coachExists = await _db.Coaches.AnyAsync(c => c.CoachId == coachId
                                                           && c.TenantId == tenantId
                                                           && (c.BranchId == branchId
                                                               || (c.UserId.HasValue && _db.UserBranches.Any(ub =>
                                                                   ub.UserId == c.UserId.Value
                                                                   && ub.BranchId == branchId
                                                                   && ub.IsActive)))
                                                           && c.IsActive
                                                           && !c.IsDeleted);
            if (!coachExists)
                return new PerkUsageResult { Success = false, ErrorMessage = "Please select a valid coach." };

            var policyCode = await _db.BranchAccessPolicyTypes
                .Where(p => p.BranchAccessPolicyTypeId == package.BranchAccessPolicyTypeId)
                .Select(p => p.PolicyCode)
                .FirstOrDefaultAsync();
            bool branchAllowed = policyCode switch
            {
                "ALL_BRANCHES" => true,
                "HOME_ONLY" => package.HomeBranchId == branchId,
                "SELECTED_BRANCHES" or "HOME_PLUS_LIMITED" or "CROSS_BRANCH_LIMITED" =>
                    package.HomeBranchId == branchId
                    || package.CrossBranchVisitsUsed < (package.CrossBranchVisitLimit ?? 0),
                "CUSTOM" => true,
                _ => false,
            };
            if (!branchAllowed)
                return new PerkUsageResult
                {
                    Success = false,
                    ErrorMessage = "This package does not allow access to the selected branch."
                };

            package.SessionCountRemaining -= 1;

            var commissionSessionCount =
                (package.SessionCountOriginal ?? 0) + package.CarryOverSessionsAdded;
            decimal? commissionAmount = null;
            if (package.PriceSnapshot.HasValue
                && package.CoachCommissionPercent.HasValue
                && commissionSessionCount > 0)
            {
                commissionAmount = Math.Round(
                    package.PriceSnapshot.Value
                    * package.CoachCommissionPercent.Value / 100m
                    / commissionSessionCount,
                    2);
            }

            Guid? attendanceRecordId = null;
            var validStatusIds = await _db.AttendanceStatuses
                .Where(s => new[] { "SUCCESS", "OVERRIDE_APPROVED", "MANUAL" }.Contains(s.StatusCode))
                .Select(s => s.AttendanceStatusId)
                .ToListAsync();
            var alreadyInside = await _db.AttendanceRecords.AnyAsync(a =>
                a.MemberId == memberId
                && a.BranchId == branchId
                && a.TenantId == tenantId
                && validStatusIds.Contains(a.AttendanceStatusId)
                && a.CheckInAtUtc > now.AddHours(-ReentryCooldownHours));

            if (!alreadyInside)
            {
                var statusCode = receptionistUserId == Guid.Empty ? "SUCCESS" : "MANUAL";
                var statusId = await _db.AttendanceStatuses
                    .Where(s => s.StatusCode == statusCode)
                    .Select(s => s.AttendanceStatusId)
                    .FirstAsync();
                attendanceRecordId = Guid.NewGuid();
                var isCrossBranch = package.HomeBranchId != branchId;

                _db.AttendanceRecords.Add(new AttendanceRecord
                {
                    AttendanceRecordId = attendanceRecordId.Value,
                    TenantId = tenantId,
                    MemberId = memberId,
                    BranchId = branchId,
                    MemberPackageId = memberPackageId,
                    AttendanceStatusId = statusId,
                    CheckInAtUtc = now,
                    PresenceUntilUtc = now.AddMinutes(branch.MemberPresenceWindowMinutes),
                    IsCrossBranchVisit = isCrossBranch,
                    SessionDeducted = true,
                    SessionsDeductedCount = 1,
                    OverrideApplied = false,
                    ReceptionistDecisionUserId =
                        receptionistUserId == Guid.Empty ? null : receptionistUserId,
                    CreatedAtUtc = now,
                    Notes = "Personal training visit",
                });

                if (isCrossBranch)
                    package.CrossBranchVisitsUsed += 1;
            }

            _db.MemberPerkUsages.Add(new MemberPerkUsage
            {
                PerkUsageId      = Guid.NewGuid(),
                TenantId         = tenantId,
                MemberId         = memberId,
                MemberPackageId  = memberPackageId,
                PerkType         = "PT",
                CoachId          = coachId,
                AttendanceRecordId = attendanceRecordId,
                CommissionPercentSnapshot = package.CoachCommissionPercent,
                CommissionAmount = commissionAmount,
                BranchId         = branchId,
                UsedAtUtc        = now,
                RecordedByUserId = receptionistUserId == Guid.Empty ? null : receptionistUserId,
            });

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new PerkUsageResult
            {
                Success = true,
                Remaining = package.SessionCountRemaining,
                AttendanceRecordId = attendanceRecordId,
                CommissionAmount = commissionAmount,
            };
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

        // ── Paid drop-ins ─────────────────────────────────────────
        public async Task<DropInPageViewModel?> BuildDropInPageAsync(
            Guid branchId, Guid tenantId, string? phone, bool canEditPrices)
        {
            var branch = await _db.Branches
                .FirstOrDefaultAsync(b => b.BranchId == branchId
                                       && b.TenantId == tenantId
                                       && b.IsActive);
            if (branch == null) return null;

            var tenant = await _db.Tenants
                .Where(t => t.TenantId == tenantId && t.IsActive)
                .Select(t => new { t.OpenGymDropInPrice, t.OneClassPassPrice })
                .FirstOrDefaultAsync();
            if (tenant == null) return null;

            var vm = new DropInPageViewModel
            {
                BranchId = branchId,
                BranchName = branch.BranchName,
                PhoneSearch = phone?.Trim(),
                SearchPerformed = !string.IsNullOrWhiteSpace(phone),
                OpenGymPrice = tenant.OpenGymDropInPrice,
                ClassPassPrice = tenant.OneClassPassPrice,
                CanEditPrices = canEditPrices,
                Branches = await GetBranchesAsync(tenantId),
            };

            var validStatusIds = await _db.AttendanceStatuses
                .Where(s => new[] { "SUCCESS", "OVERRIDE_APPROVED", "MANUAL" }.Contains(s.StatusCode))
                .Select(s => s.AttendanceStatusId)
                .ToListAsync();

            var todayDow = (int)DateTime.Today.DayOfWeek;
            var classes = await _db.GymClasses
                .Where(g => g.TenantId == tenantId
                         && g.BranchId == branchId
                         && g.DayOfWeek == todayDow
                         && g.IsActive
                         && !g.IsDeleted)
                .OrderBy(g => g.StartTime)
                .Select(g => new
                {
                    Entity = g,
                    CoachName = g.Coach == null
                        ? null
                        : (g.Coach.FirstName + " " + g.Coach.LastName).Trim()
                })
                .ToListAsync();

            foreach (var row in classes)
            {
                vm.TodayClasses.Add(new DropInClassOption
                {
                    GymClassId = row.Entity.GymClassId,
                    ClassName = row.Entity.ClassName,
                    TimeDisplay = $"{row.Entity.StartTime:HH:mm}–{row.Entity.EndTime:HH:mm}",
                    CoachName = row.CoachName,
                    Capacity = row.Entity.Capacity,
                    AttendeeCount = await CountClassAttendeesAsync(row.Entity, tenantId, validStatusIds),
                });
            }

            if (vm.SearchPerformed)
            {
                var normalizedPhone = NormalizePhone(phone!);
                var member = await _db.Members
                    .FirstOrDefaultAsync(m => m.TenantId == tenantId
                                           && !m.IsDeleted
                                           && m.PhoneNumber.Replace(" ", "")
                                                .Replace("-", "")
                                                .Replace("(", "")
                                                .Replace(")", "") == normalizedPhone);

                if (member != null)
                {
                    var coverage = await GetDropInCoverageAsync(member.MemberId, branchId, tenantId);
                    vm.ExistingMember = new DropInMemberLookup
                    {
                        MemberId = member.MemberId,
                        MembershipNumber = member.MembershipNumber,
                        FullName = member.FullName ?? $"{member.FirstName} {member.LastName}".Trim(),
                        PhoneNumber = member.PhoneNumber,
                        Email = member.Email,
                        ProfileImageUrl = member.ProfileImageUrl,
                        HasOpenGymCoverage = coverage.OpenGym,
                        HasClassCoverage = coverage.ClassPass,
                        CoverageSummary = coverage.Summary,
                    };
                }
            }

            vm.Checkout = new DropInCheckoutViewModel
            {
                BranchId = branchId,
                MemberId = vm.ExistingMember?.MemberId,
                PhoneNumber = vm.ExistingMember?.PhoneNumber ?? vm.PhoneSearch ?? string.Empty,
                ProductCode = DropInProductCodes.OpenGym,
                FinalPrice = tenant.OpenGymDropInPrice,
                PaymentMethod = "CASH",
            };

            var recentEntries = await _db.ManualIncomeEntries
                .Where(i => i.TenantId == tenantId
                         && i.BranchId == branchId
                         && !i.IsDeleted
                         && (i.SourceCode == DropInProductCodes.OpenGym
                             || i.SourceCode == DropInProductCodes.ClassPass))
                .OrderByDescending(i => i.CreatedAtUtc)
                .Take(8)
                .Select(i => new
                {
                    i.IncomeEntryId,
                    i.SourceCode,
                    i.MemberId,
                    i.Amount,
                    i.PaymentMethod,
                    i.CreatedAtUtc,
                })
                .ToListAsync();

            var recentMemberIds = recentEntries.Where(x => x.MemberId.HasValue)
                .Select(x => x.MemberId!.Value).Distinct().ToList();
            var recentMembers = await _db.Members
                .Where(m => recentMemberIds.Contains(m.MemberId))
                .Select(m => new
                {
                    m.MemberId,
                    Name = m.FullName ?? (m.FirstName + " " + m.LastName).Trim(),
                    m.MembershipNumber,
                })
                .ToDictionaryAsync(m => m.MemberId);

            vm.RecentSales = recentEntries.Select(i =>
            {
                var member = i.MemberId.HasValue
                    ? recentMembers.GetValueOrDefault(i.MemberId.Value)
                    : null;
                return new DropInRecentSale
                {
                    IncomeEntryId = i.IncomeEntryId,
                    ProductName = i.SourceCode == DropInProductCodes.ClassPass
                        ? "One Class Pass"
                        : "Open Gym Drop-In",
                    MemberName = member?.Name ?? "Unknown member",
                    MembershipNumber = member?.MembershipNumber ?? "—",
                    Amount = i.Amount,
                    PaymentMethod = i.PaymentMethod,
                    CreatedAtUtc = i.CreatedAtUtc,
                };
            }).ToList();

            return vm;
        }

        public async Task<DropInCheckoutResult> CheckoutDropInAsync(
            DropInCheckoutViewModel request, Guid receptionistUserId, Guid tenantId)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

            var branch = await _db.Branches.FirstOrDefaultAsync(b =>
                b.BranchId == request.BranchId
                && b.TenantId == tenantId
                && b.IsActive);
            if (branch == null) return DropInFail("The selected branch is not available.");

            var tenant = await _db.Tenants.FirstOrDefaultAsync(t =>
                t.TenantId == tenantId && t.IsActive);
            if (tenant == null) return DropInFail("Business settings were not found.");

            var productCode = (request.ProductCode ?? "").Trim().ToUpperInvariant();
            if (productCode is not (DropInProductCodes.OpenGym or DropInProductCodes.ClassPass))
                return DropInFail("Select a valid drop-in type.");

            if (request.FinalPrice is null || request.FinalPrice <= 0)
                return DropInFail("The final price must be greater than zero.");

            var allowedPaymentMethods = new[] { "CASH", "CARD", "BANK", "OTHER" };
            var paymentMethod = (request.PaymentMethod ?? "").Trim().ToUpperInvariant();
            if (!allowedPaymentMethods.Contains(paymentMethod))
                return DropInFail("Select a valid payment method.");

            var basePrice = productCode == DropInProductCodes.ClassPass
                ? tenant.OneClassPassPrice
                : tenant.OpenGymDropInPrice;
            if (request.FinalPrice.Value != basePrice
                && string.IsNullOrWhiteSpace(request.PriceOverrideReason))
                return DropInFail("Enter a reason for changing the standard price.");

            Member? member = null;
            var memberCreated = false;
            if (request.MemberId.HasValue)
            {
                member = await _db.Members.FirstOrDefaultAsync(m =>
                    m.MemberId == request.MemberId.Value
                    && m.TenantId == tenantId
                    && !m.IsDeleted);
                if (member == null) return DropInFail("The selected member was not found.");
            }
            else
            {
                var normalizedPhone = NormalizePhone(request.PhoneNumber);
                member = await _db.Members.FirstOrDefaultAsync(m =>
                    m.TenantId == tenantId
                    && !m.IsDeleted
                    && m.PhoneNumber.Replace(" ", "")
                        .Replace("-", "")
                        .Replace("(", "")
                        .Replace(")", "") == normalizedPhone);

                if (member == null)
                {
                    if (string.IsNullOrWhiteSpace(request.FirstName)
                        || string.IsNullOrWhiteSpace(request.LastName)
                        || string.IsNullOrWhiteSpace(request.Email)
                        || string.IsNullOrWhiteSpace(request.PhoneNumber))
                        return DropInFail("First name, last name, email, and phone are required for a new member.");

                    var normalizedEmail = request.Email.Trim().ToUpperInvariant();
                    if (await _db.Members.AnyAsync(m => m.TenantId == tenantId
                                                     && !m.IsDeleted
                                                     && m.NormalizedEmail == normalizedEmail))
                        return DropInFail("A member with this email already exists. Search for the existing member first.");

                    var activeStatusId = await _db.MemberStatuses
                        .Where(s => s.StatusCode == "ACTIVE")
                        .Select(s => s.MemberStatusId)
                        .FirstOrDefaultAsync();
                    if (activeStatusId == Guid.Empty)
                        return DropInFail("The active member status is not configured.");

                    var password = string.IsNullOrWhiteSpace(request.Password)
                        ? "demopassword"
                        : request.Password;
                    member = new Member
                    {
                        MemberId = Guid.NewGuid(),
                        TenantId = tenantId,
                        MembershipNumber = await GenerateMembershipNumberAsync(tenantId),
                        Email = request.Email.Trim().ToLowerInvariant(),
                        NormalizedEmail = normalizedEmail,
                        PasswordHash = password,
                        PasswordSalt = null,
                        FirstName = request.FirstName.Trim(),
                        LastName = request.LastName.Trim(),
                        PhoneNumber = request.PhoneNumber.Trim(),
                        DateOfBirth = request.DateOfBirth,
                        Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim(),
                        HomeBranchId = request.BranchId,
                        MemberStatusId = activeStatusId,
                        MustChangePassword = password == "demopassword",
                        IsActive = true,
                        IsDeleted = false,
                        Notes = "Created at reception during a paid drop-in sale.",
                        CreatedAtUtc = DateTime.UtcNow,
                        CreatedByUserId = receptionistUserId,
                    };
                    _db.Members.Add(member);
                    memberCreated = true;
                }
            }

            var coverage = await GetDropInCoverageAsync(member.MemberId, request.BranchId, tenantId);
            var hasCoverage = productCode == DropInProductCodes.ClassPass
                ? coverage.ClassPass
                : coverage.OpenGym;
            if (hasCoverage && !request.ConfirmPaidDropIn)
                return DropInFail(
                    $"{coverage.Summary ?? "This member has an active package that may cover this visit."} Confirm that they still want a paid drop-in.");

            var now = DateTime.UtcNow;
            GymClass? selectedClass = null;
            if (productCode == DropInProductCodes.ClassPass)
            {
                if (!request.GymClassId.HasValue)
                    return DropInFail("Select today's class for the one-class pass.");

                var todayDow = (int)DateTime.Today.DayOfWeek;
                selectedClass = await _db.GymClasses.FirstOrDefaultAsync(g =>
                    g.GymClassId == request.GymClassId.Value
                    && g.TenantId == tenantId
                    && g.BranchId == request.BranchId
                    && g.DayOfWeek == todayDow
                    && g.IsActive
                    && !g.IsDeleted);
                if (selectedClass == null)
                    return DropInFail("The selected class is not scheduled at this branch today.");

                var validStatuses = await _db.AttendanceStatuses
                    .Where(s => new[] { "SUCCESS", "OVERRIDE_APPROVED", "MANUAL" }.Contains(s.StatusCode))
                    .Select(s => s.AttendanceStatusId)
                    .ToListAsync();
                if (selectedClass.Capacity.HasValue
                    && await CountClassAttendeesAsync(selectedClass, tenantId, validStatuses) >= selectedClass.Capacity.Value)
                    return DropInFail($"{selectedClass.ClassName} is full.");

                var alreadyInClass = await _db.AttendanceRecords.AnyAsync(a =>
                    a.TenantId == tenantId
                    && a.MemberId == member.MemberId
                    && a.GymClassId == selectedClass.GymClassId
                    && a.CheckInAtUtc >= now.Date);
                if (alreadyInClass)
                    return DropInFail("This member is already recorded for the selected class today.");
            }
            else
            {
                var validStatuses = await _db.AttendanceStatuses
                    .Where(s => new[] { "SUCCESS", "OVERRIDE_APPROVED", "MANUAL" }.Contains(s.StatusCode))
                    .Select(s => s.AttendanceStatusId)
                    .ToListAsync();
                var alreadyInside = await _db.AttendanceRecords.AnyAsync(a =>
                    a.TenantId == tenantId
                    && a.MemberId == member.MemberId
                    && a.BranchId == request.BranchId
                    && validStatuses.Contains(a.AttendanceStatusId)
                    && a.PresenceUntilUtc > now);
                if (alreadyInside)
                    return DropInFail("This member is already checked in at this branch.");
            }

            var manualStatusId = await _db.AttendanceStatuses
                .Where(s => s.StatusCode == "MANUAL")
                .Select(s => s.AttendanceStatusId)
                .FirstOrDefaultAsync();
            if (manualStatusId == Guid.Empty)
                return DropInFail("The manual attendance status is not configured.");

            var attendanceId = Guid.NewGuid();
            var presenceUntil = now.AddMinutes(branch.MemberPresenceWindowMinutes);
            if (selectedClass != null)
            {
                var localEnd = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day,
                    selectedClass.EndTime.Hour, selectedClass.EndTime.Minute, 0, DateTimeKind.Local).ToUniversalTime();
                if (localEnd > presenceUntil) presenceUntil = localEnd;
            }

            _db.AttendanceRecords.Add(new AttendanceRecord
            {
                AttendanceRecordId = attendanceId,
                TenantId = tenantId,
                MemberId = member.MemberId,
                BranchId = request.BranchId,
                MemberPackageId = null,
                GymClassId = selectedClass?.GymClassId,
                AttendanceStatusId = manualStatusId,
                CheckInAtUtc = now,
                PresenceUntilUtc = presenceUntil,
                IsCrossBranchVisit = member.HomeBranchId != request.BranchId,
                SessionDeducted = false,
                SessionsDeductedCount = 0,
                OverrideApplied = request.FinalPrice.Value != basePrice,
                ReceptionistDecisionUserId = receptionistUserId,
                Notes = productCode == DropInProductCodes.ClassPass
                    ? $"Paid one-class pass · {selectedClass!.ClassName}"
                    : "Paid open gym drop-in",
                CreatedAtUtc = now,
            });

            var incomeId = Guid.NewGuid();
            _db.ManualIncomeEntries.Add(new ManualIncomeEntry
            {
                IncomeEntryId = incomeId,
                TenantId = tenantId,
                BranchId = request.BranchId,
                CategoryCode = productCode == DropInProductCodes.ClassPass
                    ? "CLASS_PASS"
                    : "DROP_IN",
                Description = productCode == DropInProductCodes.ClassPass
                    ? $"One Class Pass · {selectedClass!.ClassName} · {member.FullName ?? member.FirstName + " " + member.LastName}"
                    : $"Open Gym Drop-In · {member.FullName ?? member.FirstName + " " + member.LastName}",
                Amount = request.FinalPrice.Value,
                BaseAmount = basePrice,
                IncomeDate = DateOnly.FromDateTime(DateTime.Today),
                PaymentMethod = paymentMethod,
                Notes = memberCreated ? "New member created during checkout." : null,
                SourceCode = productCode,
                MemberId = member.MemberId,
                AttendanceRecordId = attendanceId,
                GymClassId = selectedClass?.GymClassId,
                PriceOverrideReason = request.FinalPrice.Value != basePrice
                    ? request.PriceOverrideReason!.Trim()
                    : null,
                WasMemberCreated = memberCreated,
                CreatedAtUtc = now,
                CreatedByUserId = receptionistUserId,
            });

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new DropInCheckoutResult
            {
                Success = true,
                MemberId = member.MemberId,
                MemberName = member.FullName ?? $"{member.FirstName} {member.LastName}".Trim(),
                MembershipNumber = member.MembershipNumber,
                AttendanceRecordId = attendanceId,
                IncomeEntryId = incomeId,
                Amount = request.FinalPrice.Value,
                MemberCreated = memberCreated,
            };
        }

        public async Task<(bool Success, string? Error)> VoidDropInAsync(
            Guid incomeEntryId, Guid branchId, string reason, Guid userId, Guid tenantId)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return (false, "A cancellation reason is required.");

            await using var transaction = await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);
            var entry = await _db.ManualIncomeEntries.FirstOrDefaultAsync(i =>
                i.IncomeEntryId == incomeEntryId
                && i.TenantId == tenantId
                && i.BranchId == branchId
                && !i.IsDeleted
                && (i.SourceCode == DropInProductCodes.OpenGym
                    || i.SourceCode == DropInProductCodes.ClassPass));
            if (entry == null) return (false, "Drop-in sale not found or already cancelled.");

            if (entry.AttendanceRecordId.HasValue)
            {
                var attendance = await _db.AttendanceRecords.FirstOrDefaultAsync(a =>
                    a.AttendanceRecordId == entry.AttendanceRecordId.Value
                    && a.TenantId == tenantId);
                if (attendance != null) _db.AttendanceRecords.Remove(attendance);
            }

            entry.IsDeleted = true;
            entry.VoidedAtUtc = DateTime.UtcNow;
            entry.VoidedByUserId = userId;
            entry.VoidReason = reason.Trim();
            entry.UpdatedAtUtc = DateTime.UtcNow;
            entry.UpdatedByUserId = userId;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> UpdateDropInPricesAsync(
            DropInPriceSettingsViewModel model, Guid tenantId)
        {
            if (model.OpenGymPrice <= 0 || model.ClassPassPrice <= 0)
                return (false, "Both prices must be greater than zero.");

            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId);
            if (tenant == null) return (false, "Business settings were not found.");

            tenant.OpenGymDropInPrice = model.OpenGymPrice;
            tenant.OneClassPassPrice = model.ClassPassPrice;
            tenant.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return (true, null);
        }

        private async Task<(bool OpenGym, bool ClassPass, string? Summary)> GetDropInCoverageAsync(
            Guid memberId, Guid branchId, Guid tenantId)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var packages = await _db.MemberPackages
                .Include(p => p.PackageType)
                .Include(p => p.BranchAccessPolicyType)
                .Include(p => p.MemberPackageAllowedBranches)
                .Where(p => p.MemberId == memberId
                         && p.TenantId == tenantId
                         && p.Status == "ACTIVE"
                         && p.ValidFromDate <= today
                         && (p.ValidToDate == null || p.ValidToDate >= today))
                .ToListAsync();

            var accessible = packages.Where(p => PackageAllowsBranch(p, branchId)).ToList();
            var openGym = accessible.Any(p => p.PackageType.PackageTypeCode is "OPEN_GYM" or "SUBSCRIPTION" or "BUNDLE");
            var classPass = accessible.Any(p =>
                (p.PackageType.PackageTypeCode is "SESSION" or "CLASS")
                && (!p.SessionCountRemaining.HasValue || p.SessionCountRemaining > 0));
            var labels = new List<string>();
            if (openGym) labels.Add("active open-gym access");
            if (classPass) labels.Add("remaining class sessions");
            return (openGym, classPass,
                labels.Count == 0 ? null : "Member already has " + string.Join(" and ", labels) + ".");
        }

        private static bool PackageAllowsBranch(MemberPackage package, Guid branchId)
        {
            var policy = package.BranchAccessPolicyType?.PolicyCode;
            return policy switch
            {
                "ALL_BRANCHES" => true,
                "HOME_ONLY" => package.HomeBranchId == branchId,
                "SELECTED_BRANCHES" or "HOME_PLUS_LIMITED" or "CROSS_BRANCH_LIMITED" =>
                    package.HomeBranchId == branchId
                    || package.MemberPackageAllowedBranches.Any(x => x.BranchId == branchId),
                "CUSTOM" => true,
                _ => package.HomeBranchId == branchId,
            };
        }

        private async Task<string> GenerateMembershipNumberAsync(Guid tenantId)
        {
            var existing = await _db.Members
                .Where(m => m.TenantId == tenantId)
                .Select(m => m.MembershipNumber)
                .ToListAsync();
            var next = 1000;
            foreach (var value in existing)
                if (int.TryParse(value, out var number) && number >= next)
                    next = number + 1;
            return next.ToString();
        }

        private static string NormalizePhone(string phone) =>
            (phone ?? string.Empty).Trim()
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty);

        private static DropInCheckoutResult DropInFail(string message) =>
            new() { Success = false, ErrorMessage = message };

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
