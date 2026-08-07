using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;
using WEBBANQUANAO.Models.ViewModels;

namespace WEBBANQUANAO.Controllers.Admin;

[Area("Admin")]
[Route("sys-admin-management/[controller]/[action]")]
[Authorize(Roles = "Admin")]
public class PromotionController : Controller
{
    private readonly ApplicationDbContext _context;

    public PromotionController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var promotions = await _context.Promotions
            .Include(p => p.Orders)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();

        return View(promotions);
    }

    [HttpGet]
    public IActionResult Create()
    {
        var model = new AdminPromotionFormViewModel
        {
            StartDate = DateTime.Now,
            EndDate = DateTime.Now.AddDays(30)
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminPromotionFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string code = model.Code.Trim().ToUpper();

        if (await _context.Promotions.AnyAsync(p => p.Code == code))
        {
            ModelState.AddModelError("Code", $"Mã khuyến mãi '{code}' đã tồn tại.");
            return View(model);
        }

        if (model.EndDate <= model.StartDate)
        {
            ModelState.AddModelError("EndDate", "Ngày kết thúc phải lớn hơn ngày bắt đầu.");
            return View(model);
        }

        var promotion = new Promotion
        {
            Code = code,
            DiscountType = model.DiscountType,
            DiscountValue = model.DiscountValue,
            MinOrderValue = model.MinOrderValue,
            StartDate = model.StartDate,
            EndDate = model.EndDate
        };

        _context.Promotions.Add(promotion);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Thêm mã khuyến mãi '{promotion.Code}' thành công!";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var promo = await _context.Promotions.FindAsync(id);
        if (promo == null) return NotFound();

        var model = new AdminPromotionFormViewModel
        {
            PromotionId = promo.PromotionId,
            Code = promo.Code,
            DiscountType = promo.DiscountType,
            DiscountValue = promo.DiscountValue,
            MinOrderValue = promo.MinOrderValue,
            StartDate = promo.StartDate,
            EndDate = promo.EndDate
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminPromotionFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var promo = await _context.Promotions.FindAsync(model.PromotionId);
        if (promo == null) return NotFound();

        string code = model.Code.Trim().ToUpper();

        if (await _context.Promotions.AnyAsync(p => p.Code == code && p.PromotionId != model.PromotionId))
        {
            ModelState.AddModelError("Code", $"Mã khuyến mãi '{code}' đã tồn tại.");
            return View(model);
        }

        if (model.EndDate <= model.StartDate)
        {
            ModelState.AddModelError("EndDate", "Ngày kết thúc phải lớn hơn ngày bắt đầu.");
            return View(model);
        }

        promo.Code = code;
        promo.DiscountType = model.DiscountType;
        promo.DiscountValue = model.DiscountValue;
        promo.MinOrderValue = model.MinOrderValue;
        promo.StartDate = model.StartDate;
        promo.EndDate = model.EndDate;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Cập nhật mã khuyến mãi '{promo.Code}' thành công!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var promo = await _context.Promotions
            .Include(p => p.Orders)
            .FirstOrDefaultAsync(p => p.PromotionId == id);

        if (promo == null) return NotFound();

        if (promo.Orders.Any())
        {
            TempData["ErrorMessage"] = $"Không thể xóa mã '{promo.Code}' vì đã có đơn hàng sử dụng mã này.";
            return RedirectToAction("Index");
        }

        _context.Promotions.Remove(promo);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Xóa mã khuyến mãi '{promo.Code}' thành công!";
        return RedirectToAction("Index");
    }
}
