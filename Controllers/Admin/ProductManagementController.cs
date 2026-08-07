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
public class ProductManagementController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProductManagementController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchQuery,
        int? categoryId,
        int? brandId,
        ProductStatus? status,
        int page = 1)
    {
        int pageSize = 10;
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string keyword = searchQuery.Trim().ToLower();
            query = query.Where(p => p.ProductName.ToLower().Contains(keyword));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (brandId.HasValue)
        {
            query = query.Where(p => p.BrandId == brandId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var products = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Categories = await _context.Categories.ToListAsync();
        ViewBag.Brands = await _context.Brands.ToListAsync();
        ViewBag.SearchQuery = searchQuery;
        ViewBag.CategoryId = categoryId;
        ViewBag.BrandId = brandId;
        ViewBag.Status = status;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new AdminProductFormViewModel
        {
            AvailableCategories = await _context.Categories.ToListAsync(),
            AvailableBrands = await _context.Brands.ToListAsync(),
            Variants = new List<AdminVariantViewModel>
            {
                new AdminVariantViewModel { Size = "M", Color = "Đen", SKU = $"SKU-{DateTime.Now.Ticks % 100000}-M", Price = 250000, StockQuantity = 20 }
            }
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminProductFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableCategories = await _context.Categories.ToListAsync();
            model.AvailableBrands = await _context.Brands.ToListAsync();
            return View(model);
        }

        var product = new Product
        {
            ProductName = model.ProductName,
            Description = model.Description,
            CategoryId = model.CategoryId,
            BrandId = model.BrandId,
            Gender = model.Gender,
            BasePrice = model.BasePrice,
            Status = model.Status,
            CreatedAt = DateTime.Now
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Ảnh chính
        if (!string.IsNullOrWhiteSpace(model.MainImageUrl))
        {
            _context.ProductImages.Add(new ProductImage
            {
                ProductId = product.ProductId,
                ImageUrl = model.MainImageUrl.Trim(),
                IsMain = true
            });
        }

        // Tạo biến thể ban đầu nếu có
        if (model.Variants != null && model.Variants.Any())
        {
            foreach (var v in model.Variants)
            {
                if (!string.IsNullOrWhiteSpace(v.Size) && !string.IsNullOrWhiteSpace(v.Color) && !string.IsNullOrWhiteSpace(v.SKU))
                {
                    _context.ProductVariants.Add(new ProductVariant
                    {
                        ProductId = product.ProductId,
                        Size = v.Size.Trim(),
                        Color = v.Color.Trim(),
                        SKU = v.SKU.Trim(),
                        Price = v.Price > 0 ? v.Price : model.BasePrice,
                        StockQuantity = v.StockQuantity
                    });
                }
            }
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Thêm sản phẩm mới '{product.ProductName}' thành công!";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null) return NotFound();

        var model = new AdminProductFormViewModel
        {
            ProductId = product.ProductId,
            ProductName = product.ProductName,
            Description = product.Description,
            CategoryId = product.CategoryId,
            BrandId = product.BrandId,
            Gender = product.Gender,
            BasePrice = product.BasePrice,
            Status = product.Status,
            MainImageUrl = product.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl ?? product.Images.FirstOrDefault()?.ImageUrl,
            AvailableCategories = await _context.Categories.ToListAsync(),
            AvailableBrands = await _context.Brands.ToListAsync(),
            Images = product.Images.ToList(),
            Variants = product.Variants.Select(v => new AdminVariantViewModel
            {
                VariantId = v.VariantId,
                ProductId = v.ProductId,
                Size = v.Size,
                Color = v.Color,
                SKU = v.SKU,
                Price = v.Price,
                StockQuantity = v.StockQuantity
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminProductFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableCategories = await _context.Categories.ToListAsync();
            model.AvailableBrands = await _context.Brands.ToListAsync();
            return View(model);
        }

        var product = await _context.Products.FindAsync(model.ProductId);
        if (product == null) return NotFound();

        product.ProductName = model.ProductName;
        product.Description = model.Description;
        product.CategoryId = model.CategoryId;
        product.BrandId = model.BrandId;
        product.Gender = model.Gender;
        product.BasePrice = model.BasePrice;
        product.Status = model.Status;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Cập nhật sản phẩm '{product.ProductName}' thành công!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _context.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null) return NotFound();

        // Kiểm tra xem đã có chi tiết đơn hàng nào sử dụng chưa
        var variantIds = product.Variants.Select(v => v.VariantId).ToList();
        bool hasOrders = await _context.OrderDetails.AnyAsync(od => variantIds.Contains(od.VariantId));

        if (hasOrders)
        {
            // Đổi trạng thái ngưng kinh doanh
            product.Status = ProductStatus.Discontinued;
            await _context.SaveChangesAsync();
            TempData["InfoMessage"] = $"Sản phẩm '{product.ProductName}' đã được chuyển sang trạng thái Ngưng kinh doanh do có lịch sử đơn hàng.";
        }
        else
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Xóa sản phẩm '{product.ProductName}' thành công!";
        }

        return RedirectToAction("Index");
    }

    #region Manage Variants & Images

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVariant(int productId, string size, string color, string sku, decimal price, int stockQuantity)
    {
        if (string.IsNullOrWhiteSpace(size) || string.IsNullOrWhiteSpace(color) || string.IsNullOrWhiteSpace(sku))
        {
            TempData["ErrorMessage"] = "Thông tin biến thể không đầy đủ.";
            return RedirectToAction("Edit", new { id = productId });
        }

        if (await _context.ProductVariants.AnyAsync(v => v.SKU == sku.Trim()))
        {
            TempData["ErrorMessage"] = $"Mã SKU '{sku}' đã tồn tại trong hệ thống.";
            return RedirectToAction("Edit", new { id = productId });
        }

        var variant = new ProductVariant
        {
            ProductId = productId,
            Size = size.Trim(),
            Color = color.Trim(),
            SKU = sku.Trim(),
            Price = price,
            StockQuantity = stockQuantity
        };

        _context.ProductVariants.Add(variant);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Thêm biến thể sản phẩm thành công!";
        return RedirectToAction("Edit", new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditVariant(int variantId, string size, string color, string sku, decimal price, int stockQuantity)
    {
        var variant = await _context.ProductVariants.FindAsync(variantId);
        if (variant == null) return NotFound();

        variant.Size = size.Trim();
        variant.Color = color.Trim();
        variant.SKU = sku.Trim();
        variant.Price = price;
        variant.StockQuantity = stockQuantity;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Cập nhật biến thể thành công!";
        return RedirectToAction("Edit", new { id = variant.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVariant(int variantId)
    {
        var variant = await _context.ProductVariants.FindAsync(variantId);
        if (variant == null) return NotFound();

        int productId = variant.ProductId;
        bool inOrders = await _context.OrderDetails.AnyAsync(od => od.VariantId == variantId);

        if (inOrders)
        {
            TempData["ErrorMessage"] = "Không thể xóa biến thể này vì đã có trong chi tiết đơn hàng.";
            return RedirectToAction("Edit", new { id = productId });
        }

        _context.ProductVariants.Remove(variant);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Xóa biến thể thành công!";
        return RedirectToAction("Edit", new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddImage(int productId, string imageUrl, bool isMain)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            TempData["ErrorMessage"] = "Đường dẫn hình ảnh không hợp lệ.";
            return RedirectToAction("Edit", new { id = productId });
        }

        if (isMain)
        {
            var oldMains = await _context.ProductImages.Where(i => i.ProductId == productId && i.IsMain).ToListAsync();
            foreach (var img in oldMains) img.IsMain = false;
        }

        _context.ProductImages.Add(new ProductImage
        {
            ProductId = productId,
            ImageUrl = imageUrl.Trim(),
            IsMain = isMain
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Thêm ảnh sản phẩm thành công!";
        return RedirectToAction("Edit", new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int imageId)
    {
        var image = await _context.ProductImages.FindAsync(imageId);
        if (image == null) return NotFound();

        int productId = image.ProductId;
        _context.ProductImages.Remove(image);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Xóa ảnh thành công!";
        return RedirectToAction("Edit", new { id = productId });
    }

    #endregion
}
