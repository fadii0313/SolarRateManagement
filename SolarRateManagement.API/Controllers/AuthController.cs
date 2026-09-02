using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarRateManagement.Application.Common.Interfaces;
using SolarRateManagement.Application.DTOs.Auth;
using SolarRateManagement.Domain.Entities;
using SolarRateManagement.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SolarRateManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITokenService _tokenService;

        public AuthController(AppDbContext context, ITokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _context.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.UserShops).ThenInclude(us => us.Shop)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == loginDto.Username.ToLower());

            if (user == null || !user.IsActive)
                return Unauthorized(new { Message = "Invalid username or password" });

            if (!_tokenService.VerifyPassword(loginDto.Password, user.PasswordHash))
                return Unauthorized(new { Message = "Invalid username or password" });

            var roles = user.UserRoles
                .Where(ur => ur.Role != null)
                .Select(ur => ur.Role.Name)
                .ToList();
            var roleIds = user.UserRoles
                .Where(ur => ur.Role != null)
                .Select(ur => ur.RoleId)
                .ToList();

            // Fetch permission codes linked to roles
            var permissions = await _context.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId) && rp.Permission != null)
                .Select(rp => rp.Permission.Code)
                .Distinct()
                .ToListAsync();

            var isSuperAdmin = roles.Contains("SuperAdmin");
            List<UserShopDto> shopsDto;

            if (isSuperAdmin)
            {
                // SuperAdmin sees all shops in the system
                var allShops = await _context.Shops.ToListAsync();
                shopsDto = allShops.Select(s => new UserShopDto
                {
                    ShopId = s.Id,
                    ShopName = s.Name,
                    RoleName = "SuperAdmin"
                }).ToList();
            }
            else
            {
                shopsDto = user.UserShops
                    .Where(us => us.Shop != null)
                    .Select(us => new UserShopDto
                    {
                        ShopId = us.ShopId,
                        ShopName = us.Shop.Name,
                        RoleName = us.RoleInShop
                    }).ToList();
            }

            var accessToken = _tokenService.GenerateAccessToken(user, roles, permissions);
            var refreshTokenStr = _tokenService.GenerateRefreshToken();

            // Save refresh token to database (expires in 7 days)
            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshTokenStr,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedDate = DateTime.UtcNow,
                IsRevoked = false
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            // Record login action in audit logs
            var auditLog = new AuditLog
            {
                UserId = user.Id,
                Action = "Login",
                Module = "Authentication",
                NewValue = $"User {user.Username} logged in successfully.",
                Timestamp = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            return Ok(new AuthResponseDto
            {
                Token = accessToken,
                RefreshToken = refreshTokenStr,
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = roles,
                Permissions = permissions,
                Shops = shopsDto
            });
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponseDto>> Refresh([FromBody] RefreshTokenDto refreshDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existingToken = await _context.RefreshTokens
                .Include(rt => rt.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                .Include(rt => rt.User)
                    .ThenInclude(u => u.UserShops)
                        .ThenInclude(us => us.Shop)
                .FirstOrDefaultAsync(rt => rt.Token == refreshDto.RefreshToken);

            if (existingToken == null || !existingToken.IsActive)
                return Unauthorized(new { Message = "Invalid or expired refresh token" });

            // Revoke current refresh token
            existingToken.IsRevoked = true;

            var user = existingToken.User;
            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
            var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();

            var permissions = await _context.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Select(rp => rp.Permission.Code)
                .Distinct()
                .ToListAsync();

            var isSuperAdmin = roles.Contains("SuperAdmin");
            List<UserShopDto> shopsDto;

            if (isSuperAdmin)
            {
                var allShops = await _context.Shops.ToListAsync();
                shopsDto = allShops.Select(s => new UserShopDto
                {
                    ShopId = s.Id,
                    ShopName = s.Name,
                    RoleName = "SuperAdmin"
                }).ToList();
            }
            else
            {
                shopsDto = user.UserShops.Select(us => new UserShopDto
                {
                    ShopId = us.ShopId,
                    ShopName = us.Shop.Name,
                    RoleName = us.RoleInShop
                }).ToList();
            }

            var newAccessToken = _tokenService.GenerateAccessToken(user, roles, permissions);
            var newRefreshTokenStr = _tokenService.GenerateRefreshToken();

            var newRefreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = newRefreshTokenStr,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedDate = DateTime.UtcNow,
                IsRevoked = false
            };

            _context.RefreshTokens.Add(newRefreshToken);
            await _context.SaveChangesAsync();

            return Ok(new AuthResponseDto
            {
                Token = newAccessToken,
                RefreshToken = newRefreshTokenStr,
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = roles,
                Permissions = permissions,
                Shops = shopsDto
            });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] string refreshToken)
        {
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (token != null)
            {
                token.IsRevoked = true;
                await _context.SaveChangesAsync();
            }

            return Ok(new { Message = "Logged out successfully" });
        }
    }
}
