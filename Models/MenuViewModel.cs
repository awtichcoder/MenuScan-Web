// Models/MenuViewModel.cs
using MenuQr.Areas.Admin.Models;

namespace MenuQr.Models
{
    public class MenuViewModel
    {
        public List<Category> Categories { get; set; } = new();
        public List<Dish> Dishes { get; set; } = new();
        public string CurrentCategoryId { get; set; } = "all";
        public string CurrentCategoryName { get; set; } = "Đặt đề cử";
    }
}