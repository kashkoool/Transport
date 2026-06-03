namespace TransportPlatform.Application.Abstractions;

/// <summary>
/// Login-policy switches bound from the <c>Auth</c> configuration section. Kept in the Application
/// layer so handlers can depend on it without referencing Infrastructure.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// When true, a user must have a confirmed email before they can sign in with a password.
    /// Off by default so existing/dev flows (and Google users, who are confirmed on link) are
    /// unaffected; turn on in production once verification email delivery is in place.
    /// </summary>
    public bool RequireEmailVerification { get; set; }
}
