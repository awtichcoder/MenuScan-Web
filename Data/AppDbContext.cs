// Data/AppDbContext.cs
// DbContext cho SQL Server (MenuDb) - map vào schema có sẵn ở Models/data/MenusDb.sql
using Microsoft.EntityFrameworkCore;
using MenuQr.Models.Entities;

namespace MenuQr.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Users: Username là UNIQUE, IsActive mặc định 1
            modelBuilder.Entity<User>(e =>
            {
                e.HasIndex(u => u.Username).IsUnique();
                e.Property(u => u.IsActive).HasDefaultValue(true);
            });

            // Orders: Status mặc định 'Completed', CreatedAt mặc định GETDATE()
            modelBuilder.Entity<Order>(e =>
            {
                e.Property(o => o.Status).HasDefaultValue("Completed");
                e.Property(o => o.CreatedAt).HasDefaultValueSql("GETDATE()");
            });

            // Orders 1 - N OrderDetails, xóa Order thì cascade OrderDetails
            modelBuilder.Entity<OrderDetail>(e =>
            {
                e.HasOne(d => d.Order)
                 .WithMany(o => o.OrderDetails)
                 .HasForeignKey(d => d.OrderId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // Invoices: OrderId UNIQUE => 1-1 với Orders; PaymentStatus/PaidAt có default
            modelBuilder.Entity<Invoice>(e =>
            {
                e.HasIndex(i => i.OrderId).IsUnique();
                e.Property(i => i.PaymentStatus).HasDefaultValue("Paid");
                e.Property(i => i.PaidAt).HasDefaultValueSql("GETDATE()");

                // Orders 1 - 1 Invoice (không cascade để giữ đúng schema gốc)
                e.HasOne(i => i.Order)
                 .WithOne(o => o.Invoice)
                 .HasForeignKey<Invoice>(i => i.OrderId)
                 .OnDelete(DeleteBehavior.Restrict);

                // Users 1 - N Invoices, CashierId nullable => không cascade
                e.HasOne(i => i.Cashier)
                 .WithMany(u => u.Invoices)
                 .HasForeignKey(i => i.CashierId)
                 .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
