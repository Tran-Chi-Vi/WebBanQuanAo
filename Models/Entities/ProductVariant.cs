using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WEBBANQUANAO.Models.Entities;

public class ProductVariant
{
    [Key]
    public int VariantId { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Required, MaxLength(10)]
    public string Size { get; set; } = null!;

    [Required, MaxLength(50)]
    public string Color { get; set; } = null!;

    [Required, MaxLength(50)]
    public string SKU { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public int StockQuantity { get; set; } = 0;

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
