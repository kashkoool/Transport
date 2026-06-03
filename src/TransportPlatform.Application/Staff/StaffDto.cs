namespace TransportPlatform.Application.Staff;

/// <summary>Read model for a company staff member.</summary>
public sealed record StaffDto(Guid Id, string Email, string FullName, string StaffType, bool Suspended);
