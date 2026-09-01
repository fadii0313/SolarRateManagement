using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarRateManagement.Application.Common.Interfaces;
using SolarRateManagement.Domain.Entities;
using SolarRateManagement.Infrastructure.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SolarRateManagement.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AuditLogsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public AuditLogsController(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAuditLogs()
        {
            // Verify permission
            if (!await HasAuditViewPermission())
            {
                return Forbid();
            }

            var logs = await _context.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Select(a => new
                {
                    a.Id,
                    Username = a.User != null ? a.User.Username : "System",
                    UserFullName = a.User != null ? $"{a.User.FirstName} {a.User.LastName}" : "System",
                    a.Action,
                    a.Module,
                    a.NewValue,
                    a.Timestamp,
                    a.IpAddress
                })
                .ToListAsync();

            return Ok(logs);
        }

        private async Task<bool> HasAuditViewPermission()
        {
            if (_shopContext.IsSuperAdmin)
                return true;

            var userId = _shopContext.CurrentUserId;
            if (userId == null) return false;

            return await _context.UserRoles
                .AnyAsync(ur => ur.UserId == userId &&
                                ur.Role.RolePermissions.Any(rp => rp.Permission.Code == "AUDIT_VIEW"));
        }
    }
}
