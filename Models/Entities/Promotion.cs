using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WEBBANQUANAO.Models.Entities;

public enum DiscountType : byte
{
    Percentage = 0,
    FixedAmount = 1
}

public class Promotion
{
    [Key]
    public int PromotionId { get; set; }

    [Required, MaxLength(30)]
    public string Code { get; set; } = null!;

    public DiscountType DiscountType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinOrderValue { get; set; } = 0;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public int? AssignedUserId { get; set; }

    [ForeignKey("AssignedUserId")]
    public User? AssignedUser { get; set; }

    [MaxLength(255)]
    public string? AllowedEmail { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
