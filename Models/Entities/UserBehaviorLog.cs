using System.ComponentModel.DataAnnotations;

namespace WEBBANQUANAO.Models.Entities;

public enum BehaviorActionType : byte
{
    View = 0,
    Click = 1,
    AddToCart = 2,
    Purchase = 3,
    Search = 4,
    RemoveFromCart = 5,
    CheckoutView = 6,
    RageClick = 7
}

/// <summary>
/// Nhật ký hành vi người dùng — nền tảng cho thuật toán gợi ý cá nhân hóa & phân tích báo cáo.
/// Dùng bigint làm PK vì bảng tăng trưởng rất nhanh.
/// </summary>
public class UserBehaviorLog
{
    [Key]
    public long LogId { get; set; }

    public int? UserId { get; set; } // NULL nếu là khách vãng lai
    public User? User { get; set; }

    [MaxLength(100)]
    public string? SessionId { get; set; } // Định danh phiên cho khách chưa đăng nhập (sid_...)

    [MaxLength(50)]
    public string? IpAddress { get; set; } // Địa chỉ IP người dùng

    [MaxLength(20)]
    public string? DeviceType { get; set; } // Mobile, Desktop, Tablet

    [MaxLength(255)]
    public string? PageUrl { get; set; }

    public int? ProductId { get; set; } // Nullable nếu là hành vi tìm kiếm / trang chung
    public Product? Product { get; set; }

    public BehaviorActionType ActionType { get; set; }

    [MaxLength(200)]
    public string? SearchQuery { get; set; } // Từ khóa tìm kiếm nếu có

    public double DwellTimeSeconds { get; set; } = 0; // Thời gian dừng tương tác thực tế (giây)

    public bool IsRageClick { get; set; } = false; // Đánh dấu cú nhấp bực bội (3+ click / 500ms)

    [MaxLength(50)]
    public string? RecommendationSource { get; set; } // Nguồn gợi ý (VD: 'item_cf', 'content_based', 'trending')

    [MaxLength(50)]
    public string? RecommendationBlockId { get; set; } // Vị trí khối UI (VD: 'related_products', 'cart_bundle')

    public DateTime Timestamp { get; set; } = DateTime.Now;
}
