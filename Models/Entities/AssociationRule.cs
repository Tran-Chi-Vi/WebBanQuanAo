using System.ComponentModel.DataAnnotations;

namespace WEBBANQUANAO.Models.Entities;

/// <summary>
/// Kết quả thuật toán Apriori — được lưu sẵn (không tính lại mỗi request).
/// A ⇒ B: AntecedentProductId ⇒ ConsequentProductId.
/// </summary>
public class AssociationRule
{
    [Key]
    public int RuleId { get; set; }

    public int AntecedentProductId { get; set; }
    public Product AntecedentProduct { get; set; } = null!;

    public int ConsequentProductId { get; set; }
    public Product ConsequentProduct { get; set; } = null!;

    public double Support { get; set; }
    public double Confidence { get; set; }
    public double Lift { get; set; } // Lift > 1 mới có ý nghĩa gợi ý

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
