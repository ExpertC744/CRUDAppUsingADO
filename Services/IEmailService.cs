namespace CRUDAppUsingADO.Services;

public interface IEmailService
{
    Task SendWelcomeEmailAsync(string toEmail, string fullName);
    Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetLink);
}