using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarRateManagement.Application.Common.Interfaces;
using SolarRateManagement.Application.DTOs.User;
using SolarRateManagement.Domain.Entities;
using SolarRateManagement.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SolarRateManagement.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;
        private readonly ITokenService _tokenService;

        public UsersController(AppDbContext context, IShopContext shopContext, ITokenService tokenService)
        {
            _context = context;
            _shopContext = shopContext;
            _tokenService = tokenService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            if (!await IsUserAuthorizedToManageUsers())
                return Forbid();

            var users = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    FullName = u.FirstName + " " + u.LastName,
                    u.Username,
                    u.Email,
                    u.Mobile,
                    u.IsActive,
                    u.CreatedDate,
                    Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList(),
                    Shops = u.UserShops.Select(us => new { us.ShopId, us.Shop.Name, us.RoleInShop }).ToList()
                })
                .OrderBy(u => u.FirstName)
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _context.Roles
                .Select(r => new { r.Id, r.Name, r.Description })
                .ToListAsync();
            return Ok(roles);
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!await IsUserAuthorizedToManageUsers())
                return Forbid();

            // Check if username or email already exists
            var usernameExists = await _context.Users.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower());
            if (usernameExists)
                return BadRequest(new { Message = $"Username '{dto.Username}' is already taken." });

            var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower());
            if (emailExists)
                return BadRequest(new { Message = $"Email '{dto.Email}' is already registered." });

            var newUser = new User
            {
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Username = dto.Username.Trim(),
                Email = dto.Email.Trim(),
                Mobile = dto.Mobile?.Trim() ?? string.Empty,
                PasswordHash = _tokenService.HashPassword(dto.Password),
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Assign Role if valid
            if (dto.RoleId > 0)
            {
                var roleExists = await _context.Roles.AnyAsync(r => r.Id == dto.RoleId);
                if (roleExists)
                {
                    _context.UserRoles.Add(new UserRole
                    {
                        UserId = newUser.Id,
                        RoleId = dto.RoleId
                    });
                }
            }

            // Assign Shop linkage if specified
            if (dto.ShopId.HasValue && dto.ShopId.Value > 0)
            {
                var shopExists = await _context.Shops.AnyAsync(s => s.Id == dto.ShopId.Value);
                if (shopExists)
                {
                    _context.UserShops.Add(new UserShop
                    {
                        UserId = newUser.Id,
                        ShopId = dto.ShopId.Value,
                        RoleInShop = "Manager"
                    });
                }
            }

            await _context.SaveChangesAsync();

            // Audit log creation
            var auditLog = new AuditLog
            {
                UserId = _shopContext.CurrentUserId ?? newUser.Id,
                Action = "CreateUser",
                Module = "Administration",
                NewValue = $"Created user '{newUser.Username}' ({newUser.FirstName} {newUser.LastName}).",
                Timestamp = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "User created successfully.", UserId = newUser.Id });
        }

        private async Task<bool> IsUserAuthorizedToManageUsers()
        {
            if (_shopContext.IsSuperAdmin) return true;
            var uid = _shopContext.CurrentUserId;
            if (uid == null) return false;
            return await _context.UserRoles
                .AnyAsync(ur => ur.UserId == uid &&
                    ur.Role.RolePermissions.Any(rp => rp.Permission.Code == "USER_MANAGE"));
        }
    }
}
