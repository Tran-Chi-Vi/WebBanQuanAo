using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WEBBANQUANAO.Models.Entities;

public enum OrderStatus : byte
{
    Pending = 0,
    Shipping = 1,
    Completed = 2,
    Cancelled = 3
}

public class Order
{
    [Key]
    public int OrderId { get; set; }

    public Guid OrderGuid { get; set; } = Guid.NewGuid();

    [MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int AddressId { get; set; }
    public Address Address { get; set; } = null!;

    public int? PromotionId { get; set; }
    public Promotion? Promotion { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.Now;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    public Payment? Payment { get; set; }
}
