using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Services;

namespace WEBBANQUANAO.Controllers.Admin;

[Area("Admin")]
[Route("sys-admin-management/[controller]/[action]")]
[Authorize(Roles = "Admin")]
public class AprioriController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAprioriService _aprioriService;

    public AprioriController(ApplicationDbContext context, IAprioriService aprioriService)
    {
        _context = context;
        _aprioriService = aprioriService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var rules = await _context.AssociationRules
            .Include(r => r.AntecedentProduct)
            .Include(r => r.ConsequentProduct)
            .OrderByDescending(r => r.Confidence)
            .ThenByDescending(r => r.Lift)
            .ToListAsync();

        ViewBag.TotalOrdersAnalyzed = await _context.Orders.CountAsync(o => o.Status == Models.Entities.OrderStatus.Completed || o.Status == Models.Entities.OrderStatus.Pending);
        ViewBag.LastMinedDate = rules.FirstOrDefault()?.UpdatedAt;

        return View(rules);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunMining()
    {
        try
        {
            await _aprioriService.RunAprioriJobAsync();
            TempData["SuccessMessage"] = "Đã khai phá và cập nhật lại toàn bộ Luật Khai Thắc Tập Phổ Biến (Apriori Association Rules) thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Có lỗi xảy ra khi chạy thuật toán Apriori: {ex.Message}";
        }

        return RedirectToAction("Index");
    }
}
