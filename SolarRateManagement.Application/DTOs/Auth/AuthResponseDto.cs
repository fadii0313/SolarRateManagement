using System.Collections.Generic;

namespace SolarRateManagement.Application.DTOs.Auth
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
        public List<UserShopDto> Shops { get; set; } = new();
    }

    public class UserShopDto
    {
        public int ShopId { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }
}
