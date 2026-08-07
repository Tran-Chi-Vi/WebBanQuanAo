using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Controllers.Admin;

[Area("Admin")]
[Route("sys-admin-management/[controller]/[action]")]
[Authorize(Roles = "Admin")]
public class AnalyticsController : Controller
{
    private readonly ApplicationDbContext _context;

    public AnalyticsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var now = DateTime.Now;

        var userRFM = await _context.Users
            .Include(u => u.Orders)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.CreatedAt,
                OrderCount = u.Orders.Count(o => o.Status != OrderStatus.Cancelled),
                TotalMonetary = u.Orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount),
                LastOrderDate = u.Orders.Where(o => o.Status != OrderStatus.Cancelled).OrderByDescending(o => o.OrderDate).Select(o => (DateTime?)o.OrderDate).FirstOrDefault()
            })
            .ToListAsync();

        var customerSegmentData = userRFM.Select(u =>
        {
            int recencyDays = u.LastOrderDate.HasValue ? (int)(now - u.LastOrderDate.Value).TotalDays : (int)(now - u.CreatedAt).TotalDays;
            decimal monetary = u.TotalMonetary;
            int frequency = u.OrderCount;

            string segment = "Khách Hàng Mới";
            string badgeColor = "info";

            if (monetary >= 5000000 || frequency >= 5)
            {
                segment = "Khách VIP";
                badgeColor = "success";
            }
            else if (recencyDays > 45 || (recencyDays > 30 && frequency <= 1))
            {
                segment = "Nguy Cơ Rời Bỏ (Churn Risk)";
                badgeColor = "danger";
            }
            else if (frequency >= 2 && monetary >= 1000000)
            {
                segment = "Khách Hàng Trung Thành";
                badgeColor = "warning";
            }

            return new
            {
                u.UserId,
                u.FullName,
                u.Email,
                RecencyDays = recencyDays,
                Frequency = frequency,
                Monetary = monetary,
                Segment = segment,
                BadgeColor = badgeColor
            };
        }).ToList();

        ViewBag.CustomerSegments = customerSegmentData;
        ViewBag.TotalCustomers = customerSegmentData.Count;
        ViewBag.VipCount = customerSegmentData.Count(c => c.Segment == "Khách VIP");
        ViewBag.ChurnRiskCount = customerSegmentData.Count(c => c.Segment == "Nguy Cơ Rời Bỏ (Churn Risk)");

        return View();
    }
}
