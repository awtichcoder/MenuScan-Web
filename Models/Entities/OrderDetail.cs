// Models/Entities/OrderDetail.cs
// Map tới bảng SQL Server: OrderDetails (xem Models/data/MenusDb.sql)
// Lưu ý: DishId là chuỗi để khớp ObjectId của MongoDB (Dishes._id)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MenuQr.Models.Entities
{
    [Table("OrderDetails")]
    public class OrderDetail
    {
        [Key]
        public int OrderDetailId { get; set; }

        public int OrderId { get; set; }

        [Required]
        [MaxLength(50)]
        public string DishId { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string DishName { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string CategoryName { get; set; } = null!;

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BasePrice { get; set; }

        public int DiscountPercent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceAfterDiscount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalToppingPrice { get; set; }

        // Snapshot các option đã chọn (JSON) tại thời điểm order
        public string? SelectedOptionsJson { get; set; }

        [MaxLength(500)]
        public string? ItemNote { get; set; }

        // Điều hướng
        public Order Order { get; set; } = null!;
    }
}
