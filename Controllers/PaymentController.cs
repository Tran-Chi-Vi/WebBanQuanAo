using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Controllers;

[Authorize]
public class PaymentController : Controller
{
    private readonly ApplicationDbContext _context;

    public PaymentController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> ProcessPayment(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Payment)
            .Include(o => o.User)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Variant)
                    .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null || order.Payment == null)
        {
            return NotFound();
        }

        if (order.Payment.Status == PaymentStatus.Success)
        {
            TempData["InfoMessage"] = "Đơn hàng này đã được thanh toán.";
            return RedirectToAction("Success", "Order", new { id = orderId });
        }

        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(int orderId)
    {
        var payment = await _context.Payments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.OrderId == orderId);

        if (payment == null) return NotFound();

        payment.Status = PaymentStatus.Success;
        payment.PaidAt = DateTime.Now;

        // Cập nhật trạng thái đơn hàng sang Chờ giao hàng / Đã thanh toán
        payment.Order.Status = OrderStatus.Shipping;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Thanh toán thành công qua cổng PayOS / QR Code!";
        return RedirectToAction("Success", "Order", new { id = orderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelPayment(int orderId)
    {
        var payment = await _context.Payments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.OrderId == orderId);

        if (payment == null) return NotFound();

        payment.Status = PaymentStatus.Failed;
        await _context.SaveChangesAsync();

        TempData["ErrorMessage"] = "Giao dịch thanh toán không hoàn tất.";
        return RedirectToAction("Details", "Order", new { id = orderId });
    }
}
