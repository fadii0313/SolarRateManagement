using System.ComponentModel.DataAnnotations;

namespace SolarRateManagement.Application.DTOs.Rate
{
    public class SaveDailyRateDto
    {
        [Required]
        public int ItemId { get; set; }

        [Required]
        [Range(0, 10000000.00)]
        public decimal Rate { get; set; }

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;
    }
}
