using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Services;

public interface IRecommendationService
{
    Task<List<Product>> GetPersonalizedRecommendationsAsync(int? userId, string? sessionId, int limit = 8);
    Task<List<Product>> GetTrendingAndTopSearchProductsAsync(int limit = 8);
    Task<List<Product>> GetFrequentlyBoughtTogetherAsync(int productId, int limit = 4);
}

public class RecommendationService : IRecommendationService
{
    private readonly ApplicationDbContext _context;

    public RecommendationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Product>> GetPersonalizedRecommendationsAsync(int? userId, string? sessionId, int limit = 8)
    {
        // 1. Fetch recent behavior logs (past 30 days)
        var now = DateTime.Now;
        var startDate = now.AddDays(-30);

        var userLogs = await _context.UserBehaviorLogs
            .Where(l => l.Timestamp >= startDate &&
                        ((userId.HasValue && l.UserId == userId) || (!string.IsNullOrEmpty(sessionId) && l.SessionId == sessionId)))
            .OrderByDescending(l => l.Timestamp)
            .Take(50)
            .ToListAsync();

        // 2. Fallback to Trending if user has no past logs
        if (!userLogs.Any())
        {
            return await GetTrendingAndTopSearchProductsAsync(limit);
        }

        // Extract viewed product IDs & categories
        var interactedProductIds = userLogs.Where(l => l.ProductId.HasValue).Select(l => l.ProductId!.Value).Distinct().ToList();
        var interactedCategories = await _context.Products
            .Where(p => interactedProductIds.Contains(p.ProductId))
            .Select(p => p.CategoryId)
            .Distinct()
            .ToListAsync();

        // Query recommended products in same categories, excluding discontinued / out of stock
        var recommended = await _context.Products
            .Where(p => p.Status == ProductStatus.Active &&
                        p.Variants.Any(v => v.StockQuantity > 0) &&
                        !interactedProductIds.Contains(p.ProductId) &&
                        interactedCategories.Contains(p.CategoryId))
            .Include(p => p.Images)
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();

        // If not enough recommendations, fill with top selling/trending
        if (recommended.Count < limit)
        {
            var fillCount = limit - recommended.Count;
            var fillProducts = await GetTrendingAndTopSearchProductsAsync(fillCount * 2);
            foreach (var fp in fillProducts)
            {
                if (!recommended.Any(r => r.ProductId == fp.ProductId) && !interactedProductIds.Contains(fp.ProductId))
                {
                    recommended.Add(fp);
                    if (recommended.Count >= limit) break;
                }
            }
        }

        return recommended;
    }

    public async Task<List<Product>> GetTrendingAndTopSearchProductsAsync(int limit = 8)
    {
        try
        {
            var startDate = DateTime.Now.AddDays(-30);

            // Fetch top search queries safely
            var topSearchQueries = await _context.UserBehaviorLogs
                .Where(l => l.Timestamp >= startDate && l.SearchQuery != null && l.SearchQuery != "")
                .GroupBy(l => l.SearchQuery)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key!)
                .Take(3)
                .ToListAsync();

            // Fetch products matching top search keywords or top ordered
            IQueryable<Product> query = _context.Products
                .Where(p => p.Status == ProductStatus.Active && p.Variants.Any(v => v.StockQuantity > 0));

            if (topSearchQueries.Any())
            {
                var firstQuery = topSearchQueries.First();
                query = query.OrderByDescending(p => p.ProductName.Contains(firstQuery) || p.Category.CategoryName.Contains(firstQuery))
                             .ThenByDescending(p => p.CreatedAt);
            }
            else
            {
                query = query.OrderByDescending(p => p.CreatedAt);
            }

            var results = await query
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .Take(limit)
                .ToListAsync();

            return results;
        }
        catch
        {
            // Fallback product list if any DB error occurs
            return await _context.Products
                .Where(p => p.Status == ProductStatus.Active)
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .Take(limit)
                .ToListAsync();
        }
    }

    public async Task<List<Product>> GetFrequentlyBoughtTogetherAsync(int productId, int limit = 4)
    {
        // Query Apriori Association Rules
        var consequentProductIds = await _context.AssociationRules
            .Where(r => r.AntecedentProductId == productId && r.Lift > 1.0)
            .OrderByDescending(r => r.Confidence)
            .Select(r => r.ConsequentProductId)
            .Take(limit)
            .ToListAsync();

        if (consequentProductIds.Any())
        {
            return await _context.Products
                .Where(p => consequentProductIds.Contains(p.ProductId) && p.Status == ProductStatus.Active && p.Variants.Any(v => v.StockQuantity > 0))
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .ToListAsync();
        }

        // Fallback: Same category products
        var product = await _context.Products.FindAsync(productId);
        if (product == null) return new List<Product>();

        return await _context.Products
            .Where(p => p.CategoryId == product.CategoryId && p.ProductId != productId && p.Status == ProductStatus.Active && p.Variants.Any(v => v.StockQuantity > 0))
            .Include(p => p.Images)
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .Take(limit)
            .ToListAsync();
    }
}
