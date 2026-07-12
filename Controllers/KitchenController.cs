using MenuQr.Models.Mongo;
using MenuQr.Services;
using MenuQr.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MenuQr.Controllers
{
    // Restrict access to staff members in roles Kitchen or Admin
    [Authorize(Roles = "Kitchen,Admin")]
    public class KitchenController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public KitchenController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        // Live Kitchen Console Dashboard: /Kitchen
        public async Task<IActionResult> Index(string? search, string? filter, bool groupByCategory)
        {
            var filterStr = filter ?? "Active";

            // 1. Fetch all non-cart active orders
            var orderFilter = Builders<ActiveOrder>.Filter.Ne(o => o.Status, "Cart");
            var allActiveOrders = await _mongoDb.ActiveOrders.Find(orderFilter).ToListAsync();

            // 2. Compute Summary Dashboard KPIs
            int pending = allActiveOrders.Count(o => o.Status == "Pending");
            int cooking = allActiveOrders.Count(o => o.Status == "Cooking");
            int ready = allActiveOrders.Count(o => o.Status == "Ready");
            
            // Delayed: orders older than 5 minutes still in Pending or Cooking states
            int delayed = allActiveOrders.Count(o => 
                (o.Status == "Pending" || o.Status == "Cooking") && 
                (DateTime.UtcNow - o.CreatedAt).TotalMinutes >= 5
            );

            // 3. Filter orders based on status tab selection
            var filteredOrders = allActiveOrders.AsEnumerable();
            if (filterStr == "Active")
            {
                filteredOrders = filteredOrders.Where(o => o.Status == "Pending" || o.Status == "Cooking");
            }
            else if (filterStr == "Pending")
            {
                filteredOrders = filteredOrders.Where(o => o.Status == "Pending");
            }
            else if (filterStr == "Cooking")
            {
                filteredOrders = filteredOrders.Where(o => o.Status == "Cooking");
            }
            else if (filterStr == "Ready")
            {
                filteredOrders = filteredOrders.Where(o => o.Status == "Ready");
            }
            else if (filterStr == "Delayed")
            {
                filteredOrders = filteredOrders.Where(o => 
                    (o.Status == "Pending" || o.Status == "Cooking") && 
                    (DateTime.UtcNow - o.CreatedAt).TotalMinutes >= 5
                );
            }

            // 4. Search Filter
            if (!string.IsNullOrEmpty(search))
            {
                var query = search.Trim().ToLower();
                filteredOrders = filteredOrders.Where(o => 
                    o.TableNumber.Contains(query) || 
                    (o.Id != null && o.Id.Contains(query)) ||
                    o.Items.Any(i => i.DishName.ToLower().Contains(query))
                );
            }

            // 5. Order Queue Sorting: Oldest first (FIFO)
            var sortedOrders = filteredOrders.OrderBy(o => o.CreatedAt).ToList();

            var viewModel = new KitchenDashboardViewModel
            {
                ActiveOrders = sortedOrders,
                PendingCount = pending,
                CookingCount = cooking,
                ReadyCount = ready,
                DelayedCount = delayed,
                SearchQuery = search ?? string.Empty,
                SelectedFilter = filterStr,
                GroupByCategory = groupByCategory
            };

            return View(viewModel);
        }

        // Live Order Preparation Status Update: /Kitchen/UpdateStatus (POST)
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(string orderId, string newStatus)
        {
            var update = Builders<ActiveOrder>.Update.Set(o => o.Status, newStatus);
            var result = await _mongoDb.ActiveOrders.UpdateOneAsync(o => o.Id == orderId, update);

            if (result.ModifiedCount > 0)
            {
                return Json(new { success = true, message = $"Status updated to {newStatus}" });
            }
            return Json(new { success = false, message = "Order not found or not modified" });
        }

        // Food Availability Settings View: /Kitchen/Dishes
        public async Task<IActionResult> Dishes(string? categoryId, string? search)
        {
            var categories = await _mongoDb.Categories.Find(_ => true).ToListAsync();
            
            var dishFilter = Builders<Dish>.Filter.Empty;
            if (!string.IsNullOrEmpty(categoryId))
            {
                dishFilter &= Builders<Dish>.Filter.Eq(d => d.CategoryId, categoryId);
            }
            if (!string.IsNullOrEmpty(search))
            {
                dishFilter &= Builders<Dish>.Filter.Regex(d => d.Name, new MongoDB.Bson.BsonRegularExpression(search, "i"));
            }

            var dishes = await _mongoDb.Dishes.Find(dishFilter).ToListAsync();

            var viewModel = new KitchenDishesViewModel
            {
                Dishes = dishes,
                Categories = categories,
                SelectedCategoryId = categoryId ?? string.Empty,
                SearchQuery = search ?? string.Empty
            };

            return View(viewModel);
        }

        // Toggle Dish Availability: /Kitchen/ToggleAvailability (POST)
        [HttpPost]
        public async Task<IActionResult> ToggleAvailability(string dishId)
        {
            var dish = await _mongoDb.Dishes.Find(d => d.Id == dishId).FirstOrDefaultAsync();
            if (dish == null)
            {
                return Json(new { success = false, message = "Dish not found" });
            }

            var update = Builders<Dish>.Update.Set(d => d.Available, !dish.Available);
            await _mongoDb.Dishes.UpdateOneAsync(d => d.Id == dishId, update);

            return Json(new { success = true, isAvailable = !dish.Available });
        }
    }
}
