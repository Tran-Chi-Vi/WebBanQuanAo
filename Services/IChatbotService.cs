namespace WEBBANQUANAO.Services;

public interface IChatbotService
{
    Task<(string reply, object? data)> ProcessUserMessageAsync(string userMessage, int userId);
}
