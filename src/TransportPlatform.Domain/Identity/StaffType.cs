namespace TransportPlatform.Domain.Identity;

/// <summary>
/// Sub-type of a company staff member (all share the <c>Staff</c> role). Mirrors the actor model:
/// accountants handle money, supervisors oversee, employees run the counter. Drivers are modelled
/// separately (<see cref="Fleet.Driver"/>) because they don't log in.
/// </summary>
public enum StaffType
{
    Accountant,
    Supervisor,
    Employee,
}
