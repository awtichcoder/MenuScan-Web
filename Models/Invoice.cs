using System;
using System.Collections.Generic;

namespace MenuQr.Models;

public partial class Invoice
{
    public int InvoiceId { get; set; }

    public int OrderId { get; set; }

    public int? CashierId { get; set; }

    public decimal SubTotal { get; set; }

    public decimal TotalDiscount { get; set; }

    public decimal FinalAmount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string? PaymentStatus { get; set; }

    public DateTime? PaidAt { get; set; }

    public virtual User? Cashier { get; set; }

    public virtual Order Order { get; set; } = null!;
}
