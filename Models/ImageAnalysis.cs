using System.ComponentModel.DataAnnotations;

namespace AIImageService.Models;

public class ImageAnalysis
{
    public int Id { get; set; }

    [Required]
    public string FileName { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Prediction { get; set; } = "Pending";
    public double Confidence { get; set; }
}
