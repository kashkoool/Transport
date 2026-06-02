using System.Net;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Infrastructure.Email;

/// <summary>Builds the (subject + HTML) for each transactional email. Minimal, inline HTML.</summary>
internal static class EmailTemplates
{
    public static EmailMessage Verification(string toEmail, string verifyUrl) => new(
        toEmail,
        "Verify your email — TPX Travel",
        Wrap($"""
            <h2>Welcome to TPX Travel</h2>
            <p>Confirm your email address to finish setting up your account:</p>
            <p><a href="{Attr(verifyUrl)}">Verify my email</a></p>
            <p>If you didn't create an account, you can ignore this message.</p>
            """));

    public static EmailMessage PasswordReset(string toEmail, string resetUrl) => new(
        toEmail,
        "Reset your password — TPX Travel",
        Wrap($"""
            <h2>Reset your password</h2>
            <p>We received a request to reset your password. This link expires shortly:</p>
            <p><a href="{Attr(resetUrl)}">Choose a new password</a></p>
            <p>If you didn't request this, you can safely ignore it — your password won't change.</p>
            """));

    public static EmailMessage BookingConfirmed(string toEmail, string reference, string ticketUrl) => new(
        toEmail,
        $"Booking confirmed ({reference}) — TPX Travel",
        Wrap($"""
            <h2>Your booking is confirmed</h2>
            <p>Reference <strong>{Html(reference)}</strong> is paid and confirmed.</p>
            <p><a href="{Attr(ticketUrl)}">View your ticket</a></p>
            <p>Safe travels!</p>
            """));

    private static string Wrap(string inner) =>
        $"""<div style="font-family:system-ui,Segoe UI,Arial,sans-serif;max-width:520px;margin:auto">{inner}</div>""";

    // HTML-encode interpolated values so a crafted reference/URL can't inject markup.
    private static string Html(string value) => WebUtility.HtmlEncode(value);
    private static string Attr(string url) => WebUtility.HtmlEncode(url);
}
