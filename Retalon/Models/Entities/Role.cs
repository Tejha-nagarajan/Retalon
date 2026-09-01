namespace Retalon.Models.Entities;

public class Role
{
    public Guid RoleID {  get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<UserRole> Users { get; set; } = new List<UserRole>();
}

