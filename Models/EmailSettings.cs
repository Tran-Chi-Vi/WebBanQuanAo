namespace WEBBANQUANAO.Models;

public class EmailSettings
{
    public string SmtpServer { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SenderEmail { get; set; } = "tranchivi29102005@gmail.com";
    public string SenderName { get; set; } = "FASHION STORE";
    public string Password { get; set; } = "";
    public string ApiKey { get; set; } = "";
}
