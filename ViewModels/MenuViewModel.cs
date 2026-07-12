using MenuQr.Models.Mongo;
using System.Collections.Generic;

namespace MenuQr.ViewModels
{
    public class MenuViewModel
    {
        public List<Category> Categories { get; set; } = new();
        public List<Dish> Dishes { get; set; } = new();
        public string SelectedCategoryId { get; set; } = string.Empty;
        public string SearchQuery { get; set; } = string.Empty;
        public string TableNumber { get; set; } = string.Empty;
        public ActiveOrder? ActiveOrder { get; set; }
    }
}
