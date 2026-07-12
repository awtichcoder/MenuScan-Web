using System;
using System.Collections.Generic;

namespace MenuQr.Models.Sql
{
    public class Order
    {
        public int Id { get; set; }

        public string TableNumber { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public decimal SubTotal { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal TotalAmount { get; set; }

        // Typically "Completed" or "Cancelled" for historical records
        public string Status { get; set; } = "Completed";

        public string Note { get; set; } = string.Empty;

        // Relationships
        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        
        public virtual Invoice? Invoice { get; set; }
    }
}
