namespace GymSaaS.Models
{
    public class AttendanceLogItem
    {
        public Guid   AttendanceRecordId  { get; set; }
        public Guid   MemberId            { get; set; }
        public string MemberName          { get; set; } = string.Empty;
        public string MembershipNumber    { get; set; } = string.Empty;
        public string? MemberPhotoUrl     { get; set; }
        public string BranchName          { get; set; } = string.Empty;
        public string StatusCode          { get; set; } = string.Empty;
        public string StatusName          { get; set; } = string.Empty;
        public DateTime CheckInAtUtc      { get; set; }
        public DateTime PresenceUntilUtc  { get; set; }
        public bool   SessionDeducted     { get; set; }
        public int    SessionsDeducted    { get; set; }
        public bool   IsCrossBranch       { get; set; }
        public bool   OverrideApplied     { get; set; }
        public string? PackageName        { get; set; }
        public string? Notes              { get; set; }

        // Display helpers
        public DateTime CheckInLocal      => CheckInAtUtc.ToLocalTime();
        public DateTime PresenceLocal     => PresenceUntilUtc.ToLocalTime();
        public bool     IsCurrentlyInside => PresenceUntilUtc > DateTime.UtcNow;

        public string MemberInitials => string.Concat(
            MemberName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                      .Take(2)
                      .Select(w => char.ToUpper(w[0])));
    }

    public class AttendanceStatsDto
    {
        public int     TotalCheckIns        { get; set; }
        public int     SessionsDeducted     { get; set; }
        public int     CrossBranchVisits    { get; set; }
        public int     OverrideCheckIns     { get; set; }
        public int     UniqueMembers        { get; set; }
    }
}
