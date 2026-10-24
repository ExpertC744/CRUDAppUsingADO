using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace CRUDAppUsingADO.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _log;

    public EmailService(IConfiguration config, ILogger<EmailService> log)
    {
        _config = config;
        _log = log;
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string fullName)
    {
        var message = BuildMessage(toEmail,
            "Welcome to Auth System!",
            $"<h2>Hi {fullName},</h2><p>Your account has been created successfully. Welcome aboard!</p>");
        await SendAsync(message);
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetLink)
    {
        var message = BuildMessage(toEmail,
            "Reset Your Password",
            $@"<h2>Hi {fullName},</h2>
               <p>We received a request to reset your password. Click below to proceed:</p>
               <p><a href='{resetLink}' style='background:#2563eb;color:#fff;padding:12px 24px;
                  border-radius:8px;text-decoration:none;'>Reset Password</a></p>
               <p>This link expires in 30 minutes. If you didn't request this, ignore this email.</p>");
        await SendAsync(message);
    }

    private MimeMessage BuildMessage(string toEmail, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_config["Smtp:FromName"], _config["Smtp:Username"]));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();
        return message;
    }

    private async Task SendAsync(MimeMessage message)
    {
        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(
                _config["Smtp:Host"],
                int.Parse(_config["Smtp:Port"]!),
                SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_config["Smtp:Username"], _config["Smtp:Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            // Don't let email failures break the auth flow — log and continue
            _log.LogError(ex, "Failed to send email to {To}", message.To);
        }
    }
}