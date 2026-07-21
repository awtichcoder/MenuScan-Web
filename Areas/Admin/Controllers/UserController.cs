using System.ComponentModel.DataAnnotations;
using MenuQr.Controllers;
using MenuQr.Data;
using MenuQr.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MenuQr.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class UserController : Controller
{
    private static readonly string[] RoleOptions = ["Staff", "Cashier", "Kitchen", "Chef"];
    private static readonly HashSet<string> AllowedRoles = new(RoleOptions, StringComparer.OrdinalIgnoreCase);

    private readonly ApplicationDbContext _dbContext;

    public UserController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await BuildViewModelAsync(new CreateUserViewModel()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        model.Username = model.Username?.Trim() ?? string.Empty;
        model.FullName = model.FullName?.Trim() ?? string.Empty;
        model.Role = NormalizeRole(model.Role) ?? model.Role;

        if (!AllowedRoles.Contains(model.Role ?? string.Empty))
            ModelState.AddModelError(nameof(model.Role), "Quyen khong hop le. Khong the cap quyen Admin tai man hinh nay.");

        if (await _dbContext.Users.AnyAsync(u => u.Username == model.Username))
            ModelState.AddModelError(nameof(model.Username), "Ten dang nhap da ton tai.");

        if (!ModelState.IsValid)
            return View("Index", await BuildViewModelAsync(model));

        _dbContext.Users.Add(new User
        {
            Username = model.Username,
            PasswordHash = PasswordSecurity.Hash(model.Password),
            FullName = model.FullName,
            Role = model.Role!,
            IsActive = true
        });

        await _dbContext.SaveChangesAsync();
        TempData["SuccessMessage"] = "Da tao tai khoan nhan vien.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateUserViewModel model)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == model.UserId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Khong tim thay tai khoan.";
            return RedirectToAction(nameof(Index));
        }

        if (IsSystemAdmin(user))
        {
            TempData["ErrorMessage"] = "Tai khoan Admin he thong khong the doi quyen hoac khoa tai man hinh nay.";
            return RedirectToAction(nameof(Index));
        }

        var role = NormalizeRole(model.Role);
        if (role == null)
        {
            TempData["ErrorMessage"] = "Quyen khong hop le. Khong the cap quyen Admin tai man hinh nay.";
            return RedirectToAction(nameof(Index));
        }

        user.FullName = (model.FullName ?? string.Empty).Trim();
        user.Role = role;
        user.IsActive = model.IsActive;

        await _dbContext.SaveChangesAsync();
        TempData["SuccessMessage"] = "Da cap nhat tai khoan.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.NewPassword) || model.NewPassword.Length < 6)
        {
            TempData["ErrorMessage"] = "Mat khau moi can toi thieu 6 ky tu.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == model.UserId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Khong tim thay tai khoan.";
            return RedirectToAction(nameof(Index));
        }

        user.PasswordHash = PasswordSecurity.Hash(model.NewPassword);
        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Da reset mat khau cho {user.Username}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<UserManagementViewModel> BuildViewModelAsync(CreateUserViewModel createModel)
    {
        var users = await _dbContext.Users
            .OrderByDescending(u => u.Role == "Admin")
            .ThenByDescending(u => u.IsActive == true)
            .ThenBy(u => u.Role)
            .ThenBy(u => u.FullName)
            .Select(u => new UserListItemViewModel
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Role = u.Role,
                IsActive = u.IsActive == true
            })
            .ToListAsync();

        if (string.IsNullOrWhiteSpace(createModel.Role) || !AllowedRoles.Contains(createModel.Role))
            createModel.Role = "Staff";

        return new UserManagementViewModel
        {
            Users = users,
            CreateUser = createModel,
            RoleOptions = RoleOptions
        };
    }

    private static string? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return null;

        return RoleOptions.FirstOrDefault(r => string.Equals(r, role.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSystemAdmin(User user)
    {
        return string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase);
    }
}

public class UserManagementViewModel
{
    public List<UserListItemViewModel> Users { get; set; } = new();
    public CreateUserViewModel CreateUser { get; set; } = new();
    public IReadOnlyList<string> RoleOptions { get; set; } = Array.Empty<string>();
}

public class UserListItemViewModel
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsSystemAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
}

public class CreateUserViewModel
{
    [Required(ErrorMessage = "Vui long nhap ten dang nhap.")]
    [StringLength(100, ErrorMessage = "Ten dang nhap toi da 100 ky tu.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui long nhap ho ten.")]
    [StringLength(255, ErrorMessage = "Ho ten toi da 255 ky tu.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui long nhap mat khau.")]
    [MinLength(6, ErrorMessage = "Mat khau toi thieu 6 ky tu.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui long chon quyen.")]
    public string? Role { get; set; } = "Staff";
}

public class UpdateUserViewModel
{
    public int UserId { get; set; }
    public string? FullName { get; set; }
    public string? Role { get; set; }
    public bool IsActive { get; set; }
}

public class ResetPasswordViewModel
{
    public int UserId { get; set; }
    public string NewPassword { get; set; } = string.Empty;
}
