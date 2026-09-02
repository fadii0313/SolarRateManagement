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

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!await IsUserAuthorizedToManageUsers())
                return Forbid();

            var user = await _context.Users
                .Include(u => u.UserRoles)
                .Include(u => u.UserShops)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound(new { Message = "User not found." });

            // Ensure username/email uniqueness if changed
            if (user.Username.ToLower() != dto.Username.ToLower() &&
                await _context.Users.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower()))
            {
                return BadRequest(new { Message = $"Username '{dto.Username}' is already taken." });
            }

            if (user.Email.ToLower() != dto.Email.ToLower() &&
                await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower()))
            {
                return BadRequest(new { Message = $"Email '{dto.Email}' is registered to another user." });
            }

            user.FirstName = dto.FirstName.Trim();
            user.LastName = dto.LastName.Trim();
            user.Username = dto.Username.Trim();
            user.Email = dto.Email.Trim();
            user.Mobile = dto.Mobile?.Trim() ?? string.Empty;
            user.IsActive = dto.IsActive;

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                user.PasswordHash = _tokenService.HashPassword(dto.Password);
            }

            // Update role if changed
            if (dto.RoleId > 0)
            {
                _context.UserRoles.RemoveRange(user.UserRoles);
                _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = dto.RoleId });
            }

            // Update shop assignment if changed
            if (dto.ShopId.HasValue && dto.ShopId.Value > 0)
            {
                _context.UserShops.RemoveRange(user.UserShops);
                _context.UserShops.Add(new UserShop { UserId = user.Id, ShopId = dto.ShopId.Value, RoleInShop = "Manager" });
            }

            await _context.SaveChangesAsync();

            return Ok(new { Message = "User updated successfully." });
        }

        [HttpPut("{id}/toggle-status")]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            if (!await IsUserAuthorizedToManageUsers())
                return Forbid();

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { Message = "User not found." });

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"User status changed to {(user.IsActive ? "Active" : "Inactive")}.", user.IsActive });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!await IsUserAuthorizedToManageUsers())
                return Forbid();

            var user = await _context.Users
                .Include(u => u.UserRoles)
                .Include(u => u.UserShops)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound(new { Message = "User not found." });

            // Prevent deleting self if SuperAdmin
            if (_shopContext.CurrentUserId == id)
            {
                return BadRequest(new { Message = "You cannot delete your own logged-in account." });
            }

            _context.UserRoles.RemoveRange(user.UserRoles);
            _context.UserShops.RemoveRange(user.UserShops);
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "User deleted successfully." });
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
