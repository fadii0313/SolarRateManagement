using System.ComponentModel.DataAnnotations;

namespace SolarRateManagement.Application.DTOs.Auth
{
    public class RefreshTokenDto
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
