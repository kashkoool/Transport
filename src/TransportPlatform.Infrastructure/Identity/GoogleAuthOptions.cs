namespace TransportPlatform.Infrastructure.Identity;

/// <summary>
/// Google OAuth configuration (section <c>OAuth:Google</c>). Google sign-in is registered ONLY
/// when <see cref="ClientId"/> is set, so dev/CI without credentials still boots normally and the
/// auth endpoints simply report Google as unavailable.
/// </summary>
public sealed class GoogleAuthOptions
{
    public const string SectionName = "OAuth:Google";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Path Google redirects back to after consent — must match the Console redirect URI.</summary>
    public string CallbackPath { get; set; } = "/signin-google";
}
