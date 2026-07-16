using System;
using System.Collections.Generic;

namespace MenuQr.Models;

public partial class OrderDetail
{
    public int OrderDetailId { get; set; }

    public string OrderId { get; set; } = null!;

    public string DishId { get; set; } = null!;

    public string DishName { get; set; } = null!;

    public string CategoryName { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal BasePrice { get; set; }

    public int DiscountPercent { get; set; }

    public decimal PriceAfterDiscount { get; set; }

    public decimal TotalToppingPrice { get; set; }

    public string? SelectedOptionsJson { get; set; }

    public string? ItemNote { get; set; }

    public virtual Order Order { get; set; } = null!;
}
