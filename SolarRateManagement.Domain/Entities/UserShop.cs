namespace SolarRateManagement.Domain.Entities
{
    public class UserShop
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public int ShopId { get; set; }
        public virtual Shop Shop { get; set; } = null!;

        public string RoleInShop { get; set; } = string.Empty; // e.g. ShopAdmin, ShopUser
        public bool IsActive { get; set; } = true;
    }
}
