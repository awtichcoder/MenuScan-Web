using MenuQr.Models.Mongo;

namespace MenuQr.ViewModels
{
    public class DishDetailViewModel
    {
        public Dish Dish { get; set; } = new();
        public string TableNumber { get; set; } = string.Empty;
    }
}
