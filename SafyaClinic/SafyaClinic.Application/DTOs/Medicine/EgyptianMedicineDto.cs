namespace SafyaClinic.Application.DTOs.Medicine;

// Shape of a single medicine as returned by the Egyptian drug-index lookup
// (whether it comes from a real external API or the local fallback dataset).
public class EgyptianMedicineDto
{
    public string TradeName { get; set; } = string.Empty;
    public string ScientificName { get; set; } = string.Empty; // Active ingredient
    public string Description { get; set; } = string.Empty;    // Dosage form (e.g., Tablets, Syrup)
    public string Strength { get; set; } = string.Empty;       // e.g., 500mg
    public decimal PublicPrice { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
}
