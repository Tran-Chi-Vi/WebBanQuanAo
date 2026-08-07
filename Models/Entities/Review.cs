using System.ComponentModel.DataAnnotations;

namespace WEBBANQUANAO.Models.Entities;

public class Review
{
    [Key]
    public int ReviewId { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public byte Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
