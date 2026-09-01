using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarRateManagement.Application.Common.Interfaces;
using SolarRateManagement.Domain.Entities;
using SolarRateManagement.Infrastructure.Data;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace SolarRateManagement.API.Controllers
{
    public class CategoryDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; } = 0;
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public CategoriesController(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var cats = await _context.Categories
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Description,
                    c.IsActive,
                    c.DisplayOrder,
                    ItemCount = c.Items.Count(i => i.IsActive)
                })
                .ToListAsync();
            return Ok(cats);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryDto dto)
        {
            if (!_shopContext.IsSuperAdmin)
                return Forbid();

            var category = new Category
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim() ?? "",
                IsActive = dto.IsActive,
                DisplayOrder = dto.DisplayOrder
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return Ok(category);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryDto dto)
        {
            if (!_shopContext.IsSuperAdmin)
                return Forbid();

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return NotFound(new { Message = "Category not found." });

            category.Name = dto.Name.Trim();
            category.Description = dto.Description?.Trim() ?? "";
            category.IsActive = dto.IsActive;
            category.DisplayOrder = dto.DisplayOrder;

            await _context.SaveChangesAsync();
            return Ok(category);
        }

        [HttpPut("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            if (!_shopContext.IsSuperAdmin)
                return Forbid();

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return NotFound(new { Message = "Category not found." });

            category.IsActive = !category.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"Category status updated to {(category.IsActive ? "Active" : "Inactive")}.", IsActive = category.IsActive });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            if (!_shopContext.IsSuperAdmin)
                return Forbid();

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return NotFound(new { Message = "Category not found." });

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Category deleted successfully." });
        }
    }
}
