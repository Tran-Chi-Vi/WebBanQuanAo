namespace WEBBANQUANAO.Services;

public enum CustomerTier : byte
{
    Bronze = 0,   // Đồng (< 2 triệu)
    Silver = 1,   // Bạc (>= 2 triệu) - Giảm 5%
    Gold = 2,     // Vàng (>= 5 triệu) - Giảm 10%
    Diamond = 3   // Kim Cương (>= 10 triệu) - Giảm 15%
}

public class TierInfo
{
    public CustomerTier Tier { get; set; }
    public string TierName { get; set; } = string.Empty;
    public string BadgeClass { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal NextTierThreshold { get; set; }
    public decimal AmountNeededForNextTier { get; set; }
    public double ProgressPercentage { get; set; }
}

public static class MembershipTierHelper
{
    public static TierInfo CalculateTier(decimal totalSpent)
    {
        if (totalSpent >= 10000000m) // >= 10 Triệu
        {
            return new TierInfo
            {
                Tier = CustomerTier.Diamond,
                TierName = "HẠNG KIM CƯƠNG (VIP)",
                BadgeClass = "bg-gradient-danger text-white border-danger",
                DiscountPercent = 15m,
                TotalSpent = totalSpent,
                NextTierThreshold = 10000000m,
                AmountNeededForNextTier = 0m,
                ProgressPercentage = 100.0
            };
        }
        else if (totalSpent >= 5000000m) // 5 - 10 Triệu
        {
            decimal needed = 10000000m - totalSpent;
            double progress = (double)((totalSpent - 5000000m) / 5000000m * 100m);

            return new TierInfo
            {
                Tier = CustomerTier.Gold,
                TierName = "HẠNG VÀNG (GOLD)",
                BadgeClass = "bg-warning text-dark border-warning",
                DiscountPercent = 10m,
                TotalSpent = totalSpent,
                NextTierThreshold = 10000000m,
                AmountNeededForNextTier = needed,
                ProgressPercentage = Math.Min(100, Math.Max(10, progress))
            };
        }
        else if (totalSpent >= 2000000m) // 2 - 5 Triệu
        {
            decimal needed = 5000000m - totalSpent;
            double progress = (double)((totalSpent - 2000000m) / 3000000m * 100m);

            return new TierInfo
            {
                Tier = CustomerTier.Silver,
                TierName = "HẠNG BẠC (SILVER)",
                BadgeClass = "bg-secondary text-white border-secondary",
                DiscountPercent = 5m,
                TotalSpent = totalSpent,
                NextTierThreshold = 5000000m,
                AmountNeededForNextTier = needed,
                ProgressPercentage = Math.Min(100, Math.Max(10, progress))
            };
        }
        else // < 2 Triệu
        {
            decimal needed = 2000000m - totalSpent;
            double progress = (double)(totalSpent / 2000000m * 100m);

            return new TierInfo
            {
                Tier = CustomerTier.Bronze,
                TierName = "HẠNG ĐỒNG (BRONZE)",
                BadgeClass = "bg-light text-dark border",
                DiscountPercent = 0m,
                TotalSpent = totalSpent,
                NextTierThreshold = 2000000m,
                AmountNeededForNextTier = needed,
                ProgressPercentage = Math.Min(100, Math.Max(5, progress))
            };
        }
    }
}
