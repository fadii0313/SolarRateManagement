using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarRateManagement.Application.Common.Interfaces;
using SolarRateManagement.Application.DTOs.Rate;
using SolarRateManagement.Domain.Entities;
using SolarRateManagement.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SolarRateManagement.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/rates/daily")]
    public class DailyRatesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public DailyRatesController(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DailyRateResponseDto>>> GetDailyRates([FromQuery] DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;
            var shopId = _shopContext.CurrentShopId;

            if (shopId == null)
            {
                return BadRequest(new { Message = "Please select a shop context to load daily rates." });
            }

            if (!await IsUserAuthorizedForShop(shopId.Value))
            {
                return Forbid();
            }

            // Get all active items that should have rates (global templates + shop-scoped)
            var items = await _context.Items
                .Include(i => i.Category)
                .Where(i => i.IsActive && (i.ShopId == null || i.ShopId == shopId.Value))
                .OrderBy(i => i.Category.DisplayOrder)
                .ThenBy(i => i.DisplayOrder)
                .ToListAsync();

            // Get rates for today (target date)
            var todayRates = await _context.DailyRates
                .Where(r => r.ShopId == shopId.Value && r.RateDate == targetDate)
                .ToDictionaryAsync(r => r.ItemId);

            // Get rates for yesterday (target date - 1 day)
            var yesterdayDate = targetDate.AddDays(-1);
            var yesterdayRates = await _context.DailyRates
                .Where(r => r.ShopId == shopId.Value && r.RateDate == yesterdayDate)
                .ToDictionaryAsync(r => r.ItemId, r => r.Rate);

            var result = items.Select(item =>
            {
                todayRates.TryGetValue(item.Id, out var todayRate);
                yesterdayRates.TryGetValue(item.Id, out var yesterdayPrice);

                return new DailyRateResponseDto
                {
                    ItemId = item.Id,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    Brand = item.Brand,
                    Model = item.Model,
                    Unit = item.Unit,
                    CategoryName = item.Category.Name,
                    RateId = todayRate?.Id,
                    Rate = todayRate?.Rate ?? 0,
                    YesterdayRate = yesterdayPrice,
                    Remarks = todayRate?.Remarks ?? string.Empty,
                    IsLocked = false
                };
            }).ToList();

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> SaveDailyRates([FromBody] List<SaveDailyRateDto> ratesDto, [FromQuery] DateTime? date)
        {
            if (ratesDto == null || !ratesDto.Any())
            {
                return BadRequest(new { Message = "No rates data provided." });
            }

            var targetDate = date?.Date ?? DateTime.Today;
            var shopId = _shopContext.CurrentShopId;

            if (shopId == null)
            {
                return BadRequest(new { Message = "Please select a shop context to save daily rates." });
            }

            if (!await IsUserAuthorizedForShop(shopId.Value))
            {
                return Forbid();
            }

            // Verify permissions to edit rates
            if (!await HasRateWritePermission())
            {
                return Forbid();
            }

            var itemIds = ratesDto.Select(r => r.ItemId).ToList();

            // Verify that all items belong to this shop context (either global or shop-scoped)
            var validItemCount = await _context.Items
                .CountAsync(i => itemIds.Contains(i.Id) && (i.ShopId == null || i.ShopId == shopId.Value));

            if (validItemCount != itemIds.Count)
            {
                return BadRequest(new { Message = "One or more item IDs are invalid for this shop context." });
            }

            // Fetch existing rates for today to perform update or insert
            var existingRates = await _context.DailyRates
                .Where(r => r.ShopId == shopId.Value && r.RateDate == targetDate && itemIds.Contains(r.ItemId))
                .ToDictionaryAsync(r => r.ItemId);

            foreach (var dto in ratesDto)
            {
                if (existingRates.TryGetValue(dto.ItemId, out var dailyRate))
                {

                    dailyRate.Rate = dto.Rate;
                    dailyRate.Remarks = dto.Remarks ?? string.Empty;
                    dailyRate.ModifiedDate = DateTime.UtcNow;
                }
                else
                {
                    var newRate = new DailyRate
                    {
                        ShopId = shopId.Value,
                        ItemId = dto.ItemId,
                        RateDate = targetDate,
                        Rate = dto.Rate,
                        Remarks = dto.Remarks ?? string.Empty,
                        CreatedDate = DateTime.UtcNow
                    };
                    _context.DailyRates.Add(newRate);
                }
            }

            await _context.SaveChangesAsync();

            var shopObj = await _context.Shops.FindAsync(shopId.Value);
            var shopName = shopObj?.Name ?? $"Shop #{shopId.Value}";

            // Record edit in audit log
            var auditLog = new AuditLog
            {
                UserId = _shopContext.CurrentUserId!.Value,
                Action = "SaveDailyRates",
                Module = "Rates",
                NewValue = $"Rate update submitted by '{shopName}': {ratesDto.Count} items updated for {targetDate:yyyy-MM-dd}.",
                Timestamp = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Daily rates saved successfully." });
        }

        public class CopyRatesRequestDto
        {
            public DateTime SourceDate { get; set; }
            public DateTime TargetDate { get; set; }
            public bool OverwriteExisting { get; set; } = false;
        }

        [HttpPost("/api/rates/copy")]
        public async Task<IActionResult> CopyRates([FromBody] CopyRatesRequestDto dto)
        {
            var shopId = _shopContext.CurrentShopId;
            if (shopId == null)
                return BadRequest(new { Message = "Please select a shop context first." });

            if (!await IsUserAuthorizedForShop(shopId.Value) || !await HasRateWritePermission())
                return Forbid();

            var sourceDate = dto.SourceDate.Date;
            var targetDate = dto.TargetDate.Date;

            var sourceRates = await _context.DailyRates
                .Where(r => r.ShopId == shopId.Value && r.RateDate == sourceDate)
                .ToListAsync();

            if (sourceRates.Count == 0)
                return BadRequest(new { Message = $"No rates found on source date {sourceDate:yyyy-MM-dd} to copy." });

            var existingTargetRates = await _context.DailyRates
                .Where(r => r.ShopId == shopId.Value && r.RateDate == targetDate)
                .ToDictionaryAsync(r => r.ItemId);

            int copiedCount = 0;
            foreach (var sr in sourceRates)
            {
                if (existingTargetRates.TryGetValue(sr.ItemId, out var existing))
                {
                    if (dto.OverwriteExisting)
                    {
                        existing.Rate = sr.Rate;
                        existing.Remarks = $"Copied from {sourceDate:yyyy-MM-dd}";
                        existing.ModifiedBy = _shopContext.CurrentUserId!.Value;
                        existing.ModifiedDate = DateTime.UtcNow;
                        copiedCount++;
                    }
                }
                else
                {
                    _context.DailyRates.Add(new DailyRate
                    {
                        ShopId = shopId.Value,
                        ItemId = sr.ItemId,
                        RateDate = targetDate,
                        Rate = sr.Rate,
                        Remarks = $"Copied from {sourceDate:yyyy-MM-dd}",
                        CreatedBy = _shopContext.CurrentUserId!.Value,
                        CreatedDate = DateTime.UtcNow
                    });
                    copiedCount++;
                }
            }

            await _context.SaveChangesAsync();

            // Record audit log
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = _shopContext.CurrentUserId!.Value,
                Action = "CopyDailyRates",
                Module = "Rates",
                NewValue = $"Copied {copiedCount} rates from {sourceDate:yyyy-MM-dd} to {targetDate:yyyy-MM-dd} in shop {shopId}.",
                Timestamp = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"Successfully copied {copiedCount} item rates from {sourceDate:yyyy-MM-dd} to {targetDate:yyyy-MM-dd}." });
        }

        private async Task<bool> IsUserAuthorizedForShop(int targetShopId)
        {
            if (_shopContext.IsSuperAdmin)
                return true;

            var userId = _shopContext.CurrentUserId;
            if (userId == null) return false;

            return await _context.UserShops.AnyAsync(us => us.UserId == userId && us.ShopId == targetShopId);
        }

        private async Task<bool> HasRateWritePermission()
        {
            if (_shopContext.IsSuperAdmin)
                return true;

            var userId = _shopContext.CurrentUserId;
            if (userId == null) return false;

            // Check if user has RATE_CREATE or RATE_EDIT in any role
            var hasPerm = await _context.UserRoles
                .AnyAsync(ur => ur.UserId == userId &&
                                ur.Role.RolePermissions.Any(rp => rp.Permission.Code == "RATE_CREATE" || rp.Permission.Code == "RATE_EDIT"));

            return hasPerm;
        }
    }
}
