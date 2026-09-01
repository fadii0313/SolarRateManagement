using SolarRateManagement.Domain.Entities;
using System.Collections.Generic;

namespace SolarRateManagement.Application.Common.Interfaces
{
    public interface ITokenService
    {
        string GenerateAccessToken(User user, List<string> roles, List<string> permissions);
        string GenerateRefreshToken();
        string HashPassword(string password);
        bool VerifyPassword(string password, string hashedPassword);
    }
}
