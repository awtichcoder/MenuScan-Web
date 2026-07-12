using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace MenuQr.Models.Mongo
{
    public class ActiveOrder
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string TableNumber { get; set; } = string.Empty;

        // States: "Cart", "Pending", "Cooking", "Ready"
        public string Status { get; set; } = "Cart";

        public List<ActiveOrderItem> Items { get; set; } = new();

        public double SubTotal { get; set; }
        
        public double DiscountAmount { get; set; }
        
        public string VoucherCode { get; set; } = string.Empty;
        
        public double TotalAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool CallStaffRequest { get; set; } = false;
    }

    public class ActiveOrderItem
    {
        public string DishId { get; set; } = string.Empty;
        
        public string DishName { get; set; } = string.Empty;
        
        public int Quantity { get; set; }
        
        public double BasePrice { get; set; }
        
        public string SelectedSize { get; set; } = string.Empty;
        
        public List<string> SelectedToppings { get; set; } = new();
        
        public string CustomerNote { get; set; } = string.Empty;
        
        // Calculated final unit price for this item (BasePrice + Size adjustment + toppings sum)
        public double Price { get; set; }
    }
}
