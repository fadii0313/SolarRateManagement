using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarRateManagement.Infrastructure.Data;
using System.Threading.Tasks;

namespace SolarRateManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestDbController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TestDbController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var canConnect = await _context.Database.CanConnectAsync();
            if (!canConnect)
            {
                return StatusCode(500, new { success = false, message = "Cannot connect to the database." });
            }

            var categoriesCount = await _context.Categories.CountAsync();
            var itemsCount = await _context.Items.CountAsync();
            var usersCount = await _context.Users.CountAsync();

            return Ok(new
            {
                success = true,
                message = "Database connection successful and seeded.",
                data = new
                {
                    categoriesCount,
                    itemsCount,
                    usersCount
                }
            });
        }
    }
}
