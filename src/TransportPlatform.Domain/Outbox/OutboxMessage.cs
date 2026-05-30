namespace TransportPlatform.Domain.Outbox;

/// <summary>
/// A domain event captured for reliable delivery. Written in the SAME transaction as the
/// state change that produced it, then published by a background worker — so an event is
/// never lost if the process crashes after commit (the "outbox pattern").
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public int Attempts { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage() { } // EF

    public OutboxMessage(string type, string payload, DateTimeOffset occurredAtUtc)
    {
        Type = type;
        Payload = payload;
        OccurredAtUtc = occurredAtUtc;
    }

    public void MarkProcessed(DateTimeOffset nowUtc)
    {
        ProcessedAtUtc = nowUtc;
        Error = null;
    }

    public void RecordFailure(string error)
    {
        Attempts++;
        Error = error;
    }
}
