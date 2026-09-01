namespace Retalon.Models.Entities;

public class Role
{
    public Guid RoleId {  get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

