using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;
using WEBBANQUANAO.Services;

namespace WEBBANQUANAO.Controllers.Admin;

[Area("Admin")]
[Route("sys-admin-management/[controller]/[action]")]
[Authorize(Roles = "Admin")]
public class UserController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;

    public UserController(ApplicationDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchQuery, string? roleFilter)
    {
        var query = _context.Users
            .Include(u => u.Role)
            .Include(u => u.Orders)
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

        return View(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Details(int id)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Addresses)
            .Include(u => u.Orders)
                .ThenInclude(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                        .ThenInclude(v => v.Product)
            .Include(u => u.Orders)
                .ThenInclude(o => o.Payment)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
        {
            return NotFound();
        }

        var userReviews = await _context.Reviews
            .Include(r => r.Product)
            .Where(r => r.UserId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        decimal totalSpend = user.Orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .Sum(o => o.TotalAmount);

        ViewBag.TotalSpend = totalSpend;
        ViewBag.UserReviews = userReviews;

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestSendUserEmail(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || string.IsNullOrWhiteSpace(user.Email))
        {
            return Json(new { success = false, message = "Không tìm thấy người dùng hoặc địa chỉ Gmail rỗng!" });
        }

        try
        {
            bool isSuccess = await _emailService.SendOtpEmailAsync(user.Email, user.FullName ?? user.Username, "888888");
            if (isSuccess)
            {
                return Json(new { success = true, message = $"✅ ĐÃ GỬI EMAIL THÀNH CÔNG TỚI '{user.Email}'! Vui lòng kiểm tra Hộp thư đến (Inbox) hoặc Thư rác (Spam)." });
            }
            else
            {
                return Json(new { success = false, message = $"❌ Không thể gửi Email tới '{user.Email}'. Vui lòng kiểm tra lại biến EmailSettings__Password trên Render Dashboard." });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"❌ Lỗi khi gửi Email: {ex.Message}" });
        }
    }
}
