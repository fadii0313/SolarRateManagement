using System;
using System.Collections.Generic;

namespace SolarRateManagement.Domain.Entities
{
    public class Item
    {
        public int Id { get; set; }
        public int? ShopId { get; set; }
        public virtual Shop? Shop { get; set; }

        public int CategoryId { get; set; }
        public virtual Category Category { get; set; } = null!;

        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
        public bool IsDeleted { get; set; }

        // Navigation properties
        public virtual ICollection<DailyRate> DailyRates { get; set; } = new List<DailyRate>();
    }
}
