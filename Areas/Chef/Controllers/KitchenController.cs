using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Microsoft.AspNetCore.SignalR;
using MenuQr.Hubs;
using MenuQr.Models;
using MenuQr.Areas.Admin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MenuQr.Areas.Chef.Controllers
{
    [Area("Chef")]
    public class KitchenController : KitchenBaseController
    {
        private readonly IMongoCollection<ActiveOrder> _orderCollection;
        private readonly IMongoCollection<Dish> _dishCollection;
        private readonly IMongoCollection<Category> _categoryCollection;
        private readonly IHubContext<StaffHub> _staffHub;

        public KitchenController(IMongoDatabase mongoDatabase, IHubContext<StaffHub> staffHub)
        {
            _orderCollection = mongoDatabase.GetCollection<ActiveOrder>("ActiveOrders");
            _dishCollection = mongoDatabase.GetCollection<Dish>("Dishes");
            _categoryCollection = mongoDatabase.GetCollection<Category>("Categories");
            _staffHub = staffHub;
        }

        // 1. GET: /Chef/Kitchen/Index (Cooking Queue)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Fetch all orders that are currently "Serving"
            var activeOrders = await _orderCollection
                .Find(o => o.Status == "Serving")
                .ToListAsync();

            // Extract all items that are in Ordered, Cooking, or Served status
            var queueItems = new List<KitchenQueueItemViewModel>();

            foreach (var order in activeOrders)
            {
                if (order.Items == null) continue;

                foreach (var item in order.Items)
                {
                    if (item.ItemStatus == "Ordered" || item.ItemStatus == "Cooking" || item.ItemStatus == "Served")
                    {
                        queueItems.Add(new KitchenQueueItemViewModel
                        {
                            OrderId = order.Id ?? "",
                            TableNumber = order.TableNumber,
                            CartItemId = item.CartItemId,
                            DishId = item.DishId,
                            DishName = item.DishName,
                            ImageUrl = item.ImageUrl,
                            Quantity = item.Quantity,
                            BasePrice = item.BasePrice,
                            FinalPrice = item.FinalPrice,
                            SelectedOptions = item.SelectedOptions,
                            Note = item.Note,
                            ItemStatus = item.ItemStatus,
                            OrderedAt = item.OrderedAt ?? order.CreatedAt
                        });
                    }
                }
            }

            // Sort by ordered time (FIFO)
            queueItems = queueItems.OrderBy(i => i.OrderedAt).ToList();

            return View(queueItems);
        }

        // 2. POST: /Chef/Kitchen/UpdateItemStatus
        [HttpPost]
        public async Task<IActionResult> UpdateItemStatus(string orderId, string cartItemId, string newStatus)
        {
            if (string.IsNullOrEmpty(orderId) || string.IsNullOrEmpty(cartItemId) || string.IsNullOrEmpty(newStatus))
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });
            }

            // Find the active order
            var order = await _orderCollection.Find(o => o.Id == orderId && o.Status == "Serving").FirstOrDefaultAsync();
            if (order == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn hàng hoặc bàn đã thanh toán!" });
            }

            // Locate and update the item status
            var item = order.Items.FirstOrDefault(i => i.CartItemId == cartItemId);
            if (item == null)
            {
                return Json(new { success = false, message = "Không tìm thấy món ăn trong đơn hàng!" });
            }

            item.ItemStatus = newStatus;
            order.UpdatedAt = DateTime.Now;

            var update = Builders<ActiveOrder>.Update
                .Set(o => o.Items, order.Items)
                .Set(o => o.UpdatedAt, order.UpdatedAt);

            var result = await _orderCollection.UpdateOneAsync(o => o.Id == orderId, update);

            if (result.ModifiedCount > 0)
            {
                // Notify other clients via SignalR (Staff screen and other Kitchen displays)
                await _staffHub.Clients.All.SendAsync("OrderUpdated", new { orderId, cartItemId, status = newStatus, tableNumber = order.TableNumber });
                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Cập nhật thất bại hoặc dữ liệu không đổi!" });
        }

        // 3. GET: /Chef/Kitchen/Batch (Smart Grouping)
        [HttpGet]
        public async Task<IActionResult> Batch()
        {
            var activeOrders = await _orderCollection
                .Find(o => o.Status == "Serving")
                .ToListAsync();

            var orderedItems = new List<ActiveOrderItem>();
            foreach (var order in activeOrders)
            {
                if (order.Items == null) continue;
                orderedItems.AddRange(order.Items.Where(i => i.ItemStatus == "Ordered"));
            }

            // We want to group items by: DishId + specific options JSON, to display them nicely
            // We also group by CategoryName to satisfy the request "Gom món theo danh mục"
            var groupedList = new List<BatchGroupViewModel>();

            // Group by DishId first
            var dishesGroups = orderedItems.GroupBy(i => i.DishId).ToList();

            foreach (var group in dishesGroups)
            {
                // Get dish info
                var firstItem = group.First();
                
                // Group subgroups by SelectedOptions names combined
                var subGroups = group.GroupBy(i => GetOptionsSummaryString(i.SelectedOptions)).ToList();

                foreach (var subGroup in subGroups)
                {
                    var optText = subGroup.Key;
                    int totalQty = subGroup.Sum(i => i.Quantity);

                    // Find tables details
                    var tablesList = subGroup.Select(i => {
                        // Find table for this cart item
                        var orderForTable = activeOrders.FirstOrDefault(o => o.Items.Any(item => item.CartItemId == i.CartItemId));
                        return new BatchTableDetail
                        {
                            TableNumber = orderForTable?.TableNumber ?? "K/H",
                            Quantity = i.Quantity,
                            Note = i.Note,
                            OrderedAt = i.OrderedAt ?? orderForTable?.CreatedAt ?? DateTime.Now
                        };
                    }).ToList();

                    groupedList.Add(new BatchGroupViewModel
                    {
                        DishId = group.Key,
                        DishName = firstItem.DishName,
                        OptionsText = optText,
                        TotalQuantity = totalQty,
                        TableDetails = tablesList
                    });
                }
            }

            // Sort grouped list by name
            groupedList = groupedList.OrderBy(g => g.DishName).ToList();

            return View(groupedList);
        }

        private string GetOptionsSummaryString(List<SelectedOption> options)
        {
            if (options == null || !options.Any()) return "Mặc định";
            return string.Join(" • ", options.Select(o => o.Name));
        }

        // 4. GET: /Chef/Kitchen/Dishes (Manage Dish Availability)
        [HttpGet]
        public async Task<IActionResult> Dishes()
        {
            var categories = await _categoryCollection.Find(c => c.IsActive).ToListAsync();
            var dishes = await _dishCollection.Find(_ => true).ToListAsync();

            ViewBag.Categories = categories;
            return View(dishes);
        }

        // 5. POST: /Chef/Kitchen/ToggleDishAvailability
        [HttpPost]
        public async Task<IActionResult> ToggleDishAvailability(string dishId, bool isAvailable)
        {
            if (string.IsNullOrEmpty(dishId))
            {
                return Json(new { success = false, message = "Mã món ăn không hợp lệ!" });
            }

            var update = Builders<Dish>.Update.Set(d => d.IsAvailable, isAvailable);
            var result = await _dishCollection.UpdateOneAsync(d => d.Id == dishId, update);

            if (result.ModifiedCount > 0)
            {
                // Trigger real-time reload for menu pages
                await _staffHub.Clients.All.SendAsync("DishAvailabilityChanged", new { dishId, isAvailable });
                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Không thể cập nhật hoặc món ăn không tồn tại!" });
        }
    }

    // --- VIEW MODELS FOR KITCHEN DASHBOARD ---

    public class KitchenQueueItemViewModel
    {
        public string OrderId { get; set; } = null!;
        public string TableNumber { get; set; } = null!;
        public string CartItemId { get; set; } = null!;
        public string DishId { get; set; } = null!;
        public string DishName { get; set; } = null!;
        public string ImageUrl { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal BasePrice { get; set; }
        public decimal FinalPrice { get; set; }
        public List<SelectedOption> SelectedOptions { get; set; } = new();
        public string Note { get; set; } = string.Empty;
        public string ItemStatus { get; set; } = null!;
        public DateTime OrderedAt { get; set; }
    }

    public class BatchGroupViewModel
    {
        public string DishId { get; set; } = null!;
        public string DishName { get; set; } = null!;
        public string OptionsText { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public List<BatchTableDetail> TableDetails { get; set; } = new();
    }

    public class BatchTableDetail
    {
        public string TableNumber { get; set; } = null!;
        public int Quantity { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime OrderedAt { get; set; }
    }
}
