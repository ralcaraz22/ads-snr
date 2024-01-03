﻿namespace ads.Models.Dto.PowerBiAdsDto
{
    public class PowerBiAdsChainDto
    {
        public int Divisor { get; set; }
        public decimal Ads { get; set; }
        public string? Sku { get; set; }
        public decimal Sales { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public int OutOfStockDaysCount { get; set; }
    }
}
