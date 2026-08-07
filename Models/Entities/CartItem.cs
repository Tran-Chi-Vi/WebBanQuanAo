using System.ComponentModel.DataAnnotations;

namespace WEBBANQUANAO.Models.Entities;

public class CartItem
{
    [Key]
    public int CartItemId { get; set; }

    public int CartId { get; set; }
    public Cart Cart { get; set; } = null!;

    public int VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;

    public int Quantity { get; set; }
}
