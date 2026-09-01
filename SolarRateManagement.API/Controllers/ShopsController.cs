using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarRateManagement.Application.Common.Interfaces;
using SolarRateManagement.Application.DTOs.Shop;
using SolarRateManagement.Infrastructure.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SolarRateManagement.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ShopsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public ShopsController(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ShopResponseDto>>> GetShops()
        {
            var userId = _shopContext.CurrentUserId;
            if (userId == null)
                return Unauthorized();

            IQueryable<Domain.Entities.Shop> query;

            if (_shopContext.IsSuperAdmin)
            {
                query = _context.Shops;
            }
            else
            {
                query = _context.UserShops
                    .Where(us => us.UserId == userId)
                    .Select(us => us.Shop);
            }

            var shops = await query
                .Select(s => new ShopResponseDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    OwnerName = s.OwnerName,
                    ContactNumber = s.ContactNumber,
                    Email = s.Email,
                    City = s.City,
                    Address = s.Address,
                    IsActive = s.IsActive
                })
                .ToListAsync();

            return Ok(shops);
        }
    }
}
