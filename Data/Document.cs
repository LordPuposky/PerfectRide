using System.ComponentModel.DataAnnotations;

namespace PerfectRide.Data;

public enum DocumentType
{
    Receipt,
    Registration,
    Insurance,
    Warranty,
    Other
}

public enum FileType
{
    Pdf,
    Jpg,
    Png
}

public class Document
{
    public int Id { get; set; }
    [Required]
    public int VehicleId { get; set; }
    [Required]
    public DocumentType DocumentType { get; set; }
    [Required, StringLength(60)]
    public string Name { get; set; } = string.Empty;
    [StringLength(500)]
    public string? Description { get; set; }
    [Required, StringLength(250)]
    public string FileName { get; set; } = string.Empty;
    [Required]
    public FileType FileType { get; set; }
    [Required, StringLength(500)]
    public string FilePath { get; set; } = string.Empty;
    [Required]
    public DateTime DateUploaded { get; set; }
    public Vehicle? Vehicle { get; set; }

}