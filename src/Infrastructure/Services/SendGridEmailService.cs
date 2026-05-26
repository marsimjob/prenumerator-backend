using Application.Common.Interfaces;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Infrastructure.Services;

public class SendGridEmailService(string apiKey, string fromEmail) : IEmailService
{
    private readonly string _fromName = "Prenumerator";

    public async Task SendVerificationCodeAsync(string toEmail, string code, CancellationToken ct = default)
    {
        var client  = new SendGridClient(apiKey);
        var from    = new EmailAddress(fromEmail, _fromName);
        var to      = new EmailAddress(toEmail);
        var subject = "Your Prenumerator verification code";
        var text    = $"Your verification code is: {code}\n\nIt expires in 24 hours.";
        var html    = $"<p>Your verification code is:</p><h2 style=\"letter-spacing:4px\">{code}</h2><p>It expires in 24 hours.</p>";
        var msg     = MailHelper.CreateSingleEmail(from, to, subject, text, html);
        await client.SendEmailAsync(msg, ct);
    }

    public async Task SendNewPasswordAsync(string toEmail, string newPassword, CancellationToken ct = default)
    {
        var client  = new SendGridClient(apiKey);
        var from    = new EmailAddress(fromEmail, _fromName);
        var to      = new EmailAddress(toEmail);
        var subject = "Your new Prenumerator password";
        var text    = $"Your new password is: {newPassword}\n\nYou can change it after signing in.";
        var html    = $"<p>Your new password is:</p><h2 style=\"font-family:monospace\">{newPassword}</h2><p>You can change it after signing in.</p>";
        var msg     = MailHelper.CreateSingleEmail(from, to, subject, text, html);
        await client.SendEmailAsync(msg, ct);
    }
}
