// Models/Entities/Invoice.cs
// Map tới bảng SQL Server: Invoices (xem Models/data/MenusDb.sql)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MenuQr.Models.Entities
{
    [Table("Invoices")]
    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }

        // OrderId là UNIQUE => quan hệ 1-1 với Orders (cấu hình trong AppDbContext)
        public int OrderId { get; set; }

        // CashierId nullable => hóa đơn có thể chưa gán thu ngân
        public int? CashierId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDiscount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FinalAmount { get; set; }

        [Required]
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = null!;

        [MaxLength(50)]
        public string PaymentStatus { get; set; } = "Paid";

        public DateTime PaidAt { get; set; }

        // Điều hướng
        public Order Order { get; set; } = null!;
        public User? Cashier { get; set; }
    }
}
