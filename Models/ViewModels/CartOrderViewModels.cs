using System.ComponentModel.DataAnnotations;
using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Models.ViewModels;

public class CartItemViewModel
{
    public int CartItemId { get; set; }
    public int VariantId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int AvailableStock { get; set; }
    public decimal TotalPrice => Price * Quantity;
}

public class CartViewModel
{
    public List<CartItemViewModel> Items { get; set; } = new();
    public decimal SubTotal => Items.Sum(i => i.TotalPrice);
    public string? AppliedPromoCode { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalTotal => Math.Max(0, SubTotal - DiscountAmount);
    public string? PromoMessage { get; set; }
    public bool IsPromoValid { get; set; }
}

public class AddToCartRequest
{
    [Required]
    public int VariantId { get; set; }

    [Range(1, 100, ErrorMessage = "Số lượng phải từ 1 đến 100")]
    public int Quantity { get; set; } = 1;
}

public class CheckoutViewModel
{
    public CartViewModel Cart { get; set; } = new();
    public List<Address> UserAddresses { get; set; } = new();

    public int? SelectedAddressId { get; set; }

    // New address input option
    public AddressNewViewModel NewAddress { get; set; } = new();
    public bool CreateNewAddress { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
    public string PaymentMethod { get; set; } = "COD"; // COD, PayOS, QR

    public string? Note { get; set; }
    public string? PromoCode { get; set; }
}
