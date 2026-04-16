using GymSaaS.Models;
using GymSaaS.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    public class AuthController : Controller
    {
        private readonly GymDbContext _db;
        private readonly ILogger<AuthController> _logger;

        public AuthController(GymDbContext db, ILogger<AuthController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ─────────────────────────────────────────────
        // GET /Auth/Login
        // ─────────────────────────────────────────────
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        // ─────────────────────────────────────────────
        // POST /Auth/Login
        // ─────────────────────────────────────────────
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var normalizedEmail = model.Email.Trim().ToUpperInvariant();

                // 1. Find user by email
                var user = await _db.Users
                    .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail
                                           && u.DeletedAtUtc == null);

                if (user == null)
                {
                    _logger.LogWarning("Login failed: email {Email} not found.", model.Email);
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                    return View(model);
                }

                // 2. Account locked
                if (user.IsLocked)
                {
                    ModelState.AddModelError(string.Empty, "Your account has been locked. Please contact the administrator.");
                    return View(model);
                }

                // 3. Account inactive
                if (!user.IsActive)
                {
                    ModelState.AddModelError(string.Empty, "Your account is inactive. Please contact the administrator.");
                    return View(model);
                }

                // 4. Plain text password check — TODO: replace with hashing later
                if (model.Password != user.PasswordHash)
                {
                    _logger.LogWarning("Login failed: wrong password for user {UserId}.", user.UserId);
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                    return View(model);
                }

                // 5. Load roles
                var userRoles = await _db.UserRoles
                    .Where(ur => ur.UserId == user.UserId)
                    .Join(_db.Roles, ur => ur.RoleId, r => r.RoleId, (ur, r) => r.RoleName)
                    .ToListAsync();

                // 6. Load branch assignments
                var branchIds = await _db.UserBranches
                    .Where(ub => ub.UserId == user.UserId && ub.IsActive)
                    .Select(ub => ub.BranchId.ToString())
                    .ToListAsync();

                // 7. Build claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name,           $"{user.FirstName} {user.LastName}".Trim()),
                    new Claim(ClaimTypes.Email,          user.Email),
                    new Claim("TenantId",                user.TenantId.ToString()),
                    new Claim("FirstName",               user.FirstName),
                    new Claim("LastName",                user.LastName),
                };

                foreach (var role in userRoles)
                    claims.Add(new Claim(ClaimTypes.Role, role));

                foreach (var branchId in branchIds)
                    claims.Add(new Claim("BranchId", branchId));

                // 8. Sign in with cookie
                var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                var authProps = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc   = model.RememberMe
                        ? DateTimeOffset.UtcNow.AddDays(30)
                        : DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    authProps);

                // 9. Update last login timestamp
                user.LastLoginAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                _logger.LogInformation("User {UserId} logged in successfully.", user.UserId);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for {Email}.", model.Email);
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                return View(model);
            }
        }

        // ─────────────────────────────────────────────
        // POST /Auth/Logout
        // ─────────────────────────────────────────────
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Auth");
        }

        // ─────────────────────────────────────────────
        // GET /Auth/AccessDenied
        // ─────────────────────────────────────────────
        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();
    }
}
