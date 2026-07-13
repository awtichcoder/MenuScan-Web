using System;
using System.Collections.Generic;

namespace MenuQr.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public string? TableNumber { get; set; }

    public string OrderType { get; set; } = null!;

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Invoice? Invoice { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
