﻿namespace ads.Models.Data
{
    public class Inventory
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string? Sku { get; set; }
        public string? Clubs { get; set; }
        public decimal Inv { get; set; }
    }
}
