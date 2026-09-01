using System;
using System.Collections.Generic;

namespace SolarRateManagement.Domain.Entities
{
    public class Shop
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? NtnNumber { get; set; }
        public string? RegistrationNumber { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }

        // Navigation properties
        public virtual ICollection<UserShop> UserShops { get; set; } = new List<UserShop>();
        public virtual ICollection<Item> Items { get; set; } = new List<Item>();
        public virtual ICollection<DailyRate> DailyRates { get; set; } = new List<DailyRate>();
    }
}
