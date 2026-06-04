namespace TransportPlatform.Infrastructure.Email;

/// <summary>Masks an email address for safe logging (j***@example.com) — never log it raw.</summary>
internal static class EmailMask
{
    public static string Mask(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "(none)";
        var at = email.IndexOf('@');
        if (at <= 0)
            return "***";
        var local = email[..at];
        var domain = email[(at + 1)..];
        var shown = local.Length <= 1 ? local : local[..1];
        return $"{shown}***@{domain}";
    }
}
