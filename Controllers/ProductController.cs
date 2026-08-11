using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FashionStore.Web.Services;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;
using WEBBANQUANAO.Models.ViewModels;
using WEBBANQUANAO.Services;

namespace WEBBANQUANAO.Controllers;

public class ProductController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAprioriService _aprioriService;

    public ProductController(ApplicationDbContext context, IAprioriService aprioriService)
    {
        _context = context;
        _aprioriService = aprioriService;
    }

    [HttpGet]
    public async Task<IActionResult> LiveSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Json(new List<object>());
        }

        string q = query.Trim().ToLower();
        var results = await _context.Products
            .Where(p => p.Status == ProductStatus.Active &&
                       (p.ProductName.ToLower().Contains(q) || p.Category.CategoryName.ToLower().Contains(q)))
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Take(6)
            .Select(p => new
            {
                id = p.ProductId,
                name = p.ProductName,
                category = p.Category.CategoryName,
                price = $"{p.BasePrice:N0}đ",
                image = p.Images.FirstOrDefault(i => i.IsMain).ImageUrl ?? p.Images.FirstOrDefault().ImageUrl ?? "/images/no-image.png"
            })
            .ToListAsync();

        return Json(results);
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchQuery,
        int? categoryId,
        int? brandId,
        ProductGender? gender,
        decimal? minPrice,
        decimal? maxPrice,
        string? sortBy,
        int page = 1)
    {
        int pageSize = 12;
        var query = _context.Products
            .Where(p => p.Status == ProductStatus.Active)
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Include(p => p.Reviews)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string keyword = searchQuery.Trim().ToLower();
            query = query.Where(p => p.ProductName.ToLower().Contains(keyword) ||
                                     (p.Description != null && p.Description.ToLower().Contains(keyword)));
        }

        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
        if (brandId.HasValue) query = query.Where(p => p.BrandId == brandId.Value);

        if (gender.HasValue)
        {
            if (gender.Value == ProductGender.Male)
                query = query.Where(p => p.Gender == ProductGender.Male || p.Gender == ProductGender.Unisex);
            else if (gender.Value == ProductGender.Female)
                query = query.Where(p => p.Gender == ProductGender.Female || p.Gender == ProductGender.Unisex);
            else
                query = query.Where(p => p.Gender == gender.Value);
        }

        if (minPrice.HasValue) query = query.Where(p => p.BasePrice >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(p => p.BasePrice <= maxPrice.Value);

        var favIds = GetFavoriteProductIds();
        ViewBag.FavoriteProductIds = favIds;

        query = sortBy switch
        {
            "price_asc" => query.OrderBy(p => p.BasePrice),
            "price_desc" => query.OrderByDescending(p => p.BasePrice),
            "name" => query.OrderBy(p => p.ProductName),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (favIds.Any())
        {
            products = products.OrderByDescending(p => favIds.Contains(p.ProductId)).ToList();
        }

        var categories = await _context.Categories.Where(c => c.ParentCategoryId == null).Include(c => c.SubCategories).ToListAsync();
        var brands = await _context.Brands.ToListAsync();

        ViewBag.Categories = categories;
        ViewBag.Brands = brands;
        ViewBag.SearchQuery = searchQuery;
        ViewBag.CategoryId = categoryId;
        ViewBag.BrandId = brandId;
        ViewBag.Gender = gender;
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.SortBy = sortBy;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        var viewModel = new ProductListViewModel
        {
            Products = products,
            Categories = categories,
            Brands = brands,
            SearchQuery = searchQuery,
            CategoryId = categoryId,
            BrandId = brandId,
            Gender = gender,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            SortBy = sortBy ?? "newest",
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalItems
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Include(p => p.Reviews)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.ProductId == id && p.Status == ProductStatus.Active);

        if (product == null) return NotFound();

        int userId = GetCurrentUserId();
        string sessionId = HttpContext.Session.Id;

        try
        {
            _context.UserBehaviorLogs.Add(new UserBehaviorLog
            {
                UserId = userId > 0 ? userId : null,
                SessionId = sessionId,
                ProductId = id,
                ActionType = BehaviorActionType.View,
                Timestamp = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UserBehaviorLog error: {ex.Message}");
        }

        List<Product> aprioriRecommendations = new List<Product>();
        try
        {
            var aprioriRules = await _aprioriService.GetRecommendationsAsync(id, topN: 4);
            if (aprioriRules != null)
            {
                aprioriRecommendations = aprioriRules
                    .Select(r => r.ConsequentProduct)
                    .Where(p => p != null && p.Status == ProductStatus.Active)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Apriori error: {ex.Message}");
        }

        List<Product> personalizedRecommendations = new List<Product>();
        if (userId > 0)
        {
            try
            {
                var viewedCategoryIds = await _context.UserBehaviorLogs
                    .Include(l => l.Product)
                    .Where(l => l.UserId == userId && l.ProductId != id && l.Product != null)
                    .OrderByDescending(l => l.Timestamp)
                    .Select(l => l.Product.CategoryId)
                    .Distinct()
                    .Take(10)
                    .ToListAsync();

                if (viewedCategoryIds.Any())
                {
                    personalizedRecommendations = await _context.Products
                        .Where(p => p.ProductId != id && p.Status == ProductStatus.Active && viewedCategoryIds.Contains(p.CategoryId))
                        .Include(p => p.Images)
                        .Include(p => p.Category)
                        .Take(4)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Personalized error: {ex.Message}");
            }
        }

        if (!personalizedRecommendations.Any())
        {
            personalizedRecommendations = await _context.Products
                .Where(p => p.ProductId != id && p.CategoryId == product.CategoryId && p.Status == ProductStatus.Active)
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Take(4)
                .ToListAsync();
        }

        ViewBag.AprioriRecommendations = aprioriRecommendations;
        ViewBag.PersonalizedRecommendations = personalizedRecommendations;

        return View(product);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(int productId, byte rating, string? comment)
    {
        int userId = GetCurrentUserId();
        if (userId == 0) return RedirectToAction("Login", "Account");

        if (rating < 1 || rating > 5)
        {
            TempData["ErrorMessage"] = "Số sao đánh giá phải từ 1 đến 5 sao.";
            return RedirectToAction("Details", new { id = productId });
        }

        var review = new Review
        {
            UserId = userId,
            ProductId = productId,
            Rating = rating,
            Comment = comment?.Trim(),
            CreatedAt = DateTime.Now
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Cảm ơn bạn đã gửi đánh giá cho sản phẩm!";
        return RedirectToAction("Details", new { id = productId });
    }

    [HttpPost]
    public IActionResult ToggleFavorite(int productId)
    {
        var favIds = GetFavoriteProductIds();
        bool isFavorite;
        if (favIds.Contains(productId))
        {
            favIds.Remove(productId);
            isFavorite = false;
        }
        else
        {
            favIds.Add(productId);
            isFavorite = true;
        }
        SaveFavoriteProductIds(favIds);
        return Json(new {
            success = true,
            isFavorite,
            message = isFavorite ? "Đã thêm vào danh sách yêu thích! Sản phẩm này sẽ ưu tiên hiển thị ở trên đầu." : "Đã xóa khỏi danh sách yêu thích."
        });
    }

    private List<int> GetFavoriteProductIds()
    {
        var sessionStr = HttpContext.Session.GetString("FavoriteProductIds");
        if (string.IsNullOrEmpty(sessionStr)) return new List<int>();
        return sessionStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    private void SaveFavoriteProductIds(List<int> ids)
    {
        HttpContext.Session.SetString("FavoriteProductIds", string.Join(",", ids.Distinct()));
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
    }
}
