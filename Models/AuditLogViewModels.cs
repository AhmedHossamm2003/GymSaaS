namespace GymSaaS.Models
{
    public class ActivityLogItem
    {
        public DateTime WhenUtc { get; set; }
        public string Category { get; set; } = "";   // CHECKIN | PACKAGE | PT | INBODY | LOGIN
        public string Icon { get; set; } = "bi-dot";
        public string Color { get; set; } = "#6b7280";
        public string Actor { get; set; } = "";       // who/what
        public string Action { get; set; } = "";       // what happened
        public string? Detail { get; set; }
        public Guid? MemberId { get; set; }
    }

    public class AuditLogsViewModel
    {
        public List<ActivityLogItem> Items { get; set; } = new();
        public string? CategoryFilter { get; set; }
    }
}
