using System.ComponentModel.DataAnnotations;

namespace SolarRateManagement.Application.DTOs.User
{
    public class UpdateUserDto
    {
        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string Mobile { get; set; } = string.Empty;

        public string? Password { get; set; }

        public int RoleId { get; set; }

        public int? ShopId { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
