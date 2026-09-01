using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarRateManagement.Application.Common.Interfaces;
using SolarRateManagement.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SolarRateManagement.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/rates/compare")]
    public class RateCompareController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public RateCompareController(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        // GET /api/rates/compare?date=2026-08-31
        // Returns all items with rates for every shop the user has access to
        [HttpGet]
        public async Task<IActionResult> Compare([FromQuery] DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;

            // Determine accessible shops
            int[] shopIds;
            if (_shopContext.IsSuperAdmin)
            {
                shopIds = await _context.Shops
                    .Where(s => s.IsActive)
                    .Select(s => s.Id)
                    .ToArrayAsync();
            }
            else
            {
                var uid = _shopContext.CurrentUserId;
                if (uid == null) return Unauthorized();
                shopIds = await _context.UserShops
                    .Where(us => us.UserId == uid)
                    .Select(us => us.ShopId)
                    .ToArrayAsync();
            }

            if (shopIds.Length == 0)
                return BadRequest(new { Message = "No accessible shops found." });

            // Load shop names
            var shops = await _context.Shops
                .Where(s => shopIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Name })
                .ToListAsync();

            // Load all active global items
            var items = await _context.Items
                .Include(i => i.Category)
                .Where(i => i.IsActive && i.ShopId == null)
                .OrderBy(i => i.Category.DisplayOrder)
                .ThenBy(i => i.DisplayOrder)
                .ToListAsync();

            // Load rates for all shops for targetDate
            var rates = await _context.DailyRates
                .Where(r => shopIds.Contains(r.ShopId) && r.RateDate == targetDate)
                .ToListAsync();

            var ratesMap = rates.ToDictionary(r => (r.ShopId, r.ItemId), r => r.Rate);

            var result = items.Select(item => new
            {
                item.Id,
                item.ItemCode,
                item.ItemName,
                item.Brand,
                item.Unit,
                CategoryName = item.Category.Name,
                ShopRates = shops.Select(s => new
                {
                    ShopId   = s.Id,
                    ShopName = s.Name,
                    Rate     = ratesMap.TryGetValue((s.Id, item.Id), out var r) ? r : (decimal?)null
                }).ToList()
            }).ToList();

            return Ok(new { date = targetDate, shops, items = result });
        }
    }
}
