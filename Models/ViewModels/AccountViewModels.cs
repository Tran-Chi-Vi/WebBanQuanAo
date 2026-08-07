using System.ComponentModel.DataAnnotations;
using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập Username hoặc Email")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Họ và tên không được để trống")]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(15)]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Xác nhận mật khẩu không được để trống")]
    [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProfileViewModel
{
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ và tên không được để trống")]
    public string FullName { get; set; } = string.Empty;

    [Phone]
    public string? Phone { get; set; }

    public List<Address> Addresses { get; set; } = new();

    public AddressNewViewModel NewAddress { get; set; } = new();
}

public class AddressNewViewModel
{
    [Required(ErrorMessage = "Người nhận không được để trống")]
    public string RecipientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại người nhận không được để trống")]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Địa chỉ chi tiết không được để trống")]
    public string StreetAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tỉnh/Thành phố không được để trống")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "Quận/Huyện không được để trống")]
    public string District { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phường/Xã không được để trống")]
    public string Ward { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
    [MinLength(6, ErrorMessage = "Mật khẩu mới phải từ 6 ký tự trở lên")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới")]
    [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    [DataType(DataType.Password)]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
