using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using WEBBANQUANAO.Services;

namespace WEBBANQUANAO.Controllers;

public class ChatbotController : Controller
{
    private readonly IChatbotService _chatbotService;

    public ChatbotController(IChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] ChatMessageRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Message))
        {
            return Json(new { reply = "Xin chào! Bạn cần hỗ trợ gì về các sản phẩm thời trang hôm nay?" });
        }

        int userId = GetCurrentUserId();

        // Gọi ChatbotService đã được huấn luyện tự động truy vấn CSDL SQL Server (RAG Data Engine)
        var (reply, data) = await _chatbotService.ProcessUserMessageAsync(req.Message, userId);

        return Json(new { reply, data });
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
    }

    public class ChatMessageRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}
