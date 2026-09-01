using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarRateManagement.Application.Common.Interfaces;
using SolarRateManagement.Application.DTOs.Shop;
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

            IQueryable<Shop> query;

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

        [HttpPost]
        public async Task<IActionResult> CreateShop([FromBody] CreateShopDto dto)
        {
            if (!_shopContext.IsSuperAdmin)
                return Forbid();

            var shop = new Shop
            {
                Name = dto.Name.Trim(),
                OwnerName = dto.OwnerName?.Trim() ?? "",
                ContactNumber = dto.ContactNumber?.Trim() ?? "",
                Email = dto.Email?.Trim() ?? "",
                City = dto.City.Trim(),
                Address = dto.Address?.Trim() ?? "",
                IsActive = dto.IsActive,
                CreatedDate = DateTime.UtcNow
            };

            _context.Shops.Add(shop);
            await _context.SaveChangesAsync();

            return Ok(shop);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateShop(int id, [FromBody] CreateShopDto dto)
        {
            if (!_shopContext.IsSuperAdmin)
                return Forbid();

            var shop = await _context.Shops.FindAsync(id);
            if (shop == null)
                return NotFound(new { Message = "Shop not found." });

            shop.Name = dto.Name.Trim();
            shop.OwnerName = dto.OwnerName?.Trim() ?? "";
            shop.ContactNumber = dto.ContactNumber?.Trim() ?? "";
            shop.Email = dto.Email?.Trim() ?? "";
            shop.City = dto.City.Trim();
            shop.Address = dto.Address?.Trim() ?? "";
            shop.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();
            return Ok(shop);
        }

        [HttpPut("{id}/toggle-status")]
        public async Task<IActionResult> ToggleShopStatus(int id)
        {
            if (!_shopContext.IsSuperAdmin)
                return Forbid();

            var shop = await _context.Shops.FindAsync(id);
            if (shop == null)
                return NotFound(new { Message = "Shop not found." });

            shop.IsActive = !shop.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"Shop status updated to {(shop.IsActive ? "Active" : "Inactive")}.", IsActive = shop.IsActive });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteShop(int id)
        {
            if (!_shopContext.IsSuperAdmin)
                return Forbid();

            var shop = await _context.Shops.FindAsync(id);
            if (shop == null)
                return NotFound(new { Message = "Shop not found." });

            _context.Shops.Remove(shop);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Shop deleted successfully." });
        }
    }
}
