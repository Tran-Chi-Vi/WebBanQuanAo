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

[Authorize]
public class OrderController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly IAprioriService _aprioriService;
    private readonly IEmailService _emailService;
    private const string SESSION_PROMO_KEY = "APPLIED_PROMO_CODE";
    private const string SESSION_SELECTED_VARIANTS_KEY = "SELECTED_CART_VARIANT_IDS";

    public OrderController(ApplicationDbContext context, IInventoryService inventoryService, IAprioriService aprioriService, IEmailService emailService)
    {
        _context = context;
        _inventoryService = inventoryService;
        _aprioriService = aprioriService;
        _emailService = emailService;
    }

    [HttpGet]
    public async Task<IActionResult> Checkout(string? selectedVariantIds)
    {
        try
        {
            int userId = GetCurrentUserId();
            if (userId == 0)
            {
                TempData["InfoMessage"] = "Vui lòng đăng nhập tài khoản để tiến hành thanh toán đơn hàng.";
                return RedirectToAction("Login", "Account", new { returnUrl = "/Order/Checkout" });
            }

            // Save selectedVariantIds to Session if passed from Cart page
            if (!string.IsNullOrEmpty(selectedVariantIds))
            {
                HttpContext.Session.SetString(SESSION_SELECTED_VARIANTS_KEY, selectedVariantIds);
            }
            else
            {
                selectedVariantIds = HttpContext.Session.GetString(SESSION_SELECTED_VARIANTS_KEY);
            }

            // Automatically merge any guest session cart items into User DB Cart
            await MergeGuestSessionCartToUserDbCartAsync(userId);

            var cartItems = await _context.CartItems
                .Where(ci => ci.Cart != null && ci.Cart.UserId == userId && ci.Variant != null && ci.Variant.Product != null)
                .Include(ci => ci.Variant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Images)
                .ToListAsync();

            // Filter cart items by user selection if selectedVariantIds is present
            if (!string.IsNullOrEmpty(selectedVariantIds))
            {
                var idList = selectedVariantIds.Split(',')
                    .Select(id => int.TryParse(id.Trim(), out int parsed) ? parsed : 0)
                    .Where(id => id > 0)
                    .ToList();

                if (idList.Any())
                {
                    cartItems = cartItems.Where(ci => idList.Contains(ci.VariantId)).ToList();
                }
            }

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Vui lòng chọn ít nhất 1 sản phẩm trong giỏ hàng để tiến hành thanh toán.";
                return RedirectToAction("Index", "Cart");
            }

            var addresses = await _context.Addresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ToListAsync();

            var cartVM = await BuildCartViewModelAsync(userId, cartItems);

            List<Product> aprioriRecommendations = new List<Product>();
            try
            {
                var cartProductIds = cartItems
                    .Where(ci => ci.Variant != null && ci.Variant.Product != null)
                    .Select(ci => ci.Variant.ProductId)
                    .Distinct()
                    .ToList();

                foreach (var pId in cartProductIds)
                {
                    var rules = await _aprioriService.GetRecommendationsAsync(pId, topN: 4);
                    if (rules != null)
                    {
                        foreach (var rule in rules)
                        {
                            if (rule.ConsequentProduct != null &&
                                rule.ConsequentProduct.Status == ProductStatus.Active &&
                                !cartProductIds.Contains(rule.ConsequentProductId) &&
                                !aprioriRecommendations.Any(p => p.ProductId == rule.ConsequentProductId))
                            {
                                aprioriRecommendations.Add(rule.ConsequentProduct);
                            }
                        }
                    }
                }

                if (aprioriRecommendations.Count < 4)
                {
                    var existingIds = aprioriRecommendations.Select(ar => ar.ProductId).ToList();
                    var additionalProducts = await _context.Products
                        .Where(p => p.Status == ProductStatus.Active && !cartProductIds.Contains(p.ProductId) && !existingIds.Contains(p.ProductId))
                        .Include(p => p.Images)
                        .Include(p => p.Category)
                        .Include(p => p.Variants)
                        .Take(4 - aprioriRecommendations.Count)
                        .ToListAsync();

                    aprioriRecommendations.AddRange(additionalProducts);
                }

                var finalRecIds = aprioriRecommendations.Select(p => p.ProductId).Distinct().ToList();
                if (finalRecIds.Any())
                {
                    aprioriRecommendations = await _context.Products
                        .Where(p => finalRecIds.Contains(p.ProductId))
                        .Include(p => p.Images)
                        .Include(p => p.Category)
                        .Include(p => p.Variants)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Apriori Checkout error: {ex.Message}");
            }

            ViewBag.AprioriRecommendations = aprioriRecommendations;

            var viewModel = new CheckoutViewModel
            {
                Cart = cartVM,
                UserAddresses = addresses,
                SelectedAddressId = addresses.FirstOrDefault(a => a.IsDefault)?.AddressId ?? addresses.FirstOrDefault()?.AddressId
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Checkout GET Error: {ex.Message}");
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi nạp trang thanh toán. Vui lòng thử lại!";
            return RedirectToAction("Index", "Cart");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutViewModel model)
    {
        try
        {
            int userId = GetCurrentUserId();
            if (userId == 0)
            {
                TempData["InfoMessage"] = "Vui lòng đăng nhập tài khoản để tiến hành thanh toán đơn hàng.";
                return RedirectToAction("Login", "Account", new { returnUrl = "/Order/Checkout" });
            }

            // Ensure any guest session items are merged into DB
            await MergeGuestSessionCartToUserDbCartAsync(userId);

            var cartItems = await _context.CartItems
                .Where(ci => ci.Cart != null && ci.Cart.UserId == userId && ci.Variant != null && ci.Variant.Product != null)
                .Include(ci => ci.Variant)
                    .ThenInclude(v => v.Product)
                .ToListAsync();

            // Filter cart items by selectedVariantIds from Session
            string? selectedVariantIds = HttpContext.Session.GetString(SESSION_SELECTED_VARIANTS_KEY);
            if (!string.IsNullOrEmpty(selectedVariantIds))
            {
                var idList = selectedVariantIds.Split(',')
                    .Select(id => int.TryParse(id.Trim(), out int parsed) ? parsed : 0)
                    .Where(id => id > 0)
                    .ToList();

                if (idList.Any())
                {
                    cartItems = cartItems.Where(ci => idList.Contains(ci.VariantId)).ToList();
                }
            }

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Vui lòng chọn ít nhất 1 sản phẩm trong giỏ hàng để tiến hành thanh toán.";
                return RedirectToAction("Index", "Cart");
            }

            int addressId = 0;
            bool userHasSavedAddresses = await _context.Addresses.AnyAsync(a => a.UserId == userId);

            bool isEnteringNewAddress = model.CreateNewAddress || 
                !userHasSavedAddresses || 
                (model.NewAddress != null && 
                 (!string.IsNullOrWhiteSpace(model.NewAddress.RecipientName) ||
                  !string.IsNullOrWhiteSpace(model.NewAddress.Phone) ||
                  !string.IsNullOrWhiteSpace(model.NewAddress.StreetAddress) ||
                  !string.IsNullOrWhiteSpace(model.NewAddress.City)));

            if (isEnteringNewAddress && model.NewAddress != null)
            {
                if (string.IsNullOrWhiteSpace(model.NewAddress.RecipientName) ||
                    string.IsNullOrWhiteSpace(model.NewAddress.Phone) ||
                    string.IsNullOrWhiteSpace(model.NewAddress.StreetAddress) ||
                    string.IsNullOrWhiteSpace(model.NewAddress.City) ||
                    string.IsNullOrWhiteSpace(model.NewAddress.District) ||
                    string.IsNullOrWhiteSpace(model.NewAddress.Ward))
                {
                    TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ thông tin địa chỉ giao hàng mới (Tên người nhận, SĐT, Số nhà/Đường, Tỉnh/Thành, Quận/Huyện, Phường/Xã).";
                    return await RebindCheckoutViewAsync(userId, cartItems, model);
                }

                var newAddress = new Address
                {
                    UserId = userId,
                    RecipientName = model.NewAddress.RecipientName.Trim(),
                    Phone = model.NewAddress.Phone.Trim(),
                    DetailAddress = model.NewAddress.StreetAddress.Trim(),
                    Province = model.NewAddress.City.Trim(),
                    District = model.NewAddress.District.Trim(),
                    Ward = model.NewAddress.Ward.Trim(),
                    IsDefault = !userHasSavedAddresses
                };

                _context.Addresses.Add(newAddress);
                await _context.SaveChangesAsync();
                addressId = newAddress.AddressId;
            }
            else
            {
                // Fallback: If SelectedAddressId is missing or invalid, pick default or first address in DB!
                if (model.SelectedAddressId.HasValue && await _context.Addresses.AnyAsync(a => a.AddressId == model.SelectedAddressId.Value && a.UserId == userId))
                {
                    addressId = model.SelectedAddressId.Value;
                }
                else
                {
                    var defaultAddr = await _context.Addresses
                        .Where(a => a.UserId == userId)
                        .OrderByDescending(a => a.IsDefault)
                        .FirstOrDefaultAsync();

                    if (defaultAddr != null)
                    {
                        addressId = defaultAddr.AddressId;
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Vui lòng chọn hoặc nhập đầy đủ thông tin địa chỉ giao hàng hợp lệ.";
                        return await RebindCheckoutViewAsync(userId, cartItems, model);
                    }
                }
            }

            foreach (var item in cartItems)
            {
                bool stockDeducted = await _inventoryService.TryDeductStockAsync(item.VariantId, item.Quantity);
                if (!stockDeducted)
                {
                    TempData["ErrorMessage"] = $"Sản phẩm {item.Variant?.Product?.ProductName ?? "này"} ({item.Variant?.Size} - {item.Variant?.Color}) đã hết hàng hoặc không đủ số lượng.";
                    return await RebindCheckoutViewAsync(userId, cartItems, model);
                }
            }

            decimal subTotal = cartItems.Sum(i => i.Variant.Price * i.Quantity);

            decimal totalSpent = await _context.Orders
                .Where(o => o.UserId == userId && o.Status != OrderStatus.Cancelled)
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

            var tierInfo = MembershipTierHelper.CalculateTier(totalSpent);
            decimal tierDiscountAmount = (subTotal * tierInfo.DiscountPercent) / 100m;

            decimal promoDiscount = 0;
            int? promotionId = null;

            string? promoCode = HttpContext.Session.GetString(SESSION_PROMO_KEY);
            if (!string.IsNullOrEmpty(promoCode))
            {
                var promo = await _context.Promotions.FirstOrDefaultAsync(p => p.Code == promoCode);
                var currentUser = await _context.Users.FindAsync(userId);
                string currentUserEmail = currentUser?.Email ?? User.FindFirst(ClaimTypes.Email)?.Value ?? "";

                bool isRestricted = (promo?.AssignedUserId.HasValue == true || !string.IsNullOrEmpty(promo?.AllowedEmail));
                bool isUserMatch = promo?.AssignedUserId.HasValue == true && promo.AssignedUserId.Value == userId;
                bool isEmailMatch = !string.IsNullOrEmpty(currentUserEmail) && 
                                    !string.IsNullOrEmpty(promo?.AllowedEmail) && 
                                    promo.AllowedEmail.Trim().ToLower() == currentUserEmail.Trim().ToLower();

                bool isAllowed = !isRestricted || isUserMatch || isEmailMatch;

                if (promo != null && promo.StartDate <= DateTime.Now && promo.EndDate >= DateTime.Now && subTotal >= promo.MinOrderValue && isAllowed)
                {
                    promotionId = promo.PromotionId;
                    promoDiscount = promo.DiscountType == DiscountType.Percentage ? (subTotal * promo.DiscountValue) / 100m : promo.DiscountValue;
                }
                else
                {
                    HttpContext.Session.Remove(SESSION_PROMO_KEY);
                }
            }

            decimal totalDiscount = tierDiscountAmount + promoDiscount;
            decimal finalTotal = Math.Max(0, subTotal - totalDiscount);

            var orderGuid = Guid.NewGuid();
            var orderNumber = $"ORD-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}";

            var order = new Order
            {
                OrderGuid = orderGuid,
                OrderNumber = orderNumber,
                UserId = userId,
                AddressId = addressId,
                PromotionId = promotionId,
                OrderDate = DateTime.Now,
                Status = OrderStatus.Pending,
                TotalAmount = finalTotal
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            foreach (var item in cartItems)
            {
                _context.OrderDetails.Add(new OrderDetail
                {
                    OrderId = order.OrderId,
                    VariantId = item.VariantId,
                    UnitPrice = item.Variant.Price,
                    Quantity = item.Quantity
                });

                try
                {
                    _context.UserBehaviorLogs.Add(new UserBehaviorLog
                    {
                        UserId = userId,
                        ProductId = item.Variant.ProductId,
                        ActionType = BehaviorActionType.Purchase,
                        Timestamp = DateTime.Now
                    });
                }
                catch {}
            }

            string payMethod = model.PaymentMethod ?? "COD";

            var payment = new Payment
            {
                OrderId = order.OrderId,
                Amount = finalTotal,
                Status = PaymentStatus.WaitingForPayment,
                PayOSTransactionId = payMethod != "COD" ? $"PAYOS_{order.OrderGuid.ToString().Substring(0, 8).ToUpper()}" : null,
                QRCodeUrl = payMethod != "COD" ? $"https://img.vietqr.io/image/MB-0359876543-compact.png?amount={(long)finalTotal}&addInfo={order.OrderNumber}" : null
            };

            _context.Payments.Add(payment);

            _context.CartItems.RemoveRange(cartItems);
            HttpContext.Session.Remove(SESSION_PROMO_KEY);
            HttpContext.Session.Remove(SESSION_SELECTED_VARIANTS_KEY);

            await _context.SaveChangesAsync();

            // Gửi Hóa Đơn Đặt Hàng Qua Gmail (Fire & Forget Task - Phản hồi khách hàng ngay lập tức trong 50ms)
            int targetOrderId = order.OrderId;
            var serviceProvider = HttpContext.RequestServices;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await scopedEmailService.SendOrderInvoiceEmailAsync(targetOrderId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Lỗi gửi email hóa đơn nền: " + ex.ToString());
                }
            });

            if (payMethod == "PayOS" || payMethod == "QR")
            {
                return RedirectToAction("ProcessPayment", "Payment", new { orderId = order.OrderId });
            }

            TempData["SuccessMessage"] = $"Đặt hàng thành công! Bạn nhận được ưu đãi {tierInfo.TierName} giảm {tierInfo.DiscountPercent}%. Cảm ơn bạn đã mua sắm.";
            return RedirectToAction("Success", new { id = order.OrderId });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Checkout POST Error: {ex.Message}");
            TempData["ErrorMessage"] = "Có lỗi xảy ra trong quá trình đặt hàng: " + ex.Message;
            return RedirectToAction("Index", "Cart");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Success(int id)
    {
        int userId = GetCurrentUserId();
        var order = await _context.Orders
            .Include(o => o.Address)
            .Include(o => o.Payment)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Variant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        if (order == null) return NotFound();

        return View(order);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> CancelOrder(int id)
    {
        int userId = GetCurrentUserId();
        var order = await _context.Orders
            .Include(o => o.Address)
            .Include(o => o.Payment)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Variant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        if (order == null) return NotFound();

        if (order.Status != OrderStatus.Pending)
        {
            TempData["ErrorMessage"] = "Chỉ có thể hủy đơn hàng đang ở trạng thái 'Chờ xử lý'.";
            return RedirectToAction("MyOrders", "Account");
        }

        return View(order);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(int orderId, string? cancelReason)
    {
        int userId = GetCurrentUserId();
        var order = await _context.Orders
            .Include(o => o.Payment)
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);

        if (order == null) return NotFound();

        if (order.Status != OrderStatus.Pending)
        {
            TempData["ErrorMessage"] = "Đơn hàng này không thể hủy do đã được xử lý hoặc giao hàng.";
            return RedirectToAction("MyOrders", "Account");
        }

        // Cập nhật trạng thái đơn hàng -> Cancelled
        order.Status = OrderStatus.Cancelled;

        // Cập nhật trạng thái thanh toán -> Failed nếu có
        if (order.Payment != null)
        {
            order.Payment.Status = PaymentStatus.Failed;
        }

        // HOÀN TRẢ SỐ LƯỢNG TỒN KHO CHO CÁC SẢN PHẨM TRONG ĐƠN HÀNG
        foreach (var detail in order.OrderDetails)
        {
            var variant = await _context.ProductVariants.FindAsync(detail.VariantId);
            if (variant != null)
            {
                variant.StockQuantity += detail.Quantity;
            }
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã hủy đơn hàng #{order.OrderId} thành công! Số lượng sản phẩm đã được tự động hoàn lại vào kho.";
        return RedirectToAction("MyOrders", "Account");
    }

    #region Helpers

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
    }

    private async Task<CartViewModel> BuildCartViewModelAsync(int userId, List<CartItem> cartItems)
    {
        var items = cartItems.Select(ci => new CartItemViewModel
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

        decimal subTotal = items.Sum(i => i.TotalPrice);

        // Tính ưu đãi phân hạng thành viên (Tính tất cả các đơn hàng hợp lệ chưa bị hủy)
        decimal totalSpent = await _context.Orders
            .Where(o => o.UserId == userId && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        var tierInfo = MembershipTierHelper.CalculateTier(totalSpent);
        decimal tierDiscount = (subTotal * tierInfo.DiscountPercent) / 100m;

        string? promoCode = HttpContext.Session.GetString(SESSION_PROMO_KEY);
        decimal promoDiscount = 0;

        if (!string.IsNullOrEmpty(promoCode))
        {
            var promo = await _context.Promotions.FirstOrDefaultAsync(p => p.Code == promoCode);
            if (promo != null && promo.StartDate <= DateTime.Now && promo.EndDate >= DateTime.Now && subTotal >= promo.MinOrderValue)
            {
                promoDiscount = promo.DiscountType == DiscountType.Percentage ? (subTotal * promo.DiscountValue) / 100m : promo.DiscountValue;
            }
        }

        return new CartViewModel
        {
            Items = items,
            AppliedPromoCode = promoCode,
            DiscountAmount = tierDiscount + promoDiscount
        };
    }

    private async Task<IActionResult> RebindCheckoutViewAsync(int userId, List<CartItem> cartItems, CheckoutViewModel model)
    {
        model.Cart = await BuildCartViewModelAsync(userId, cartItems);
        model.UserAddresses = await _context.Addresses.Where(a => a.UserId == userId).ToListAsync();
        return View(model);
    }

    private async Task MergeGuestSessionCartToUserDbCartAsync(int userId)
    {
        if (userId <= 0) return;
        var gCartJson = HttpContext.Session.GetString("GUEST_CART");
        if (string.IsNullOrEmpty(gCartJson)) return;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(gCartJson);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
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

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.TryGetProperty("VariantId", out var vProp) && el.TryGetProperty("Quantity", out var qProp))
                    {
                        int vId = vProp.GetInt32();
                        int qty = qProp.GetInt32();

                        var variant = await _context.ProductVariants.FindAsync(vId);
                        if (variant != null && variant.StockQuantity > 0)
                        {
                            var existingItem = userCart.Items.FirstOrDefault(i => i.VariantId == vId);
                            if (existingItem != null)
                            {
                                existingItem.Quantity += qty;
                            }
                            else
                            {
                                userCart.Items.Add(new CartItem { VariantId = vId, Quantity = qty });
                            }
                        }
                    }
                }
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Merge cart error: {ex.Message}");
        }
        finally
        {
            HttpContext.Session.Remove("GUEST_CART");
        }
    }

    #endregion

    #region Public Order QR Code Tracking

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Track(Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Address)
            .Include(o => o.Promotion)
            .Include(o => o.Payment)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Variant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(o => o.OrderGuid == id);

        if (order == null)
        {
            return NotFound("Không tìm thấy thông tin đơn hàng.");
        }

        return View(order);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ShipperUpdateStatus(Guid orderGuid, string actionType)
    {
        var order = await _context.Orders
            .Include(o => o.Payment)
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.OrderGuid == orderGuid);

        if (order == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin đơn hàng.";
            return RedirectToAction("Index", "Home");
        }

        if (order.Status == OrderStatus.Completed)
        {
            TempData["InfoMessage"] = "Đơn hàng này đã được xác nhận giao thành công trước đó.";
            return RedirectToAction("Track", new { id = orderGuid });
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            TempData["ErrorMessage"] = "Đơn hàng này đã bị hủy, không thể cập nhật.";
            return RedirectToAction("Track", new { id = orderGuid });
        }

        if (actionType == "confirm_success")
        {
            order.Status = OrderStatus.Completed;
            DateTime confirmTime = DateTime.UtcNow;
            if (order.Payment != null)
            {
                order.Payment.Status = PaymentStatus.Success;
                if (!order.Payment.PaidAt.HasValue) order.Payment.PaidAt = confirmTime;
            }
            await _context.SaveChangesAsync();

            // Gửi email thông báo Giao Hàng Thành Công tới Khách Hàng (Background Task)
            int targetOrderId = order.OrderId;
            var serviceProvider = HttpContext.RequestServices;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await scopedEmailService.SendOrderCompletedEmailAsync(targetOrderId, confirmTime);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Lỗi gửi email giao hàng thành công: " + ex.ToString());
                }
            });

            TempData["SuccessMessage"] = $"🎉 Đã giao thành công đơn hàng #{order.OrderNumber}!";
            TempData["AutoRedirectExit"] = true;
        }
        else if (actionType == "fail_attempt")
        {
            order.DeliveryAttemptCount += 1;
            if (order.DeliveryAttemptCount < 3)
            {
                order.Status = OrderStatus.WaitingForCustomer; // Chuyển sang trạng thái "Chờ Nhận Hàng (Chờ Khách Xác Nhận)"
                await _context.SaveChangesAsync();

                // Gửi email cho Khách Hàng bấm xác nhận sẵn sàng nhận hàng lần tiếp theo
                int targetOrderId = order.OrderId;
                int attemptNo = order.DeliveryAttemptCount;
                var serviceProvider = HttpContext.RequestServices;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                        await scopedEmailService.SendDeliveryFailedCustomerConfirmationEmailAsync(targetOrderId, attemptNo);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Lỗi gửi email xác nhận giao lại cho khách: " + ex.ToString());
                    }
                });

                TempData["ErrorMessage"] = $"⚠️ Giao thất bại lần {order.DeliveryAttemptCount}/3. Đã gửi mail cho khách!";
                TempData["AutoRedirectExit"] = true;
            }
            else
            {
                // Giao thất bại 3 lần -> Tự động HỦY ĐƠN HÀNG & HOÀN TRẢ TỒN KHO VỀ CỬA HÀNG
                order.Status = OrderStatus.Cancelled;
                DateTime cancelTime = DateTime.UtcNow;
                if (order.Payment != null)
                {
                    order.Payment.Status = PaymentStatus.Failed;
                }

                foreach (var detail in order.OrderDetails)
                {
                    var variant = await _context.ProductVariants.FindAsync(detail.VariantId);
                    if (variant != null)
                    {
                        variant.StockQuantity += detail.Quantity;
                    }
                }

                await _context.SaveChangesAsync();

                // Gửi email thông báo Hủy Đơn Hàng tới Khách Hàng (Background Task)
                int targetOrderId = order.OrderId;
                var serviceProvider = HttpContext.RequestServices;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                        await scopedEmailService.SendOrderCancelledEmailAsync(targetOrderId, cancelTime, "Giao hàng không thành công sau 3 lần phát (Không liên lạc được / Khách từ chối nhận)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Lỗi gửi email hủy đơn hàng: " + ex.ToString());
                    }
                });

                TempData["ErrorMessage"] = $"❌ Đơn #{order.OrderNumber} đã bị hủy (thất bại 3 lần) & hoàn kho!";
                TempData["AutoRedirectExit"] = true;
            }
        }

        return RedirectToAction("Track", new { id = orderGuid });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> CustomerConfirmDelivery(Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.OrderGuid == id);

        if (order == null)
        {
            return NotFound("Không tìm thấy thông tin đơn hàng.");
        }

        if (order.Status == OrderStatus.WaitingForCustomer)
        {
            order.Status = OrderStatus.Shipping; // Khách đã bấm xác nhận -> Chuyển lại thành Đang Giao Hàng để Shipper đi giao
            await _context.SaveChangesAsync();

            // Gửi email xác nhận lại cho khách hàng
            int targetOrderId = order.OrderId;
            var serviceProvider = HttpContext.RequestServices;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await scopedEmailService.SendCustomerReDeliveryConfirmedEmailAsync(targetOrderId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Lỗi gửi email xác nhận giao lại: " + ex.ToString());
                }
            });
        }

        return View("CustomerConfirmSuccess", order);
    }

    #endregion
}
