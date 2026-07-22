using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MenuQr.Data;
using MenuQr.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MenuQr.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public AccountController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectByRole(User);

        await EnsureDefaultUsersAsync();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        await EnsureDefaultUsersAsync();

        if (!ModelState.IsValid)
            return View(model);

        var username = model.Username.Trim();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive == true);

        if (user == null || !PasswordSecurity.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Ten dang nhap hoac mat khau khong dung.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new("Username", user.Username)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectByRole(new ClaimsPrincipal(identity));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("/Account/Logout")]
    public async Task<IActionResult> LogoutDirect()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectByRole(ClaimsPrincipal principal)
    {
        if (principal.IsInRole("Admin"))
            return RedirectToAction("Index", "Revenue", new { area = "Admin" });

        if (principal.IsInRole("Staff") || principal.IsInRole("Cashier"))
            return RedirectToAction("Index", "Staff", new { area = "Staff" });

        if (principal.IsInRole("Kitchen") || principal.IsInRole("Chef"))
            return RedirectToAction("Index", "Kitchen", new { area = "Chef" });

        return RedirectToAction("Index", "Home");
    }

    private async Task EnsureDefaultUsersAsync()
    {
        await UpsertDefaultUserAsync("admin", "admin123", "Quan tri vien", "Admin");
        await UpsertDefaultUserAsync("staff", "staff123", "Nhan vien", "Staff");
        await UpsertDefaultUserAsync("kitchen", "kitchen123", "Bếp Trưởng", "Kitchen");

        await _dbContext.SaveChangesAsync();
    }

    private async Task UpsertDefaultUserAsync(string username, string password, string fullName, string role)
    {
        var userExists = await _dbContext.Users.AnyAsync(u => u.Username == username);
        if (userExists)
            return;

        _dbContext.Users.Add(new User
        {
            Username = username,
            PasswordHash = PasswordSecurity.Hash(password),
            FullName = fullName,
            Role = role,
            IsActive = true
        });
    }
}

public static class PasswordSecurity
{
    private const string Prefix = "SHA256:";

    public static string Hash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Prefix + Convert.ToBase64String(bytes);
    }

    public static bool Verify(string password, string storedHash)
    {
        if (storedHash.StartsWith(Prefix, StringComparison.Ordinal))
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(Hash(password)),
                Encoding.UTF8.GetBytes(storedHash));

        return storedHash == password;
    }
}
