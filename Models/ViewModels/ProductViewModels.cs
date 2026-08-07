using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Models.ViewModels;

public class ProductListViewModel
{
    public List<Product> Products { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<Brand> Brands { get; set; } = new();

    // Filters
    public int? CategoryId { get; set; }
    public int? BrandId { get; set; }
    public ProductGender? Gender { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? SearchQuery { get; set; }
    public string SortBy { get; set; } = "newest"; // newest, price_asc, price_desc, popular

    // Pagination
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalItems { get; set; }
}

public class ProductDetailViewModel
{
    public Product Product { get; set; } = null!;
    public List<AssociationRule> Recommendations { get; set; } = new();
    public List<Product> RelatedProducts { get; set; } = new();
    public bool IsFavorite { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
}

public class AddReviewViewModel
{
    public int ProductId { get; set; }
    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
}
