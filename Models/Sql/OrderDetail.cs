namespace MenuQr.Models.Sql
{
    public class OrderDetail
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public virtual Order? Order { get; set; }

        public string DishId { get; set; } = string.Empty; // MongoDB Dish reference string

        public string DishName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal BasePrice { get; set; }

        public decimal Discount { get; set; }

        public string SelectedOptions { get; set; } = string.Empty; // e.g. "Size: L"

        public string SelectedToppings { get; set; } = string.Empty; // e.g. "Extra Cheese, Pearls"

        public string CustomerNote { get; set; } = string.Empty;
    }
}
