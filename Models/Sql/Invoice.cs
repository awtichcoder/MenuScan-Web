using System;

namespace MenuQr.Models.Sql
{
    public class Invoice
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public virtual Order? Order { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        public decimal Total { get; set; }

        public decimal Discount { get; set; }

        public decimal FinalAmount { get; set; }

        public string CashierUsername { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = string.Empty; // Cash, BankTransfer, Card
    }
}
