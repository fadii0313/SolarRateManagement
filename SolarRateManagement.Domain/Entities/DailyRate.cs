using System;

namespace SolarRateManagement.Domain.Entities
{
    public class DailyRate
    {
        public int Id { get; set; }
        public int ShopId { get; set; }
        public virtual Shop Shop { get; set; } = null!;

        public int ItemId { get; set; }
        public virtual Item Item { get; set; } = null!;

        public decimal Rate { get; set; }
        public DateTime RateDate { get; set; }
        public string Remarks { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? ModifiedBy { get; set; }
    }
}
