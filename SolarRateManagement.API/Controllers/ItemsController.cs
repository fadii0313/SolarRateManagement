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

            if (!await IsUserAuthorizedForShop(shopId))
            {
                return Forbid();
            }

            var items = await _context.Items
                .Include(i => i.Category)
                .Where(i => (i.ShopId == null || i.ShopId == shopId))
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

            if (!await IsUserAuthorizedForShop(shopId))
            {
                return Forbid();
            }

            var codeExists = await _context.Items.AnyAsync(i => i.ShopId == shopId && i.ItemCode.ToLower() == createDto.ItemCode.ToLower());
            if (codeExists)
            {
                return BadRequest(new { Message = $"An item with code '{createDto.ItemCode}' already exists in this shop context." });
            }

            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == createDto.CategoryId);
            if (!categoryExists)
            {
                return BadRequest(new { Message = "The specified category does not exist." });
            }

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

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateItem(int id, [FromBody] CreateItemDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var item = await _context.Items.FindAsync(id);
            if (item == null)
                return NotFound(new { Message = "Item not found." });

            if (!await IsUserAuthorizedForShop(item.ShopId))
                return Forbid();

            item.CategoryId = dto.CategoryId;
            item.ItemCode = dto.ItemCode.Trim();
            item.ItemName = dto.ItemName.Trim();
            item.Brand = dto.Brand?.Trim() ?? "";
            item.Model = dto.Model?.Trim() ?? "";
            item.Unit = dto.Unit.Trim();
            item.Description = dto.Description?.Trim() ?? "";
            item.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();
            return Ok(item);
        }

        [HttpPut("{id}/toggle-status")]
        public async Task<IActionResult> ToggleItemStatus(int id)
        {
            var item = await _context.Items.FindAsync(id);
            if (item == null)
                return NotFound(new { Message = "Item not found." });

            if (!await IsUserAuthorizedForShop(item.ShopId))
                return Forbid();

            item.IsActive = !item.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"Item status updated to {(item.IsActive ? "Active" : "Inactive")}.", IsActive = item.IsActive });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var item = await _context.Items.FindAsync(id);
            if (item == null)
                return NotFound(new { Message = "Item not found." });

            if (!await IsUserAuthorizedForShop(item.ShopId))
                return Forbid();

            _context.Items.Remove(item);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Item deleted successfully." });
        }

        private async Task<bool> IsUserAuthorizedForShop(int? targetShopId)
        {
            if (targetShopId == null)
            {
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
