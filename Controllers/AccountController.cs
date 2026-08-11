using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;
using WEBBANQUANAO.Models.ViewModels;
using WEBBANQUANAO.Services;

namespace WEBBANQUANAO.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;

    public AccountController(ApplicationDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectUserByRole(User.IsInRole("Admin"), returnUrl);
        }

        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == model.UsernameOrEmail || u.Email == model.UsernameOrEmail);

        if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !VerifyPassword(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError("", "Tên đăng nhập/email hoặc mật khẩu không chính xác.");
            return View(model);
        }

        await SignInUserAsync(user, model.RememberMe);

        TempData["SuccessMessage"] = $"Chào mừng {user.FullName} đã quay trở lại!";

        bool isAdmin = user.Role?.RoleName == "Admin";
        return RedirectUserByRole(isAdmin, model.ReturnUrl);
    }

    #region External Authentication (Google & Facebook)

    [HttpGet]
    public IActionResult GoogleLogin()
    {
        var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { provider = "Google" });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public IActionResult FacebookLogin()
    {
        var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { provider = "Facebook" });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, FacebookDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string provider)
    {
        var scheme = provider == "Google" ? GoogleDefaults.AuthenticationScheme : FacebookDefaults.AuthenticationScheme;
        var result = await HttpContext.AuthenticateAsync(scheme);

        if (!result.Succeeded || result.Principal == null)
        {
            TempData["ErrorMessage"] = $"Đăng nhập qua {provider} thất bại hoặc bị hủy.";
            return RedirectToAction("Login");
        }

        var claims = result.Principal.Claims;
        var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? $"{provider} User";
        var providerKey = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var avatarUrl = claims.FirstOrDefault(c => c.Type == "picture" || c.Type == "urn:facebook:picture" || c.Type.EndsWith("picture") || c.Type.Contains("avatar"))?.Value;

        if (provider == "Facebook" && string.IsNullOrEmpty(avatarUrl) && !string.IsNullOrEmpty(providerKey))
        {
            avatarUrl = $"https://graph.facebook.com/{providerKey}/picture?type=large";
        }

        if (string.IsNullOrEmpty(email))
        {
            email = $"{providerKey ?? Guid.NewGuid().ToString().Substring(0, 8)}@{provider.ToLower()}.fashionstore.vn";
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email ||
                (provider == "Google" && u.GoogleId == providerKey) ||
                (provider == "Facebook" && u.FacebookId == providerKey));

        if (user == null)
        {
            var customerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Customer");
            int roleId = customerRole?.RoleId ?? 2;

            user = new User
            {
                UserGuid = Guid.NewGuid(),
                Username = $"{provider.ToLower()}_{Guid.NewGuid().ToString().Substring(0, 6)}",
                Email = email,
                FullName = name,
                RoleId = roleId,
                GoogleId = provider == "Google" ? providerKey : null,
                FacebookId = provider == "Facebook" ? providerKey : null,
                AvatarUrl = avatarUrl,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.Carts.Add(new Cart { UserId = user.UserId });
            await _context.SaveChangesAsync();
        }
        else
        {
            if (provider == "Google" && string.IsNullOrEmpty(user.GoogleId)) user.GoogleId = providerKey;
            if (provider == "Facebook" && string.IsNullOrEmpty(user.FacebookId)) user.FacebookId = providerKey;
            if (!string.IsNullOrEmpty(avatarUrl)) user.AvatarUrl = avatarUrl;
            await _context.SaveChangesAsync();
        }

        await SignInUserAsync(user, isPersistent: true);
        TempData["SuccessMessage"] = $"Đăng nhập thành công bằng {provider}! Chào mừng {user.FullName}.";

        return RedirectUserByRole(user.Role?.RoleName == "Admin", null);
    }

    #endregion

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (await _context.Users.AnyAsync(u => u.Username == model.Username))
        {
            ModelState.AddModelError("Username", "Tên đăng nhập này đã được sử dụng.");
            return View(model);
        }

        if (await _context.Users.AnyAsync(u => u.Email == model.Email))
        {
            ModelState.AddModelError("Email", "Email này đã được sử dụng.");
            return View(model);
        }

        var customerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Customer");
        int roleId = customerRole?.RoleId ?? 2;

        var newUser = new User
        {
            UserGuid = Guid.NewGuid(),
            FullName = model.FullName,
            Username = model.Username,
            Email = model.Email,
            Phone = model.Phone,
            PasswordHash = HashPassword(model.Password),
            RoleId = roleId,
            CreatedAt = DateTime.Now
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        var newCart = new Cart
        {
            UserId = newUser.UserId
        };
        _context.Carts.Add(newCart);
        await _context.SaveChangesAsync();

        await SignInUserAsync(newUser, isPersistent: false);

        TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Chào mừng bạn mua sắm tại cửa hàng.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["InfoMessage"] = "Bạn đã đăng xuất tài khoản.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        int userId = GetCurrentUserId();
        var user = await _context.Users
            .Include(u => u.Addresses)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null) return NotFound();

        // 1. Tính tổng tiêu dùng & Phân hạng khách hàng (Tính tất cả các đơn hàng hợp lệ chưa bị hủy)
        decimal totalSpent = await _context.Orders
            .Where(o => o.UserId == userId && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        var tierInfo = MembershipTierHelper.CalculateTier(totalSpent);
        ViewBag.TierInfo = tierInfo;

        var model = new ProfileViewModel
        {
            UserId = user.UserId,
            Username = user.Username ?? "",
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Addresses = user.Addresses.ToList()
        };

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
    {
        int userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        user.FullName = model.FullName;
        user.Phone = model.Phone;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Cập nhật thông tin cá nhân thành công!";
        return RedirectToAction("Profile");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAddress(AddressNewViewModel model)
    {
        int userId = GetCurrentUserId();

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thông tin địa chỉ không hợp lệ.";
            return RedirectToAction("Profile");
        }

        if (model.IsDefault)
        {
            var oldDefaults = await _context.Addresses.Where(a => a.UserId == userId && a.IsDefault).ToListAsync();
            foreach (var addr in oldDefaults) addr.IsDefault = false;
        }

        var newAddress = new Address
        {
            UserId = userId,
            RecipientName = model.RecipientName,
            Phone = model.Phone,
            DetailAddress = model.StreetAddress,
            Province = model.City,
            District = model.District,
            Ward = model.Ward,
            IsDefault = model.IsDefault || !await _context.Addresses.AnyAsync(a => a.UserId == userId)
        };

        _context.Addresses.Add(newAddress);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Thêm địa chỉ mới thành công!";
        return RedirectToAction("Profile");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        int userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        if (string.IsNullOrEmpty(user.PasswordHash) || !VerifyPassword(model.CurrentPassword, user.PasswordHash))
        {
            TempData["ErrorMessage"] = "Mật khẩu hiện tại không đúng.";
            return RedirectToAction("Profile");
        }

        user.PasswordHash = HashPassword(model.NewPassword);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
        return RedirectToAction("Profile");
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> MyOrders()
    {
        int userId = GetCurrentUserId();
        var orders = await _context.Orders
            .Where(o => o.UserId == userId)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Variant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Images)
            .Include(o => o.Payment)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return View(orders);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> OrderDetails(int id)
    {
        int userId = GetCurrentUserId();
        var order = await _context.Orders
            .Include(o => o.Address)
            .Include(o => o.Promotion)
            .Include(o => o.Payment)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Variant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        if (order == null) return NotFound();

        return View(order);
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    #region Helper Methods

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
    }

    private async Task SignInUserAsync(User user, bool isPersistent)
    {
        // Generate new UserGuid for single active device session validation
        user.UserGuid = Guid.NewGuid();
        try
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Update UserGuid error: {ex.Message}");
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim("UserGuid", user.UserGuid.ToString()),
            new Claim(ClaimTypes.Name, user.Username ?? user.Email),
            new Claim("FullName", user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "Customer")
        };

        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            claims.Add(new Claim("AvatarUrl", user.AvatarUrl));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            ExpiresUtc = isPersistent ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(12)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
    }

    private IActionResult RedirectUserByRole(bool isAdmin, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        if (isAdmin)
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        return RedirectToAction("Index", "Home");
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private static bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword)) return false;
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
        catch
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password + "WEBBANQUANAO_SALT_2026");
            return Convert.ToBase64String(sha256.ComputeHash(bytes)) == hashedPassword;
        }
    }
    #endregion

    #region Forgot Password OTP Flow

    [HttpPost]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Email))
        {
            return Json(new { success = false, message = "Vui lòng nhập địa chỉ Gmail tài khoản đã đăng ký!" });
        }

        string email = request.Email.Trim().ToLower();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email || u.Username.ToLower() == email);

        if (user == null)
        {
            return Json(new { success = false, message = "Tài khoản Gmail này chưa được đăng ký trong hệ thống!" });
        }

        // Generate 6-digit OTP
        var random = new Random();
        string otpCode = random.Next(100000, 999999).ToString();

        HttpContext.Session.SetString("ResetOtpCode", otpCode);
        HttpContext.Session.SetString("ResetOtpEmail", user.Email);
        HttpContext.Session.SetString("ResetOtpExpiry", DateTime.UtcNow.AddMinutes(10).ToString("o"));

        // Send OTP Email
        await _emailService.SendOtpEmailAsync(user.Email, user.FullName, otpCode);

        return Json(new { 
            success = true, 
            message = $"Mã OTP đã được gửi thành công đến Gmail {user.Email}! Vui lòng kiểm tra hộp thư."
        });
    }

    [HttpPost]
    public IActionResult VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.OtpCode))
        {
            return Json(new { success = false, message = "Vui lòng nhập mã OTP xác thực!" });
        }

        string savedOtp = HttpContext.Session.GetString("ResetOtpCode") ?? "";
        string expiryStr = HttpContext.Session.GetString("ResetOtpExpiry") ?? "";

        if (string.IsNullOrEmpty(savedOtp) || string.IsNullOrEmpty(expiryStr))
        {
            return Json(new { success = false, message = "Mã OTP chưa được khởi tạo hoặc đã hết hạn. Vui lòng gửi lại!" });
        }

        if (DateTime.TryParse(expiryStr, out var expiry) && DateTime.UtcNow > expiry)
        {
            return Json(new { success = false, message = "Mã OTP đã hết hạn (quá 10 phút). Vui lòng gửi lại mã mới!" });
        }

        if (request.OtpCode.Trim() != savedOtp)
        {
            return Json(new { success = false, message = "Mã OTP không chính xác! Vui lòng kiểm tra lại." });
        }

        HttpContext.Session.SetString("ResetOtpVerified", "true");
        return Json(new { success = true, message = "Xác thực mã OTP thành công! Vui lòng nhập mật khẩu mới." });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPasswordWithOtp([FromBody] ResetPasswordOtpRequest request)
    {
        bool isVerified = HttpContext.Session.GetString("ResetOtpVerified") == "true";
        string targetEmail = HttpContext.Session.GetString("ResetOtpEmail") ?? "";

        if (!isVerified || string.IsNullOrEmpty(targetEmail))
        {
            return Json(new { success = false, message = "Phiên xác thực OTP không hợp lệ hoặc đã hết hạn!" });
        }

        if (string.IsNullOrWhiteSpace(request?.NewPassword) || request.NewPassword.Length < 6)
        {
            return Json(new { success = false, message = "Mật khẩu mới phải có ít nhất 6 ký tự!" });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == targetEmail);
        if (user == null)
        {
            return Json(new { success = false, message = "Không tìm thấy người dùng!" });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();

        HttpContext.Session.Remove("ResetOtpCode");
        HttpContext.Session.Remove("ResetOtpEmail");
        HttpContext.Session.Remove("ResetOtpExpiry");
        HttpContext.Session.Remove("ResetOtpVerified");

        return Json(new { success = true, message = "Đổi mật khẩu thành công! Bạn có thể đăng nhập ngay bằng mật khẩu mới.", email = user.Email });
    }

    #endregion
}

public class SendOtpRequest { public string Email { get; set; } = ""; }
public class VerifyOtpRequest { public string OtpCode { get; set; } = ""; }
public class ResetPasswordOtpRequest { public string NewPassword { get; set; } = ""; }

