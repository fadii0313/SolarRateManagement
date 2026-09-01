using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarRateManagement.Application.Common.Interfaces;
using SolarRateManagement.Application.DTOs.Item;
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
    [Route("api/[controller]")]
    public class ItemsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public ItemsController(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ItemResponseDto>>> GetItems()
        {
            var shopId = _shopContext.CurrentShopId;

            // Enforce tenant boundary check
            if (!await IsUserAuthorizedForShop(shopId))
            {
                return Forbid();
            }

            // Retrieve items: global template items (ShopId == null) 
            // plus items specific to the active shop (if a shop context is active)
            var items = await _context.Items
                .Include(i => i.Category)
                .Where(i => i.IsActive && (i.ShopId == null || i.ShopId == shopId))
                .OrderBy(i => i.Category.DisplayOrder)
                .ThenBy(i => i.DisplayOrder)
                .Select(i => new ItemResponseDto
                {
                    Id = i.Id,
                    ItemCode = i.ItemCode,
                    ItemName = i.ItemName,
                    Brand = i.Brand,
                    Model = i.Model,
                    Unit = i.Unit,
                    Description = i.Description,
                    CategoryId = i.CategoryId,
                    CategoryName = i.Category.Name,
                    ShopId = i.ShopId,
                    IsActive = i.IsActive
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost]
        public async Task<ActionResult<ItemResponseDto>> CreateItem([FromBody] CreateItemDto createDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var shopId = _shopContext.CurrentShopId;

            // Tenant boundary security
            if (!await IsUserAuthorizedForShop(shopId))
            {
                return Forbid();
            }

            // Check if ItemCode already exists for this shop context
            var codeExists = await _context.Items.AnyAsync(i => i.ShopId == shopId && i.ItemCode.ToLower() == createDto.ItemCode.ToLower());
            if (codeExists)
            {
                return BadRequest(new { Message = $"An item with code '{createDto.ItemCode}' already exists in this shop context." });
            }

            // Validate Category
            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == createDto.CategoryId);
            if (!categoryExists)
            {
                return BadRequest(new { Message = "The specified category does not exist." });
            }

            // Determine display order (appended to end of category items)
            var maxDisplayOrder = await _context.Items
                .Where(i => i.CategoryId == createDto.CategoryId && i.ShopId == shopId)
                .Select(i => (int?)i.DisplayOrder)
                .FirstOrDefaultAsync() ?? 0;

            var newItem = new Item
            {
                CategoryId = createDto.CategoryId,
                ShopId = shopId,
                ItemCode = createDto.ItemCode.Trim(),
                ItemName = createDto.ItemName.Trim(),
                Brand = createDto.Brand?.Trim() ?? string.Empty,
                Model = createDto.Model?.Trim() ?? string.Empty,
                Unit = createDto.Unit.Trim(),
                Description = createDto.Description?.Trim() ?? string.Empty,
                IsActive = createDto.IsActive,
                DisplayOrder = maxDisplayOrder + 1,
                CreatedDate = DateTime.UtcNow
            };

            _context.Items.Add(newItem);
            await _context.SaveChangesAsync();

            // Record creation in audit log
            var auditLog = new AuditLog
            {
                UserId = _shopContext.CurrentUserId!.Value,
                Action = "CreateItem",
                Module = "Catalog",
                NewValue = $"Created item '{newItem.ItemCode}' in shop context '{shopId}'.",
                Timestamp = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            var categoryName = await _context.Categories.Where(c => c.Id == newItem.CategoryId).Select(c => c.Name).FirstAsync();

            return CreatedAtAction(nameof(GetItems), new { id = newItem.Id }, new ItemResponseDto
            {
                Id = newItem.Id,
                ItemCode = newItem.ItemCode,
                ItemName = newItem.ItemName,
                Brand = newItem.Brand,
                Model = newItem.Model,
                Unit = newItem.Unit,
                Description = newItem.Description,
                CategoryId = newItem.CategoryId,
                CategoryName = categoryName,
                ShopId = newItem.ShopId,
                IsActive = newItem.IsActive
            });
        }

        private async Task<bool> IsUserAuthorizedForShop(int? targetShopId)
        {
            if (targetShopId == null)
            {
                // Only SuperAdmin can view or modify global templates (null shop ID)
                return _shopContext.IsSuperAdmin;
            }

            if (_shopContext.IsSuperAdmin)
                return true;

            var userId = _shopContext.CurrentUserId;
            if (userId == null) return false;

            return await _context.UserShops.AnyAsync(us => us.UserId == userId && us.ShopId == targetShopId);
        }
    }
}
