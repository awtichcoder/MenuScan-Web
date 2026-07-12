using MenuQr.Models.Mongo;
using System.Collections.Generic;

namespace MenuQr.ViewModels
{
    public class KitchenDashboardViewModel
    {
        public List<ActiveOrder> ActiveOrders { get; set; } = new();
        public int PendingCount { get; set; }
        public int CookingCount { get; set; }
        public int ReadyCount { get; set; }
        public int DelayedCount { get; set; }
        
        // Search & Filter state
        public string SearchQuery { get; set; } = string.Empty;
        public string SelectedFilter { get; set; } = "Active"; // Active, Pending, Cooking, Ready
        public bool GroupByCategory { get; set; }
    }
}
