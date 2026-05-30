using System.ComponentModel.DataAnnotations;

namespace GymSaaS.Models;

// ── Friendly labels & grouping for view permissions ──
public static class ViewPermissionCatalog
{
    // Maps raw folder name → (friendly label, group, icon)
    public static readonly Dictionary<string, (string Label, string Group, string Icon)> Catalog =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Overview
            ["Dashboard"]         = ("Dashboard",            "Overview",      "bi-grid-1x2"),

            // Operations
            ["Reception"]         = ("Live Reception",       "Operations",    "bi-display"),
            ["Attendance"]        = ("Attendance Logs",      "Operations",    "bi-qr-code-scan"),

            // Members
            ["Members"]           = ("Members",              "Members",       "bi-people"),
            ["Invitations"]       = ("Invitations",          "Members",       "bi-ticket-detailed"),
            ["MemberPackages"]    = ("Assign Packages",      "Members",       "bi-ticket-perforated"),

            // Management
            ["Branches"]          = ("Branches",             "Management",    "bi-building"),
            ["PackageCatalog"]    = ("Package Catalog",      "Management",    "bi-box-seam"),
            ["Users"]             = ("Staff Users",          "Management",    "bi-people-fill"),
            ["Roles"]             = ("Roles & Permissions",  "Management",    "bi-shield-lock"),

            // Finance
            ["Reports"]           = ("Reports & Analytics",  "Finance",       "bi-bar-chart-line"),
            ["Expenses"]          = ("Expenses",             "Finance",       "bi-receipt"),
            ["Income"]            = ("Income",               "Finance",       "bi-cash-stack"),

            // Communication
            ["Notifications"]     = ("Notifications",        "Communication", "bi-bell"),

            // Content
            ["Coaches"]           = ("Coaches",              "Content",       "bi-person-badge"),
            ["Classes"]           = ("Classes",              "Content",       "bi-calendar2-week"),
            ["Exercises"]         = ("Exercise Library",     "Content",       "bi-activity"),
            ["WorkoutTemplates"]  = ("Workout Templates",    "Content",       "bi-journal-text"),

            // System
            ["AuditLogs"]         = ("Audit Logs",           "System",        "bi-journal-text"),
            ["Settings"]          = ("Settings",             "System",        "bi-gear"),
        };

    // Ordered group display in the picker (matches sidebar)
    public static readonly string[] GroupOrder = new[]
    {
        "Overview", "Operations", "Members", "Management",
        "Finance", "Communication", "Content", "System"
    };

    public static (string Label, string Group, string Icon) Resolve(string folderName)
    {
        if (Catalog.TryGetValue(folderName, out var entry))
            return entry;
        // Fallback: prettify the folder name
        return (PrettifyFolderName(folderName), "Other", "bi-folder");
    }

    private static string PrettifyFolderName(string raw)
    {
        // Insert spaces before capitals: "MemberPackages" → "Member Packages"
        if (string.IsNullOrEmpty(raw)) return raw;
        var chars = new List<char> { raw[0] };
        for (int i = 1; i < raw.Length; i++)
        {
            if (char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1]))
                chars.Add(' ');
            chars.Add(raw[i]);
        }
        return new string(chars.ToArray());
    }
}

public class RoleListItemViewModel
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? RoleDescription { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public int ViewsCount { get; set; }
}

public class ViewPermissionCheckboxViewModel
{
    public Guid ViewPermissionId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public class CreateRoleViewModel
{
    [Required]
    [StringLength(100)]
    public string RoleName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? RoleDescription { get; set; }

    public bool IsActive { get; set; } = true;

    public List<ViewPermissionCheckboxViewModel> Permissions { get; set; } = new();
}

public class EditRoleViewModel : CreateRoleViewModel
{
    public Guid RoleId { get; set; }
    public bool IsSystem { get; set; }
}