using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WEBBANQUANAO.Models.Entities;

public enum ProductGender : byte
{
    Male = 0,
    Female = 1,
    Unisex = 2
}

public enum ProductStatus : byte
{
    Discontinued = 0,
    Active = 1
}

public class Product
{
    [Key]
    public int ProductId { get; set; }

    public Guid ProductGuid { get; set; } = Guid.NewGuid();

    [MaxLength(250)]
    public string Slug { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ProductName { get; set; } = null!;

    public string? Description { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    public ProductGender Gender { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BasePrice { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<UserBehaviorLog> BehaviorLogs { get; set; } = new List<UserBehaviorLog>();

    public ICollection<AssociationRule> RulesAsAntecedent { get; set; } = new List<AssociationRule>();
    public ICollection<AssociationRule> RulesAsConsequent { get; set; } = new List<AssociationRule>();
}
