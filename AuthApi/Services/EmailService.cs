using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuthApi.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = false)
    {
        var smtpSettings = _configuration.GetSection("Smtp");
        var host = smtpSettings["Host"];
        var port = int.Parse(smtpSettings["Port"] ?? "587");
        var username = smtpSettings["Username"];
        var password = smtpSettings["Password"];
        var fromEmail = smtpSettings["FromEmail"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(fromEmail))
        {
            _logger.LogError("Invalid SMTP configuration. Host: {Host}, Username: {Username}, Password: {Password}, FromEmail: {FromEmail}",
                host, username, password, fromEmail);
            throw new InvalidOperationException("SMTP configuration is missing or invalid.");
        }

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(username, password),
            EnableSsl = true
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(fromEmail, "CURA Support"),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml 
        };
        mailMessage.To.Add(toEmail);

        try
        {
            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail}. Details: {Message}", toEmail, ex.Message);
            throw new InvalidOperationException($"Failed to send email. Details: {ex.Message}", ex);
        }
    }
}