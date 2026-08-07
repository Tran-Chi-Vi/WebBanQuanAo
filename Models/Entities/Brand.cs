using System.ComponentModel.DataAnnotations;

namespace WEBBANQUANAO.Models.Entities;

public class Brand
{
    [Key]
    public int BrandId { get; set; }

    [Required]
    [MaxLength(100)]
    public string BrandName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? LogoUrl { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}