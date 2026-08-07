using System.ComponentModel.DataAnnotations;
using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Models.Entities;

public class ProductImage
{
    [Key]
    public int ImageId { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Required, MaxLength(255)]
    public string ImageUrl { get; set; } = null!;

    public bool IsMain { get; set; } = false;
}
