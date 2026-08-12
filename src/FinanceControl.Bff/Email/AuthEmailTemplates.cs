using System.Net;

namespace FinanceControl.Bff.Email;

public static class AuthEmailTemplates
{
    public static string Confirmation(string displayName, string confirmationLink) => Layout(
        "Confirme seu e-mail",
        $"Olá, {WebUtility.HtmlEncode(displayName)}!",
        "Confirme seu endereço de e-mail para ativar sua conta e começar a usar o Finance Control.",
        "Confirmar e-mail",
        confirmationLink,
        "Este link expira em 2 horas. Se você não criou esta conta, ignore esta mensagem.");

    public static string PasswordReset(string displayName, string resetLink) => Layout(
        "Redefina sua senha",
        $"Olá, {WebUtility.HtmlEncode(displayName)}!",
        "Recebemos uma solicitação para redefinir a senha da sua conta no Finance Control.",
        "Criar nova senha",
        resetLink,
        "Este link expira em 2 horas. Se você não solicitou a alteração, ignore esta mensagem.");

    public static string EmailChange(string displayName, string newEmail, string confirmationLink) => Layout(
        "Confirme seu novo e-mail",
        $"Olá, {WebUtility.HtmlEncode(displayName)}!",
        $"Confirme que {newEmail} será o novo endereço de acesso à sua conta do Finance Control.",
        "Confirmar novo e-mail",
        confirmationLink,
        "Este link expira em 2 horas. Se você não solicitou a alteração, não confirme e mantenha sua senha protegida.");

    private static string Layout(
        string preview,
        string greeting,
        string message,
        string buttonLabel,
        string link,
        string footer)
    {
        var safeLink = WebUtility.HtmlEncode(link);
        return $$"""
            <!doctype html>
            <html lang="pt-BR">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width"></head>
            <body style="margin:0;background:#f3f6f4;font-family:Arial,sans-serif;color:#17211b">
              <span style="display:none;max-height:0;overflow:hidden">{{WebUtility.HtmlEncode(preview)}}</span>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="padding:32px 16px;background:#f3f6f4">
                <tr><td align="center">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#fff;border:1px solid #dce5df;border-radius:20px;overflow:hidden">
                    <tr><td style="padding:28px 32px;background:#153d2d;color:#fff;font-size:22px;font-weight:700">Finance <span style="color:#6dde9b">Control</span></td></tr>
                    <tr><td style="padding:36px 32px">
                      <h1 style="margin:0 0 16px;font-size:26px">{{greeting}}</h1>
                      <p style="margin:0 0 28px;line-height:1.6;color:#526159">{{WebUtility.HtmlEncode(message)}}</p>
                      <a href="{{safeLink}}" style="display:inline-block;padding:14px 22px;border-radius:10px;background:#1f8f5f;color:#fff;text-decoration:none;font-weight:700">{{WebUtility.HtmlEncode(buttonLabel)}}</a>
                      <p style="margin:28px 0 8px;font-size:13px;color:#6a776f">Se o botão não funcionar, copie este endereço:</p>
                      <p style="margin:0;word-break:break-all;font-size:12px;color:#1f8f5f">{{safeLink}}</p>
                    </td></tr>
                    <tr><td style="padding:20px 32px;background:#f7faf8;font-size:12px;line-height:1.5;color:#718078">{{WebUtility.HtmlEncode(footer)}}</td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
