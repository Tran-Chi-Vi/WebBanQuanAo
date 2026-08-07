using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace WEBBANQUANAO.Services;

public interface IAprioriService
{
    /// <summary>Chạy định kỳ (VD: job mỗi đêm) để tìm frequent itemsets và sinh luật kết hợp.</summary>
    Task RunAprioriJobAsync(double minSupport = 0.01, double minConfidence = 0.2);

    /// <summary>Lấy danh sách sản phẩm "thường được mua cùng" theo Lift cao nhất.</summary>
    Task<List<AssociationRule>> GetRecommendationsAsync(int productId, int topN = 5);
}

public class AprioriService : IAprioriService
{
    private readonly ApplicationDbContext _context;

    public AprioriService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AssociationRule>> GetRecommendationsAsync(int productId, int topN = 5)
    {
        return await _context.AssociationRules
            .Where(r => r.AntecedentProductId == productId)
            .OrderByDescending(r => r.Lift)
            .Take(topN)
            .Include(r => r.ConsequentProduct)
            .ToListAsync();
    }

    public async Task RunAprioriJobAsync(double minSupport = 0.01, double minConfidence = 0.2)
    {
        // 1. Nhóm OrderDetail theo OrderId để biết mỗi đơn hàng gồm những ProductId nào
        var orderProductGroups = await _context.OrderDetails
            .Include(od => od.Variant)
            .Select(od => new { od.OrderId, ProductId = od.Variant.ProductId })
            .Distinct()
            .ToListAsync();

        var transactions = orderProductGroups
            .GroupBy(x => x.OrderId)
            .Select(g => g.Select(x => x.ProductId).ToHashSet())
            .Where(t => t.Count > 1)
            .ToList();

        int totalTransactions = transactions.Count;
        if (totalTransactions == 0) return;

        // 2. Đếm tần suất xuất hiện của từng cặp sản phẩm (2-itemsets)
        var pairCounts = new Dictionary<(int, int), int>();
        var singleCounts = new Dictionary<int, int>();

        foreach (var t in transactions)
        {
            foreach (var p in t)
                singleCounts[p] = singleCounts.GetValueOrDefault(p) + 1;

            var items = t.ToList();
            for (int i = 0; i < items.Count; i++)
                for (int j = 0; j < items.Count; j++)
                {
                    if (i == j) continue;
                    var key = (items[i], items[j]); // A -> B (có hướng)
                    pairCounts[key] = pairCounts.GetValueOrDefault(key) + 1;
                }
        }

        // 3. Tính Support, Confidence, Lift và lưu luật đạt ngưỡng
        var newRules = new List<AssociationRule>();
        foreach (var ((a, b), countAB) in pairCounts)
        {
            double support = (double)countAB / totalTransactions;
            if (support < minSupport) continue;

            double confidence = (double)countAB / singleCounts[a];
            if (confidence < minConfidence) continue;

            double supportB = (double)singleCounts[b] / totalTransactions;
            double lift = supportB == 0 ? 0 : confidence / supportB;

            newRules.Add(new AssociationRule
            {
                AntecedentProductId = a,
                ConsequentProductId = b,
                Support = support,
                Confidence = confidence,
                Lift = lift,
                UpdatedAt = DateTime.Now
            });
        }

        // 4. Xóa luật cũ và ghi lại (không tính lại mỗi request, chỉ chạy job định kỳ)
        _context.AssociationRules.RemoveRange(_context.AssociationRules);
        await _context.AssociationRules.AddRangeAsync(newRules);
        await _context.SaveChangesAsync();
    }
}
