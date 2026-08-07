using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WEBBANQUANAO.Models.Entities;

public enum PaymentStatus : byte
{
    WaitingForPayment = 0,
    Success = 1,
    Failed = 2
}

public class Payment
{
    [Key]
    public int PaymentId { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    [MaxLength(100)]
    public string? PayOSTransactionId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.WaitingForPayment;

    public DateTime? PaidAt { get; set; }

    [MaxLength(255)]
    public string? QRCodeUrl { get; set; }
}
