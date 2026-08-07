using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Controllers.Admin;

[Area("Admin")]
[Route("sys-admin-management/[controller]/[action]")]
[Authorize(Roles = "Admin")]
public class UserAccountController : Controller
{
    private readonly ApplicationDbContext _context;

    public UserAccountController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchQuery, string? roleFilter)
    {
        bool isUnlocked = HttpContext.Session.GetString("AdminUserAccountUnlocked") == "true";
        ViewBag.IsUnlocked = isUnlocked;

        if (!isUnlocked)
        {
            ViewBag.SearchQuery = searchQuery;
            ViewBag.RoleFilter = roleFilter;
            return View(new List<User>());
        }

        var query = _context.Users
            .Include(u => u.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string keyword = searchQuery.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(keyword) ||
                                     u.Username.ToLower().Contains(keyword) ||
                                     u.Email.ToLower().Contains(keyword) ||
                                     (u.Phone != null && u.Phone.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(roleFilter))
        {
            query = query.Where(u => u.Role.RoleName == roleFilter);
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        ViewBag.SearchQuery = searchQuery;
        ViewBag.RoleFilter = roleFilter;
        ViewBag.Roles = await _context.Roles.ToListAsync();

        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyPin(string pin)
    {
        if (pin == "291005")
        {
            HttpContext.Session.SetString("AdminUserAccountUnlocked", "true");
            TempData["SuccessMessage"] = "Xác thực 2 lớp thành công! Đã mở khóa Quản Lý Tài Khoản Người Dùng.";
            return RedirectToAction(nameof(Index));
        }

        TempData["ErrorMessage"] = "Mã PIN bảo vệ 2 lớp không chính xác! Vui lòng thử lại.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult LockSession()
    {
        HttpContext.Session.Remove("AdminUserAccountUnlocked");
        TempData["InfoMessage"] = "Đã khóa phiên truy cập Quản Lý Tài Khoản Người Dùng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAccount(int userId, string fullName, string email, string? phone, int roleId)
    {
        if (HttpContext.Session.GetString("AdminUserAccountUnlocked") != "true")
        {
            TempData["ErrorMessage"] = "Phiên làm việc đã bị khóa! Vui lòng xác thực PIN.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy người dùng!";
            return RedirectToAction(nameof(Index));
        }

        user.FullName = fullName.Trim();
        user.Email = email.Trim();
        user.Phone = phone?.Trim();
        user.RoleId = roleId;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã cập nhật thông tin tài khoản @{user.Username}!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int userId, string newPassword)
    {
        if (HttpContext.Session.GetString("AdminUserAccountUnlocked") != "true")
        {
            TempData["ErrorMessage"] = "Phiên làm việc đã bị khóa! Vui lòng xác thực PIN.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy người dùng!";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            TempData["ErrorMessage"] = "Mật khẩu mới phải có ít nhất 6 ký tự!";
            return RedirectToAction(nameof(Index));
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã đổi mật khẩu thành công cho tài khoản @{user.Username}!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(int userId)
    {
        if (HttpContext.Session.GetString("AdminUserAccountUnlocked") != "true")
        {
            TempData["ErrorMessage"] = "Phiên làm việc đã bị khóa! Vui lòng xác thực PIN.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy tài khoản!";
            return RedirectToAction(nameof(Index));
        }

        if (user.Role?.RoleName == "Admin" && user.Username == "admin")
        {
            TempData["ErrorMessage"] = "Không thể xóa tài khoản Quản Trị Viên hệ thống gốc!";
            return RedirectToAction(nameof(Index));
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã xóa tài khoản @{user.Username} khỏi hệ thống!";
        return RedirectToAction(nameof(Index));
    }
}
