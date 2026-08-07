using System.ComponentModel.DataAnnotations;
using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Models.ViewModels;

public class AdminDashboardViewModel
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int PendingOrdersCount { get; set; }
    public int TotalProducts { get; set; }
    public int TotalCustomers { get; set; }

    public List<Order> RecentOrders { get; set; } = new();
    public List<ProductVariant> LowStockVariants { get; set; } = new();
    public List<TopProductItem> TopSellingProducts { get; set; } = new();
    public List<MonthlyRevenueItem> RevenueByMonth { get; set; } = new();
}

public class TopProductItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int TotalSold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class MonthlyRevenueItem
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Revenue { get; set; }
}

public class AdminProductFormViewModel
{
    public int ProductId { get; set; }

    [Required(ErrorMessage = "Tên sản phẩm không được để trống")]
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn danh mục")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thương hiệu")]
    public int BrandId { get; set; }

    public ProductGender Gender { get; set; } = ProductGender.Unisex;

    [Required(ErrorMessage = "Giá gốc không được để trống")]
    [Range(0, 1000000000, ErrorMessage = "Giá sản phẩm phải hợp lệ")]
    public decimal BasePrice { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Active;

    public string? MainImageUrl { get; set; }

    public List<Category> AvailableCategories { get; set; } = new();
    public List<Brand> AvailableBrands { get; set; } = new();
    public List<AdminVariantViewModel> Variants { get; set; } = new();
    public List<ProductImage> Images { get; set; } = new();
}

public class AdminVariantViewModel
{
    public int VariantId { get; set; }
    public int ProductId { get; set; }

    [Required(ErrorMessage = "Size không được để trống")]
    public string Size { get; set; } = string.Empty;

    [Required(ErrorMessage = "Màu sắc không được để trống")]
    public string Color { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mã SKU không được để trống")]
    public string SKU { get; set; } = string.Empty;

    [Required(ErrorMessage = "Giá biến thể không được để trống")]
    [Range(0, 1000000000)]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Số lượng tồn kho không được để trống")]
    [Range(0, 100000)]
    public int StockQuantity { get; set; }
}

public class AdminOrderListViewModel
{
    public List<Order> Orders { get; set; } = new();
    public OrderStatus? StatusFilter { get; set; }
    public string? SearchQuery { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalOrdersCount { get; set; }
}

public class AdminPromotionFormViewModel
{
    public int PromotionId { get; set; }

    [Required(ErrorMessage = "Mã giảm giá không được để trống")]
    [MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

    [Required(ErrorMessage = "Giá trị giảm không được để trống")]
    [Range(0, 100000000)]
    public decimal DiscountValue { get; set; }

    [Range(0, 100000000)]
    public decimal MinOrderValue { get; set; } = 0;

    [Required(ErrorMessage = "Ngày bắt đầu không được để trống")]
    public DateTime StartDate { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Ngày kết thúc không được để trống")]
    public DateTime EndDate { get; set; } = DateTime.Now.AddDays(30);
}
