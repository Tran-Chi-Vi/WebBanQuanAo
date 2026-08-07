using System.ComponentModel.DataAnnotations;
using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Models.Entities;

public enum BehaviorActionType : byte
{
    View = 0,
    Click = 1,
    AddToCart = 2,
    Purchase = 3,
    Search = 4
}

/// <summary>
/// Nhật ký hành vi người dùng — nền tảng cho thuật toán gợi ý cá nhân hóa.
/// Dùng bigint làm PK vì bảng tăng trưởng rất nhanh.
/// </summary>
public class UserBehaviorLog
{
    [Key]
    public long LogId { get; set; }

    public int? UserId { get; set; } // NULL nếu là khách vãng lai
    public User? User { get; set; }

    [MaxLength(100)]
    public string? SessionId { get; set; } // Định danh phiên cho khách chưa đăng nhập

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public BehaviorActionType ActionType { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;
}
