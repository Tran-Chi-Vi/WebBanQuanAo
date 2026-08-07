using System.ComponentModel.DataAnnotations;

namespace WEBBANQUANAO.Models.Entities;

public class User
{
    [Key]
    public int UserId { get; set; }

    public Guid UserGuid { get; set; } = Guid.NewGuid();

    [MaxLength(100)]
    public string? Username { get; set; }

    [Required, MaxLength(150)]
    public string Email { get; set; } = null!;

    [MaxLength(255)]
    public string? PasswordHash { get; set; }

    [MaxLength(100)]
    public string? GoogleId { get; set; }

    [MaxLength(100)]
    public string? FacebookId { get; set; }

    [Required, MaxLength(150)]
    public string FullName { get; set; } = null!;

    [MaxLength(15)]
    public string? Phone { get; set; }

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public Cart? Cart { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<UserBehaviorLog> BehaviorLogs { get; set; } = new List<UserBehaviorLog>();
    public ICollection<ChatConversation> ChatConversations { get; set; } = new List<ChatConversation>();
}
