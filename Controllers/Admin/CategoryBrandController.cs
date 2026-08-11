using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Controllers.Admin;

[Area("Admin")]
[Route("sys-admin-management/[controller]/[action]")]
[Authorize(Roles = "Admin")]
public class CategoryBrandController : Controller
{
    private readonly ApplicationDbContext _context;

    public CategoryBrandController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Categories()
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Include(c => c.ParentCategory)
            .Include(c => c.Products)
            .Include(c => c.SubCategories)
            .ToListAsync();

        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(string categoryName, int? parentCategoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            TempData["ErrorMessage"] = "Tên danh mục không được để trống.";
            return RedirectToAction("Categories");
        }

        var category = new Category
        {
            CategoryName = categoryName.Trim(),
            ParentCategoryId = parentCategoryId
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Thêm danh mục '{category.CategoryName}' thành công!";
        return RedirectToAction("Categories");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int categoryId, string categoryName, int? parentCategoryId)
    {
        var category = await _context.Categories.FindAsync(categoryId);
        if (category == null) return NotFound();

        if (string.IsNullOrWhiteSpace(categoryName))
        {
            TempData["ErrorMessage"] = "Tên danh mục không được để trống.";
            return RedirectToAction("Categories");
        }

        category.CategoryName = categoryName.Trim();
        category.ParentCategoryId = parentCategoryId != categoryId ? parentCategoryId : null;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Cập nhật danh mục '{category.CategoryName}' thành công!";
        return RedirectToAction("Categories");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.Categories
            .Include(c => c.Products)
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category == null) return NotFound();

        if (category.Products.Any() || category.SubCategories.Any())
        {
            TempData["ErrorMessage"] = $"Không thể xóa danh mục '{category.CategoryName}' vì có sản phẩm hoặc danh mục con đang thuộc về nó.";
            return RedirectToAction("Categories");
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Xóa danh mục '{category.CategoryName}' thành công!";
        return RedirectToAction("Categories");
    }

    #region Brands

    [HttpGet]
    public async Task<IActionResult> Brands()
    {
        var brands = await _context.Brands
            .Include(b => b.Products)
            .ToListAsync();

        return View(brands);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBrand(string brandName, string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(brandName))
        {
            TempData["ErrorMessage"] = "Tên thương hiệu không được để trống.";
            return RedirectToAction("Brands");
        }

        if (await _context.Brands.AnyAsync(b => b.BrandName == brandName.Trim()))
        {
            TempData["ErrorMessage"] = $"Thương hiệu '{brandName}' đã tồn tại.";
            return RedirectToAction("Brands");
        }

        var brand = new Brand
        {
            BrandName = brandName.Trim(),
            LogoUrl = logoUrl?.Trim()
        };

        _context.Brands.Add(brand);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Thêm thương hiệu '{brand.BrandName}' thành công!";
        return RedirectToAction("Brands");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBrand(int brandId, string brandName, string? logoUrl)
    {
        var brand = await _context.Brands.FindAsync(brandId);
        if (brand == null) return NotFound();

        if (string.IsNullOrWhiteSpace(brandName))
        {
            TempData["ErrorMessage"] = "Tên thương hiệu không được để trống.";
            return RedirectToAction("Brands");
        }

        brand.BrandName = brandName.Trim();
        brand.LogoUrl = logoUrl?.Trim();

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Cập nhật thương hiệu '{brand.BrandName}' thành công!";
        return RedirectToAction("Brands");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBrand(int id)
    {
        var brand = await _context.Brands
            .Include(b => b.Products)
            .FirstOrDefaultAsync(b => b.BrandId == id);

        if (brand == null) return NotFound();

        if (brand.Products.Any())
        {
            TempData["ErrorMessage"] = $"Không thể xóa thương hiệu '{brand.BrandName}' vì đang có {brand.Products.Count} sản phẩm liên kết.";
            return RedirectToAction("Brands");
        }

        _context.Brands.Remove(brand);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Xóa thương hiệu '{brand.BrandName}' thành công!";
        return RedirectToAction("Brands");
    }

    #endregion
}
