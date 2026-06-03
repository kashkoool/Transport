namespace TransportPlatform.Application.Abstractions;

/// <summary>
/// App-facing view of external (social) login state, so API endpoints can branch on availability
/// and reference the auth scheme names WITHOUT depending on Infrastructure types (keeps the
/// endpoint layer on Application abstractions only). Infrastructure binds the real credentials and
/// sets <see cref="GoogleEnabled"/>.
/// </summary>
public sealed class ExternalAuthOptions
{
    /// <summary>Google authentication scheme (matches GoogleDefaults.AuthenticationScheme).</summary>
    public const string GoogleScheme = "Google";

    /// <summary>Short-lived cookie scheme that carries the OAuth handshake between Google and our callback.</summary>
    public const string ExternalCookieScheme = "External";

    /// <summary>True when Google sign-in is configured and wired up.</summary>
    public bool GoogleEnabled { get; set; }
}
