using System.Net;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Infrastructure.Email;

/// <summary>
/// Builds the (subject + HTML) for each transactional email. Minimal, inline HTML.
/// Bilingual: an Arabic message is produced when <c>lang == "ar"</c>; anything else (including
/// null/unknown) degrades to English. Arabic bodies are wrapped RTL.
/// </summary>
internal static class EmailTemplates
{
    public static EmailMessage Verification(string toEmail, string verifyUrl, string? lang = null) =>
        IsArabic(lang)
            ? new(toEmail,
                "تأكيد بريدك الإلكتروني — TPX Travel",
                Wrap("ar", $"""
                    <h2>مرحبًا بك في TPX Travel</h2>
                    <p>يرجى تأكيد عنوان بريدك الإلكتروني لإكمال إعداد حسابك:</p>
                    <p><a href="{Attr(verifyUrl)}">تأكيد بريدي الإلكتروني</a></p>
                    <p>إذا لم تُنشئ حسابًا، يمكنك تجاهل هذه الرسالة.</p>
                    """))
            : new(toEmail,
                "Verify your email — TPX Travel",
                Wrap("en", $"""
                    <h2>Welcome to TPX Travel</h2>
                    <p>Confirm your email address to finish setting up your account:</p>
                    <p><a href="{Attr(verifyUrl)}">Verify my email</a></p>
                    <p>If you didn't create an account, you can ignore this message.</p>
                    """));

    public static EmailMessage PasswordReset(string toEmail, string resetUrl, string? lang = null) =>
        IsArabic(lang)
            ? new(toEmail,
                "إعادة تعيين كلمة المرور — TPX Travel",
                Wrap("ar", $"""
                    <h2>إعادة تعيين كلمة المرور</h2>
                    <p>تلقّينا طلبًا لإعادة تعيين كلمة مرورك. تنتهي صلاحية هذا الرابط قريبًا:</p>
                    <p><a href="{Attr(resetUrl)}">اختيار كلمة مرور جديدة</a></p>
                    <p>إذا لم تطلب ذلك، يمكنك تجاهل هذه الرسالة بأمان — لن تتغيّر كلمة مرورك.</p>
                    """))
            : new(toEmail,
                "Reset your password — TPX Travel",
                Wrap("en", $"""
                    <h2>Reset your password</h2>
                    <p>We received a request to reset your password. This link expires shortly:</p>
                    <p><a href="{Attr(resetUrl)}">Choose a new password</a></p>
                    <p>If you didn't request this, you can safely ignore it — your password won't change.</p>
                    """));

    public static EmailMessage BookingConfirmed(string toEmail, string reference, string ticketUrl, string? lang = null) =>
        IsArabic(lang)
            ? new(toEmail,
                $"تم تأكيد الحجز ({reference}) — TPX Travel",
                Wrap("ar", $"""
                    <h2>تم تأكيد حجزك</h2>
                    <p>الحجز رقم <strong>{Html(reference)}</strong> مدفوع ومؤكَّد.</p>
                    <p><a href="{Attr(ticketUrl)}">عرض تذكرتك</a></p>
                    <p>رحلة سعيدة!</p>
                    """))
            : new(toEmail,
                $"Booking confirmed ({reference}) — TPX Travel",
                Wrap("en", $"""
                    <h2>Your booking is confirmed</h2>
                    <p>Reference <strong>{Html(reference)}</strong> is paid and confirmed.</p>
                    <p><a href="{Attr(ticketUrl)}">View your ticket</a></p>
                    <p>Safe travels!</p>
                    """));

    // Only "ar" selects Arabic; null/unknown/anything else stays English.
    private static bool IsArabic(string? lang) => string.Equals(lang, "ar", StringComparison.OrdinalIgnoreCase);

    private static string Wrap(string lang, string inner)
    {
        var dir = lang == "ar" ? "rtl" : "ltr";
        return $"""<div lang="{lang}" dir="{dir}" style="font-family:system-ui,Segoe UI,Arial,sans-serif;max-width:520px;margin:auto;text-align:{(dir == "rtl" ? "right" : "left")}">{inner}</div>""";
    }

    // HTML-encode interpolated values so a crafted reference/URL can't inject markup.
    private static string Html(string value) => WebUtility.HtmlEncode(value);
    private static string Attr(string url) => WebUtility.HtmlEncode(url);
}
