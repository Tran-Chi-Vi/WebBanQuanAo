using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class BehaviorTrackerController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public BehaviorTrackerController(ApplicationDbContext context)
    {
        _context = context;
    }

    public class TrackLogDto
    {
        public string? SessionId { get; set; }
        public string? DeviceType { get; set; }
        public string? PageUrl { get; set; }
        public string ActionType { get; set; } = "View";
        public int? ProductId { get; set; }
        public string? SearchQuery { get; set; }
        public double DwellTimeSeconds { get; set; } = 0;
        public bool IsRageClick { get; set; } = false;
        public string? RecommendationSource { get; set; }
        public string? RecommendationBlockId { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Log([FromBody] TrackLogDto dto)
    {
        if (dto == null) return BadRequest();

        // Extract IP address from HttpContext
        string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        if (HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            ipAddress = forwardedFor.ToString().Split(',')[0].Trim();
        }

        // Get authenticated User ID if logged in
        int? userId = null;
        var uIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(uIdStr, out int parsedUserId) && parsedUserId > 0)
        {
            userId = parsedUserId;
        }

        // Parse BehaviorActionType Enum
        BehaviorActionType actionEnum = BehaviorActionType.View;
        if (Enum.TryParse<BehaviorActionType>(dto.ActionType, true, out var parsedAction))
        {
            actionEnum = parsedAction;
        }

        // Create Behavior Log entry
        var log = new UserBehaviorLog
        {
            UserId = userId,
            SessionId = string.IsNullOrEmpty(dto.SessionId) ? $"sid_anon_{Guid.NewGuid():N}" : dto.SessionId,
            IpAddress = ipAddress,
            DeviceType = string.IsNullOrEmpty(dto.DeviceType) ? "Desktop" : dto.DeviceType,
            PageUrl = dto.PageUrl,
            ProductId = dto.ProductId > 0 ? dto.ProductId : null,
            ActionType = actionEnum,
            SearchQuery = string.IsNullOrWhiteSpace(dto.SearchQuery) ? null : dto.SearchQuery.Trim(),
            DwellTimeSeconds = Math.Max(0, dto.DwellTimeSeconds),
            IsRageClick = dto.IsRageClick || actionEnum == BehaviorActionType.RageClick,
            RecommendationSource = dto.RecommendationSource,
            RecommendationBlockId = dto.RecommendationBlockId,
            Timestamp = DateTime.Now
        };

        _context.UserBehaviorLogs.Add(log);
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }
}
