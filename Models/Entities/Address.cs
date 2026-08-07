using System.ComponentModel.DataAnnotations;

namespace WEBBANQUANAO.Models.Entities;

public class Address
{
    [Key]
    public int AddressId { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required, MaxLength(150)]
    public string RecipientName { get; set; } = null!;

    [Required, MaxLength(15)]
    public string Phone { get; set; } = null!;

    [Required, MaxLength(255)]
    public string DetailAddress { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Province { get; set; } = null!;

    [Required, MaxLength(100)]
    public string District { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Ward { get; set; } = null!;

    public bool IsDefault { get; set; } = false;
}
