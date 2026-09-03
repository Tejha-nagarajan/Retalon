namespace Retalon.DTOs.Notifications;

public class SendTestEmailRequestDto
{
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = "Retalon Test Email";
    public string Body { get; set; } = "This is a test email from Retalon.";
}