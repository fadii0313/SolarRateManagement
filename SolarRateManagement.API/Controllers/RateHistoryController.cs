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
    [Route("api/rates/history")]
    public class RateHistoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public RateHistoryController(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetHistory(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? category)
        {
            var shopId = _shopContext.CurrentShopId;
            if (shopId == null)
                return BadRequest(new { Message = "Select a shop context to view rate history." });

            if (!_shopContext.IsSuperAdmin)
            {
                var authorized = await _context.UserShops
                    .AnyAsync(us => us.UserId == _shopContext.CurrentUserId && us.ShopId == shopId);
                if (!authorized) return Forbid();
            }

            var from = fromDate?.Date ?? DateTime.Today.AddDays(-29);
            var to   = toDate?.Date   ?? DateTime.Today;

            var query = _context.DailyRates
                .Include(r => r.Item).ThenInclude(i => i.Category)
                .Where(r => r.ShopId == shopId && r.RateDate >= from && r.RateDate <= to);

            if (!string.IsNullOrWhiteSpace(category) && category != "All")
                query = query.Where(r => r.Item.Category.Name == category);

            var records = await query
                .OrderByDescending(r => r.RateDate)
                .ThenBy(r => r.Item.Category.DisplayOrder)
                .ThenBy(r => r.Item.DisplayOrder)
                .Select(r => new
                {
                    r.Id,
                    r.RateDate,
                    r.Rate,
                    r.Remarks,
                    ItemId       = r.ItemId,
                    ItemCode     = r.Item.ItemCode,
                    ItemName     = r.Item.ItemName,
                    Brand        = r.Item.Brand,
                    Unit         = r.Item.Unit,
                    CategoryName = r.Item.Category.Name
                })
                .ToListAsync();

            return Ok(records);
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var cats = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => c.Name)
                .ToListAsync();
            return Ok(cats);
        }
    }
}
