namespace SolarRateManagement.Application.DTOs.Rate
{
    public class DailyRateResponseDto
    {
        public int ItemId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        
        public int? RateId { get; set; }
        public decimal Rate { get; set; }
        public decimal YesterdayRate { get; set; }
        public string Remarks { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
    }
}
