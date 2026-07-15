// Models/Entities/Order.cs
// Map tới bảng SQL Server: Orders (xem Models/data/MenusDb.sql)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MenuQr.Models.Entities
{
    [Table("Orders")]
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        [MaxLength(20)]
        public string? TableNumber { get; set; }

        [Required]
        [MaxLength(50)]
        public string OrderType { get; set; } = null!;

        [MaxLength(50)]
        public string Status { get; set; } = "Completed";

        public DateTime CreatedAt { get; set; }

        // Điều hướng
        public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public Invoice? Invoice { get; set; }
    }
}
