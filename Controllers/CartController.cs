using MenuQr.Models.Mongo;
using MenuQr.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MenuQr.Controllers
{
    public class CartController : Controller
    {
        private readonly MongoDbService _mongoDb;
        private readonly OrderService _orderService;

        public CartController(MongoDbService mongoDb, OrderService orderService)
        {
            _mongoDb = mongoDb;
            _orderService = orderService;
        }

        // View Cart: /Cart
        public async Task<IActionResult> Index()
        {
            var tableNumber = HttpContext.Session.GetString("TableNumber");
            if (string.IsNullOrEmpty(tableNumber))
            {
                return RedirectToAction("Index", "Home");
            }

            var cart = await _orderService.GetActiveOrderByTableAsync(tableNumber);
            return View(cart);
        }

        // Add to Cart Action (POST)
        [HttpPost]
        public async Task<IActionResult> AddToCart(
            string dishId, 
            string selectedSize, 
            List<string> selectedToppings, 
            int quantity, 
            string? customerNote)
        {
            var tableNumber = HttpContext.Session.GetString("TableNumber");
            if (string.IsNullOrEmpty(tableNumber))
            {
                return RedirectToAction("Index", "Home");
            }

            var dish = await _mongoDb.Dishes.Find(d => d.Id == dishId).FirstOrDefaultAsync();
            if (dish == null)
            {
                return NotFound("Dish not found");
            }

            // 1. Calculate Option Price Adjustments
            double sizeAdjustment = 0;
            if (!string.IsNullOrEmpty(selectedSize))
            {
                var sizeOpt = dish.Sizes.FirstOrDefault(s => s.Name.Equals(selectedSize, StringComparison.OrdinalIgnoreCase));
                if (sizeOpt != null)
                {
                    sizeAdjustment = sizeOpt.PriceAdjustment;
                }
            }

            double toppingsSum = 0;
            if (selectedToppings != null && selectedToppings.Any())
            {
                foreach (var toppingName in selectedToppings)
                {
                    var toppingOpt = dish.Toppings.FirstOrDefault(t => t.Name.Equals(toppingName, StringComparison.OrdinalIgnoreCase));
                    if (toppingOpt != null)
                    {
                        toppingsSum += toppingOpt.Price;
                    }
                }
            }

            double finalUnitPrice = dish.Price + sizeAdjustment + toppingsSum;

            // 2. Map to ActiveOrderItem
            var cartItem = new ActiveOrderItem
            {
                DishId = dish.Id!,
                DishName = dish.Name,
                Quantity = quantity > 0 ? quantity : 1,
                BasePrice = dish.Price,
                SelectedSize = selectedSize ?? "Standard",
                SelectedToppings = selectedToppings ?? new List<string>(),
                CustomerNote = customerNote ?? string.Empty,
                Price = finalUnitPrice
            };

            // 3. Save to order/cart
            await _orderService.AddItemToCartAsync(tableNumber, cartItem);

            // Redirect back to menu with a success state
            TempData["SuccessMessage"] = $"{dish.Name} added to cart!";
            return RedirectToAction("Menu", "Home");
        }

        // Update Cart Item Quantity (POST)
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(
            string dishId, 
            string size, 
            List<string> toppings, 
            int quantity)
        {
            var tableNumber = HttpContext.Session.GetString("TableNumber");
            if (string.IsNullOrEmpty(tableNumber))
            {
                return RedirectToAction("Index", "Home");
            }

            await _orderService.UpdateCartItemQuantityAsync(tableNumber, dishId, size, toppings, quantity);
            return RedirectToAction(nameof(Index));
        }

        // Delete Cart Item (POST)
        [HttpPost]
        public async Task<IActionResult> RemoveItem(
            string dishId, 
            string size, 
            List<string> toppings)
        {
            var tableNumber = HttpContext.Session.GetString("TableNumber");
            if (string.IsNullOrEmpty(tableNumber))
            {
                return RedirectToAction("Index", "Home");
            }

            await _orderService.RemoveItemFromCartAsync(tableNumber, dishId, size, toppings);
            return RedirectToAction(nameof(Index));
        }

        // Apply Voucher Discount (POST)
        [HttpPost]
        public async Task<IActionResult> ApplyVoucher(string voucherCode)
        {
            var tableNumber = HttpContext.Session.GetString("TableNumber");
            if (string.IsNullOrEmpty(tableNumber))
            {
                return RedirectToAction("Index", "Home");
            }

            double discountPercent = 0;
            bool isValid = false;

            // Simple voucher rules for mockup verification
            if (voucherCode.Equals("HADILAO10", StringComparison.OrdinalIgnoreCase))
            {
                discountPercent = 10;
                isValid = true;
            }
            else if (voucherCode.Equals("WELCOME20", StringComparison.OrdinalIgnoreCase))
            {
                discountPercent = 20;
                isValid = true;
            }
            else if (voucherCode.Equals("STUDENT30", StringComparison.OrdinalIgnoreCase))
            {
                discountPercent = 30;
                isValid = true;
            }

            if (isValid)
            {
                await _orderService.ApplyVoucherAsync(tableNumber, voucherCode.ToUpper(), discountPercent);
                TempData["SuccessMessage"] = $"Promo code applied successfully! {discountPercent}% off.";
            }
            else
            {
                TempData["ErrorMessage"] = "Invalid voucher code.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Remove Applied Voucher
        public async Task<IActionResult> RemoveVoucher()
        {
            var tableNumber = HttpContext.Session.GetString("TableNumber");
            if (!string.IsNullOrEmpty(tableNumber))
            {
                await _orderService.RemoveVoucherAsync(tableNumber);
            }
            return RedirectToAction(nameof(Index));
        }

        // Checkout View: /Cart/Checkout
        public async Task<IActionResult> Checkout()
        {
            var tableNumber = HttpContext.Session.GetString("TableNumber");
            if (string.IsNullOrEmpty(tableNumber))
            {
                return RedirectToAction("Index", "Home");
            }

            var cart = await _orderService.GetActiveOrderByTableAsync(tableNumber);
            if (cart == null || !cart.Items.Any())
            {
                return RedirectToAction("Menu", "Home");
            }

            return View(cart);
        }

        // Submit Order to Kitchen (POST)
        [HttpPost]
        public async Task<IActionResult> SubmitOrder()
        {
            var tableNumber = HttpContext.Session.GetString("TableNumber");
            if (string.IsNullOrEmpty(tableNumber))
            {
                return RedirectToAction("Index", "Home");
            }

            var success = await _orderService.SubmitOrderAsync(tableNumber);
            if (success)
            {
                TempData["SuccessMessage"] = "Order placed! Preparing food soon...";
                return RedirectToAction("Tracking", "Order");
            }

            TempData["ErrorMessage"] = "Unable to place order. Your cart is empty.";
            return RedirectToAction(nameof(Index));
        }
    }
}
