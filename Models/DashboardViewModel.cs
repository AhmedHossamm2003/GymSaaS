namespace GymSaaS.Models
{
    public class DashboardViewModel
    {
        public int ActiveMembers     { get; set; }
        public int AttendanceToday   { get; set; }
        public int CurrentlyInside   { get; set; }
        public int ExpiringPackages  { get; set; }
        public int LowSessionMembers { get; set; }

        public List<BranchSummaryItem>    BranchSummaries  { get; set; } = new();
        public List<RecentAttendanceItem> RecentAttendance { get; set; } = new();
    }

    public class BranchSummaryItem
    {
        public Guid   BranchId        { get; set; }
        public string BranchName      { get; set; } = string.Empty;
        public string City            { get; set; } = string.Empty;
        public int    AttendanceToday  { get; set; }
        public int    CurrentlyInside  { get; set; }
    }

    public class RecentAttendanceItem
    {
        public string    MemberName       { get; set; } = string.Empty;
        public string    MembershipNumber { get; set; } = string.Empty;
        public string?   ProfileImageUrl  { get; set; }
        public DateTime  CheckInAtUtc     { get; set; }
        public Guid      BranchId         { get; set; }
        public string    BranchName       { get; set; } = string.Empty;
        public bool      OverrideApplied  { get; set; }

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.UtcNow - CheckInAtUtc;
                if (diff.TotalMinutes < 1)  return "just now";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours   < 24) return $"{(int)diff.TotalHours}h ago";
                return CheckInAtUtc.ToString("MMM d");
            }
        }
    }
}
