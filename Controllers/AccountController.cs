using MenuQr.Data;
using MenuQr.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MenuQr.Controllers
{
    public class AccountController : Controller
    {
        private readonly MenuDbContext _sqlContext;

        public AccountController(MenuDbContext sqlContext)
        {
            _sqlContext = sqlContext;
        }

        // Login Page: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl)
        {
            // If already logged in, redirect to correct panel
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectUserByRole(User.FindFirst(ClaimTypes.Role)?.Value);
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // Login Action (POST)
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError(string.Empty, "Please enter both username and password.");
                return View();
            }

            // 1. Hash entered password
            var hashedPassword = PasswordHelper.HashPassword(password);

            // 2. Query SQL Server for user
            var user = await _sqlContext.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.PasswordHash == hashedPassword);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View();
            }

            // 3. Create claims principal
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FullName", user.FullName)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            // 4. Sign in user
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            // 5. Redirect based on returnUrl or role
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectUserByRole(user.Role);
        }

        // Logout Action: /Account/Logout
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // Access Denied: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // Redirect helper based on staff role
        private IActionResult RedirectUserByRole(string? role)
        {
            if (role == "Admin")
            {
                return RedirectToAction("Index", "Admin");
            }
            if (role == "Kitchen")
            {
                return RedirectToAction("Index", "Kitchen");
            }
            if (role == "Cashier")
            {
                return RedirectToAction("Index", "Cashier");
            }
            return RedirectToAction("Index", "Home");
        }
    }
}
