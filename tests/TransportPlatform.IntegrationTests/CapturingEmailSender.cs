using System.Collections.Concurrent;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Test <see cref="IEmailSender"/> that captures messages instead of sending, so tests can assert
/// an email was produced and extract the verify/reset link + token from its body.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>The most recent message addressed to <paramref name="email"/>, if any.</summary>
    public EmailMessage? LastTo(string email) =>
        _sent.Where(m => string.Equals(m.ToEmail, email, StringComparison.OrdinalIgnoreCase)).LastOrDefault();
}
