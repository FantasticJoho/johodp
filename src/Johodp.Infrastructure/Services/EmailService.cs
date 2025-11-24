namespace Johodp.Infrastructure.Services;

using Johodp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service d'envoi d'emails (implémentation avec logging uniquement pour le développement)
/// TODO: Implémenter l'envoi réel via SMTP, SendGrid, AWS SES, etc.
/// </summary>
public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly string _baseUrl;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
        // TODO: Configurer via appsettings.json
        _baseUrl = "http://localhost:5000";
    }

    public async Task<bool> SendActivationEmailAsync(
        string email,
        string firstName,
        string lastName,
        string activationToken,
        Guid userId,
        string? tenantId = null)
    {
        var activationUrl = BuildActivationUrl(activationToken, userId, tenantId);

        var subject = "Activez votre compte";
        var body = BuildActivationEmailBody(firstName, lastName, activationUrl);

        _logger.LogInformation(
            "[EMAIL] Sending activation email to {Email} (User: {FirstName} {LastName}, UserId: {UserId}, Tenant: {TenantId})",
            email,
            firstName,
            lastName,
            userId,
            tenantId ?? "wildcard");

        _logger.LogInformation(
            "[EMAIL] Subject: {Subject}",
            subject);

        _logger.LogInformation(
            "[EMAIL] Activation URL: {ActivationUrl}",
            activationUrl);

        _logger.LogInformation(
            "[EMAIL] Body:\n{Body}",
            body);

        // TODO: Implémenter l'envoi réel
        // Exemples:
        // - SMTP: await _smtpClient.SendMailAsync(message);
        // - SendGrid: await _sendGridClient.SendEmailAsync(message);
        // - AWS SES: await _sesClient.SendEmailAsync(request);

        await Task.CompletedTask;

        _logger.LogInformation(
            "[EMAIL] ✅ Activation email logged successfully for {Email}",
            email);

        return true;
    }

    public async Task<bool> SendPasswordResetEmailAsync(
        string email,
        string firstName,
        string resetToken,
        Guid userId)
    {
        var resetUrl = $"{_baseUrl}/account/reset-password?token={Uri.EscapeDataString(resetToken)}&userId={userId}";

        var subject = "Réinitialisation de votre mot de passe";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
</head>
<body>
    <h2>Réinitialisation de mot de passe</h2>
    <p>Bonjour {firstName},</p>
    <p>Vous avez demandé la réinitialisation de votre mot de passe.</p>
    <p>Cliquez sur le lien ci-dessous pour définir un nouveau mot de passe :</p>
    <p><a href='{resetUrl}'>Réinitialiser mon mot de passe</a></p>
    <p>Ce lien expire dans 24 heures.</p>
    <p>Si vous n'avez pas demandé cette réinitialisation, ignorez ce message.</p>
    <p>Cordialement,<br>L'équipe Johodp</p>
</body>
</html>";

        _logger.LogInformation(
            "[EMAIL] Sending password reset email to {Email} (User: {FirstName}, UserId: {UserId})",
            email,
            firstName,
            userId);

        _logger.LogInformation(
            "[EMAIL] Subject: {Subject}",
            subject);

        _logger.LogInformation(
            "[EMAIL] Reset URL: {ResetUrl}",
            resetUrl);

        _logger.LogInformation(
            "[EMAIL] Body:\n{Body}",
            body);

        await Task.CompletedTask;

        _logger.LogInformation(
            "[EMAIL] ✅ Password reset email logged successfully for {Email}",
            email);

        return true;
    }

    public async Task<bool> SendWelcomeEmailAsync(
        string email,
        string firstName,
        string lastName,
        string? tenantName = null)
    {
        var subject = $"Bienvenue{(tenantName != null ? $" chez {tenantName}" : "")} !";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
</head>
<body>
    <h2>Bienvenue !</h2>
    <p>Bonjour {firstName} {lastName},</p>
    <p>Votre compte a été activé avec succès{(tenantName != null ? $" pour l'organisation {tenantName}" : "")}.</p>
    <p>Vous pouvez maintenant vous connecter et commencer à utiliser nos services.</p>
    <p>Cordialement,<br>L'équipe Johodp</p>
</body>
</html>";

        _logger.LogInformation(
            "[EMAIL] Sending welcome email to {Email} (User: {FirstName} {LastName}, Tenant: {TenantName})",
            email,
            firstName,
            lastName,
            tenantName ?? "N/A");

        _logger.LogInformation(
            "[EMAIL] Subject: {Subject}",
            subject);

        _logger.LogInformation(
            "[EMAIL] Body:\n{Body}",
            body);

        await Task.CompletedTask;

        _logger.LogInformation(
            "[EMAIL] ✅ Welcome email logged successfully for {Email}",
            email);

        return true;
    }

    public async Task<bool> SendEmailAsync(string email, string subject, string body)
    {
        _logger.LogInformation(
            "[EMAIL] Sending generic email to {Email}",
            email);

        _logger.LogInformation(
            "[EMAIL] Subject: {Subject}",
            subject);

        _logger.LogInformation(
            "[EMAIL] Body:\n{Body}",
            body);

        // TODO: Implémenter l'envoi réel
        await Task.CompletedTask;

        _logger.LogInformation(
            "[EMAIL] ✅ Generic email logged successfully for {Email}",
            email);

        return true;
    }

    private string BuildActivationUrl(string activationToken, Guid userId, string? tenantId)
    {
        var url = $"{_baseUrl}/account/activate?token={Uri.EscapeDataString(activationToken)}&userId={userId}";
        
        if (!string.IsNullOrEmpty(tenantId))
        {
            url += $"&tenant={tenantId}";
        }

        return url;
    }

    private string BuildActivationEmailBody(string firstName, string lastName, string activationUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; }}
        .button {{ display: inline-block; padding: 12px 30px; background: #667eea; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ margin-top: 20px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Activez votre compte</h1>
        </div>
        <div class='content'>
            <p>Bonjour {firstName} {lastName},</p>
            
            <p>Bienvenue ! Votre compte a été créé avec succès.</p>
            
            <p>Pour activer votre compte et définir votre mot de passe, cliquez sur le bouton ci-dessous :</p>
            
            <p style='text-align: center;'>
                <a href='{activationUrl}' class='button'>Activer mon compte</a>
            </p>
            
            <p>Ou copiez-collez ce lien dans votre navigateur :</p>
            <p style='font-size: 12px; word-break: break-all; background: #fff; padding: 10px; border-radius: 4px;'>
                {activationUrl}
            </p>
            
            <p><strong>Ce lien expire dans 24 heures.</strong></p>
            
            <p>Si vous n'avez pas demandé la création de ce compte, ignorez ce message.</p>
            
            <div class='footer'>
                <p>Cordialement,<br>L'équipe Johodp Identity Platform</p>
                <p>Cet email a été envoyé automatiquement, merci de ne pas y répondre.</p>
            </div>
        </div>
    </div>
</body>
</html>";
    }
}
