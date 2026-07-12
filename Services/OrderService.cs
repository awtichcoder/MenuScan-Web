using MenuQr.Models.Mongo;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MenuQr.Services
{
    public class OrderService
    {
        private readonly MongoDbService _mongoDb;

        public OrderService(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        // Get the active order/cart for a table
        public async Task<ActiveOrder?> GetActiveOrderByTableAsync(string tableNumber)
        {
            return await _mongoDb.ActiveOrders
                .Find(o => o.TableNumber == tableNumber)
                .FirstOrDefaultAsync();
        }

        // Get or create an active order with status "Cart"
        public async Task<ActiveOrder> GetOrCreateCartAsync(string tableNumber)
        {
            var activeOrder = await GetActiveOrderByTableAsync(tableNumber);
            
            if (activeOrder == null)
            {
                activeOrder = new ActiveOrder
                {
                    TableNumber = tableNumber,
                    Status = "Cart",
                    Items = new List<ActiveOrderItem>(),
                    CreatedAt = DateTime.UtcNow
                };
                await _mongoDb.ActiveOrders.InsertOneAsync(activeOrder);
            }
            
            return activeOrder;
        }

        // Add item to cart and calculate totals
        public async Task AddItemToCartAsync(string tableNumber, ActiveOrderItem newItem)
        {
            var cart = await GetOrCreateCartAsync(tableNumber);

            // Check if exact same item (same dish, size, toppings) already exists in the cart
            var existingItem = cart.Items.FirstOrDefault(i => 
                i.DishId == newItem.DishId && 
                i.SelectedSize == newItem.SelectedSize && 
                AreToppingsEqual(i.SelectedToppings, newItem.SelectedToppings)
            );

            if (existingItem != null)
            {
                existingItem.Quantity += newItem.Quantity;
                existingItem.CustomerNote = string.IsNullOrEmpty(newItem.CustomerNote) 
                    ? existingItem.CustomerNote 
                    : newItem.CustomerNote;
            }
            else
            {
                cart.Items.Add(newItem);
            }

            RecalculateTotals(cart);

            // Save back to MongoDB
            await _mongoDb.ActiveOrders.ReplaceOneAsync(o => o.Id == cart.Id, cart);
        }

        // Update item quantity
        public async Task UpdateCartItemQuantityAsync(string tableNumber, string dishId, string size, List<string> toppings, int quantity)
        {
            var cart = await GetActiveOrderByTableAsync(tableNumber);
            if (cart == null) return;

            var item = cart.Items.FirstOrDefault(i => 
                i.DishId == dishId && 
                i.SelectedSize == size && 
                AreToppingsEqual(i.SelectedToppings, toppings)
            );

            if (item != null)
            {
                if (quantity <= 0)
                {
                    cart.Items.Remove(item);
                }
                else
                {
                    item.Quantity = quantity;
                }

                RecalculateTotals(cart);
                await _mongoDb.ActiveOrders.ReplaceOneAsync(o => o.Id == cart.Id, cart);
            }
        }

        // Remove item from cart
        public async Task RemoveItemFromCartAsync(string tableNumber, string dishId, string size, List<string> toppings)
        {
            await UpdateCartItemQuantityAsync(tableNumber, dishId, size, toppings, 0);
        }

        // Apply discount voucher
        public async Task<bool> ApplyVoucherAsync(string tableNumber, string code, double discountPercent)
        {
            var cart = await GetActiveOrderByTableAsync(tableNumber);
            if (cart == null) return false;

            cart.VoucherCode = code;
            cart.DiscountAmount = Math.Round(cart.SubTotal * (discountPercent / 100.0));
            cart.TotalAmount = Math.Max(0, cart.SubTotal - cart.DiscountAmount);

            await _mongoDb.ActiveOrders.ReplaceOneAsync(o => o.Id == cart.Id, cart);
            return true;
        }

        // Remove voucher discount
        public async Task RemoveVoucherAsync(string tableNumber)
        {
            var cart = await GetActiveOrderByTableAsync(tableNumber);
            if (cart == null) return;

            cart.VoucherCode = string.Empty;
            cart.DiscountAmount = 0;
            cart.TotalAmount = cart.SubTotal;

            await _mongoDb.ActiveOrders.ReplaceOneAsync(o => o.Id == cart.Id, cart);
        }

        // Submit order (from "Cart" to "Pending")
        public async Task<bool> SubmitOrderAsync(string tableNumber)
        {
            var cart = await GetActiveOrderByTableAsync(tableNumber);
            if (cart == null || !cart.Items.Any()) return false;

            cart.Status = "Pending";
            cart.CreatedAt = DateTime.UtcNow;

            await _mongoDb.ActiveOrders.ReplaceOneAsync(o => o.Id == cart.Id, cart);
            return true;
        }

        // Call staff
        public async Task CallStaffAsync(string tableNumber)
        {
            var cart = await GetOrCreateCartAsync(tableNumber);
            cart.CallStaffRequest = true;
            await _mongoDb.ActiveOrders.ReplaceOneAsync(o => o.Id == cart.Id, cart);
        }

        // Reset call staff status
        public async Task ResetCallStaffAsync(string tableNumber)
        {
            var activeOrder = await GetActiveOrderByTableAsync(tableNumber);
            if (activeOrder != null)
            {
                activeOrder.CallStaffRequest = false;
                await _mongoDb.ActiveOrders.ReplaceOneAsync(o => o.Id == activeOrder.Id, activeOrder);
            }
        }

        // Helper: Check if lists of toppings match exactly
        private bool AreToppingsEqual(List<string> list1, List<string> list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            var sorted1 = list1.OrderBy(t => t).ToList();
            var sorted2 = list2.OrderBy(t => t).ToList();

            return sorted1.SequenceEqual(sorted2);
        }

        // Helper: Recalculate cart totals
        private void RecalculateTotals(ActiveOrder cart)
        {
            cart.SubTotal = cart.Items.Sum(i => i.Price * i.Quantity);
            
            // Reapply discount percentage based on voucher if exists (we will resolve voucher discount dynamically)
            if (!string.IsNullOrEmpty(cart.VoucherCode))
            {
                // Note: DiscountAmount recalculation is handled in ApplyVoucherAsync, 
                // but if items change, we scale the discount accordingly.
                // Assuming voucher stays applied at current rate, we'll keep it proportional.
                var oldSubtotal = cart.SubTotal;
                if (oldSubtotal > 0)
                {
                    // For simplicity, we recalculate using a hardcoded/mocked rate or keep current discount capped
                    cart.TotalAmount = Math.Max(0, cart.SubTotal - cart.DiscountAmount);
                }
            }
            else
            {
                cart.DiscountAmount = 0;
                cart.TotalAmount = cart.SubTotal;
            }
        }
    }
}
