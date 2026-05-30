using GymSaaS.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using GymSaaS.Services;
using GymSaaS.Services.Reception;
using QuestPDF.Infrastructure;

// QuestPDF community license (free for individuals & small businesses < $1M revenue)
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// ── MVC ──────────────────────────────────────────
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

// ── Database ─────────────────────────────────────
builder.Services.AddDbContext<GymDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IReceptionService, ReceptionService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IReportsService, ReportsService>();
builder.Services.AddScoped<GymSaaS.Services.Exports.IPdfExportService, GymSaaS.Services.Exports.PdfExportService>();
builder.Services.AddScoped<GymSaaS.Services.Exports.IExcelExportService, GymSaaS.Services.Exports.ExcelExportService>();
// ── Authentication ────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Name = "GymSaaS.Auth";
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
    });

// ── Authorization Policies ────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireRole("SuperAdmin"));

    options.AddPolicy("AdminAndAbove", policy =>
        policy.RequireRole("SuperAdmin", "Admin"));

    options.AddPolicy("ManagerAndAbove", policy =>
        policy.RequireRole("SuperAdmin", "Admin", "BranchManager"));

    options.AddPolicy("AnyStaff", policy =>
        policy.RequireRole("SuperAdmin", "Admin", "BranchManager", "Receptionist"));
});

// ── Session ───────────────────────────────────────
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<GymSaaS.Services.QrCodeService>();

var app = builder.Build();

// ── Pipeline ──────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.UseAuthentication();  // must be before UseAuthorization
app.UseAuthorization();

// Default route → Dashboard (not Home)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();