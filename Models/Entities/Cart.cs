using System.ComponentModel.DataAnnotations;

namespace WEBBANQUANAO.Models.Entities;

public class Cart
{
    [Key]
    public int CartId { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
