using Microsoft.AspNetCore.Http;
using SolarRateManagement.Application.Common.Interfaces;
using System.Security.Claims;

namespace SolarRateManagement.Infrastructure.Services
{
    public class ShopContext : IShopContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ShopContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int? CurrentShopId
        {
            get
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext == null) return null;

                if (httpContext.Request.Headers.TryGetValue("X-Shop-Id", out var shopIdStr))
                {
                    if (int.TryParse(shopIdStr, out var shopId))
                    {
                        return shopId;
                    }
                }
                return null;
            }
        }

        public int? CurrentUserId
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdStr = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out var userId))
                {
                    return userId;
                }
                return null;
            }
        }

        public bool IsSuperAdmin
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;
                return user?.IsInRole("SuperAdmin") ?? false;
            }
        }
    }
}
