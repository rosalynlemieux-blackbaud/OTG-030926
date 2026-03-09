using OTG.Domain.Common;

namespace OTG.Domain.Identity;

public sealed class User : AuditableEntity
{
    public required string Email { get; set; }
    public string? PasswordHash { get; set; }
    public bool EmailConfirmed { get; set; }
    public List<UserRole> Roles { get; set; } = [UserRole.Participant];
    public Profile? Profile { get; set; }
}
