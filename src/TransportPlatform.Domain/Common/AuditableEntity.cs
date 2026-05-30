namespace TransportPlatform.Domain.Common;

/// <summary>
/// An entity that carries audit metadata. Timestamps are set by the persistence layer
/// (via <c>SaveChanges</c> interception) so business code never has to remember to.
/// </summary>
public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
}
