using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Retalon.Models.Configuration;
using Retalon.Services.Interfaces;

namespace Retalon.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;

    public EmailService(IOptions<EmailSettings> emailOptions)
    {
        _emailSettings = emailOptions.Value;
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_emailSettings.SmtpServer))
            throw new InvalidOperationException("SMTP server is not configured.");

        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress(
                _emailSettings.FromName,
                _emailSettings.FromEmail));

        message.To.Add(
            MailboxAddress.Parse(toEmail));

        message.Subject = subject;

        message.Body = new TextPart("plain")
        {
            Text = body
        };

        using var client = new SmtpClient();

        var secureSocketOption = _emailSettings.EnableSsl
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(
            _emailSettings.SmtpServer,
            _emailSettings.SmtpPort,
            secureSocketOption,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_emailSettings.Username))
        {
            await client.AuthenticateAsync(
                _emailSettings.Username,
                _emailSettings.Password,
                cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);

        await client.DisconnectAsync(true, cancellationToken);
    }
}