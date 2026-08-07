using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;
using WEBBANQUANAO.Models.ViewModels;

namespace WEBBANQUANAO.Controllers;

public class CartController : Controller
{
    private readonly ApplicationDbContext _context;
    private const string SESSION_CART_KEY = "GUEST_CART";
    private const string SESSION_PROMO_KEY = "APPLIED_PROMO_CODE";

    public CartController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cartViewModel = await GetCartViewModelAsync();
        return View(cartViewModel);
    }

    [HttpPost]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest req)
    {
        if (req == null || req.VariantId <= 0)
        {
            return Json(new { success = false, message = "Vui lòng chọn Size & Màu sắc sản phẩm trước khi thêm vào giỏ hàng!" });
        }

        var variant = await _context.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.VariantId == req.VariantId);

        if (variant == null || variant.Product.Status == ProductStatus.Discontinued)
        {
            return Json(new { success = false, message = "Sản phẩm không tồn tại hoặc đã ngừng kinh doanh." });
        }

        if (variant.StockQuantity < req.Quantity)
        {
            return Json(new { success = false, message = $"Số lượng tồn kho không đủ. Chỉ còn {variant.StockQuantity} sản phẩm." });
        }

        int userId = GetCurrentUserId();

        if (userId > 0)
        {
            var userCart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (userCart == null)
            {
                userCart = new Cart { UserId = userId };
                _context.Carts.Add(userCart);
                await _context.SaveChangesAsync();
            }

            var cartItem = userCart.Items.FirstOrDefault(i => i.VariantId == req.VariantId);
            if (cartItem != null)
            {
                if (variant.StockQuantity < cartItem.Quantity + req.Quantity)
                {
                    return Json(new { success = false, message = $"Tổng số lượng trong giỏ ({cartItem.Quantity + req.Quantity}) vượt quá tồn kho ({variant.StockQuantity})." });
                }
                cartItem.Quantity += req.Quantity;
            }
            else
            {
                userCart.Items.Add(new CartItem
                {
                    VariantId = req.VariantId,
                    Quantity = req.Quantity
                });
            }

            _context.UserBehaviorLogs.Add(new UserBehaviorLog
            {
                UserId = userId,
                ProductId = variant.ProductId,
                ActionType = BehaviorActionType.AddToCart,
                Timestamp = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }
        else
        {
            var sessionCart = GetSessionCart();
            var existing = sessionCart.FirstOrDefault(i => i.VariantId == req.VariantId);
            if (existing != null)
            {
                if (variant.StockQuantity < existing.Quantity + req.Quantity)
                {
                    return Json(new { success = false, message = $"Tổng số lượng trong giỏ hàng vượt quá tồn kho hiện có." });
                }
                existing.Quantity += req.Quantity;
            }
            else
            {
                sessionCart.Add(new SessionCartItem { VariantId = req.VariantId, Quantity = req.Quantity });
            }
            SaveSessionCart(sessionCart);
        }

        int totalCartCount = await GetCartTotalItemCountAsync();
        return Json(new { success = true, message = "Đã thêm sản phẩm vào giỏ hàng!", cartCount = totalCartCount });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateQuantity(int variantId, int quantity)
    {
        if (quantity <= 0)
        {
            return await RemoveItem(variantId);
        }

        var variant = await _context.ProductVariants.FindAsync(variantId);
        if (variant == null) return Json(new { success = false, message = "Sản phẩm không tồn tại." });

        if (variant.StockQuantity < quantity)
        {
            return Json(new { success = false, message = $"Số lượng tồn kho không đủ (chỉ còn {variant.StockQuantity})." });
        }

        int userId = GetCurrentUserId();
        if (userId > 0)
        {
            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Cart.UserId == userId && ci.VariantId == variantId);

            if (cartItem != null)
            {
                cartItem.Quantity = quantity;
                await _context.SaveChangesAsync();
            }
        }
        else
        {
            var sessionCart = GetSessionCart();
            var item = sessionCart.FirstOrDefault(i => i.VariantId == variantId);
            if (item != null)
            {
                item.Quantity = quantity;
                SaveSessionCart(sessionCart);
            }
        }

        var cartViewModel = await GetCartViewModelAsync();
        return Json(new
        {
            success = true,
            subTotal = cartViewModel.SubTotal,
            discount = cartViewModel.DiscountAmount,
            finalTotal = cartViewModel.FinalTotal,
            itemTotal = (variant.Price * quantity),
            cartCount = cartViewModel.Items.Sum(i => i.Quantity)
        });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveItem(int variantId)
    {
        int userId = GetCurrentUserId();
        if (userId > 0)
        {
            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Cart.UserId == userId && ci.VariantId == variantId);

            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
            }
        }
        else
        {
            var sessionCart = GetSessionCart();
            sessionCart.RemoveAll(i => i.VariantId == variantId);
            SaveSessionCart(sessionCart);
        }

        var cartViewModel = await GetCartViewModelAsync();
        return Json(new
        {
            success = true,
            message = "Đã xóa sản phẩm khỏi giỏ hàng",
            subTotal = cartViewModel.SubTotal,
            discount = cartViewModel.DiscountAmount,
            finalTotal = cartViewModel.FinalTotal,
            cartCount = cartViewModel.Items.Sum(i => i.Quantity)
        });
    }

    [HttpPost]
    public async Task<IActionResult> ApplyPromotion(string promoCode)
    {
        if (string.IsNullOrWhiteSpace(promoCode))
        {
            HttpContext.Session.Remove(SESSION_PROMO_KEY);
            return Json(new { success = false, message = "Vui lòng nhập mã giảm giá." });
        }

        string code = promoCode.Trim().ToUpper();
        var promotion = await _context.Promotions
            .FirstOrDefaultAsync(p => p.Code == code);

        if (promotion == null)
        {
            return Json(new { success = false, message = "Mã giảm giá không tồn tại." });
        }

        DateTime now = DateTime.Now;
        if (promotion.StartDate > now || promotion.EndDate < now)
        {
            return Json(new { success = false, message = "Mã giảm giá đã hết hạn hoặc chưa có hiệu lực." });
        }

        var cart = await GetCartViewModelAsync();
        if (cart.SubTotal < promotion.MinOrderValue)
        {
            return Json(new
            {
                success = false,
                message = $"Mã giảm giá này chỉ áp dụng cho đơn hàng từ {promotion.MinOrderValue:N0}đ trở lên."
            });
        }

        HttpContext.Session.SetString(SESSION_PROMO_KEY, code);

        return Json(new
        {
            success = true,
            message = "Áp dụng mã giảm giá thành công!",
            discount = cart.DiscountAmount,
            finalTotal = cart.FinalTotal
        });
    }

    #region Helpers

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
    }

    private async Task<CartViewModel> GetCartViewModelAsync()
    {
        int userId = GetCurrentUserId();
        var items = new List<CartItemViewModel>();

        if (userId > 0)
        {
            var userCartItems = await _context.CartItems
                .Where(ci => ci.Cart.UserId == userId)
                .Include(ci => ci.Variant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Images)
                .ToListAsync();

            items = userCartItems.Select(ci => new CartItemViewModel
            {
                CartItemId = ci.CartItemId,
                VariantId = ci.VariantId,
                ProductId = ci.Variant.ProductId,
                ProductName = ci.Variant.Product.ProductName,
                Size = ci.Variant.Size,
                Color = ci.Variant.Color,
                Price = ci.Variant.Price,
                Quantity = ci.Quantity,
                AvailableStock = ci.Variant.StockQuantity,
                ImageUrl = ci.Variant.Product.Images.FirstOrDefault()?.ImageUrl ?? "/images/no-image.png"
            }).ToList();
        }
        else
        {
            var sessionItems = GetSessionCart();
            var variantIds = sessionItems.Select(s => s.VariantId).ToList();

            var variants = await _context.ProductVariants
                .Where(v => variantIds.Contains(v.VariantId))
                .Include(v => v.Product)
                    .ThenInclude(p => p.Images)
                .ToListAsync();

            items = sessionItems.Select(s =>
            {
                var v = variants.FirstOrDefault(v => v.VariantId == s.VariantId);
                if (v == null) return null;
                return new CartItemViewModel
                {
                    CartItemId = 0,
                    VariantId = s.VariantId,
                    ProductId = v.ProductId,
                    ProductName = v.Product.ProductName,
                    Size = v.Size,
                    Color = v.Color,
                    Price = v.Price,
                    Quantity = s.Quantity,
                    AvailableStock = v.StockQuantity,
                    ImageUrl = v.Product.Images.FirstOrDefault()?.ImageUrl ?? "/images/no-image.png"
                };
            }).Where(i => i != null).Select(i => i!).ToList();
        }

        decimal subTotal = items.Sum(i => i.TotalPrice);
        string? promoCode = HttpContext.Session.GetString(SESSION_PROMO_KEY);
        decimal discount = 0;
        string? promoMsg = null;
        bool isPromoValid = false;

        if (!string.IsNullOrEmpty(promoCode))
        {
            var promo = await _context.Promotions.FirstOrDefaultAsync(p => p.Code == promoCode);
            if (promo != null && promo.StartDate <= DateTime.Now && promo.EndDate >= DateTime.Now && subTotal >= promo.MinOrderValue)
            {
                if (promo.DiscountType == DiscountType.Percentage)
                {
                    discount = (subTotal * promo.DiscountValue) / 100m;
                }
                else
                {
                    discount = promo.DiscountValue;
                }
                promoMsg = $"Đã áp dụng mã {promo.Code}";
                isPromoValid = true;
            }
            else
            {
                HttpContext.Session.Remove(SESSION_PROMO_KEY);
            }
        }

        return new CartViewModel
        {
            Items = items,
            AppliedPromoCode = promoCode,
            DiscountAmount = discount,
            PromoMessage = promoMsg,
            IsPromoValid = isPromoValid
        };
    }

    private async Task<int> GetCartTotalItemCountAsync()
    {
        int userId = GetCurrentUserId();
        if (userId > 0)
        {
            return await _context.CartItems
                .Where(ci => ci.Cart.UserId == userId)
                .SumAsync(ci => ci.Quantity);
        }
        else
        {
            return GetSessionCart().Sum(i => i.Quantity);
        }
    }

    private List<SessionCartItem> GetSessionCart()
    {
        string? json = HttpContext.Session.GetString(SESSION_CART_KEY);
        return string.IsNullOrEmpty(json) ? new List<SessionCartItem>() : JsonSerializer.Deserialize<List<SessionCartItem>>(json) ?? new List<SessionCartItem>();
    }

    private void SaveSessionCart(List<SessionCartItem> items)
    {
        HttpContext.Session.SetString(SESSION_CART_KEY, JsonSerializer.Serialize(items));
    }

    private class SessionCartItem
    {
        public int VariantId { get; set; }
        public int Quantity { get; set; }
    }

    #endregion
}
