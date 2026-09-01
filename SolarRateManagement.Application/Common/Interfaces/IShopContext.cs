namespace SolarRateManagement.Application.Common.Interfaces
{
    public interface IShopContext
    {
        int? CurrentShopId { get; }
        bool IsSuperAdmin { get; }
        int? CurrentUserId { get; }
    }
}
