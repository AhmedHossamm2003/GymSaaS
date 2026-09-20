namespace GymSaaS.Models
{
    public class NotificationItem
    {
        public string Category { get; set; } = "";   // EXPIRING | EXPIRED | LOW_SESSIONS | PENDING_SCAN
        public string Icon { get; set; } = "bi-bell"; // bootstrap icon class
        public string Severity { get; set; } = "info"; // info | warning | danger | success
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";
        public Guid? MemberId { get; set; }            // for click-through
        public Guid? AttendanceRecordId { get; set; }  // set for PENDING_SCAN — enables inline confirm
        public DateTime WhenUtc { get; set; }
    }

    public class NotificationsViewModel
    {
        public List<NotificationItem> Items { get; set; } = new();
        public int ExpiringCount { get; set; }
        public int ExpiredCount { get; set; }
        public int LowSessionCount { get; set; }
        public int PendingScanCount { get; set; }
        public int TotalCount => Items.Count;
    }
}
