// Models/Entities/User.cs
// Map tới bảng SQL Server: Users (xem Models/data/MenusDb.sql)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MenuQr.Models.Entities
{
    [Table("Users")]
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string FullName { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = null!;

        public bool IsActive { get; set; } = true;

        // Điều hướng: 1 nhân viên (thu ngân) lập nhiều hóa đơn
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }
}
