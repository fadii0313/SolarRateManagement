using System.ComponentModel.DataAnnotations;

namespace SolarRateManagement.Application.DTOs.Shop
{
    public class CreateShopDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public string OwnerName { get; set; } = string.Empty;

        public string ContactNumber { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        [Required]
        public string City { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
