using MenuQr.Data;
using MenuQr.Models.Mongo;
using MenuQr.Models.Sql;
using MenuQr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MenuQr.Controllers
{
    [Authorize(Roles = "Cashier,Admin")]
    public class CashierController : Controller
    {
        private readonly MongoDbService _mongoDb;
        private readonly MenuDbContext _sqlContext;
        private readonly OrderService _orderService;

        public CashierController(MongoDbService mongoDb, MenuDbContext sqlContext, OrderService orderService)
        {
            _mongoDb = mongoDb;
            _sqlContext = sqlContext;
            _orderService = orderService;
        }

        // Live Table Layout Grid: /Cashier
        public async Task<IActionResult> Index()
        {
            // 1. Fetch active orders from MongoDB
            var activeOrders = await _mongoDb.ActiveOrders.Find(o => o.Status != "Cart").ToListAsync();

            // Mock 10 tables for restaurant layout
            var tablesList = new List<TableStatusDto>();
            for (int i = 1; i <= 10; i++)
            {
                var tableNum = i.ToString("D2");
                var activeOrder = activeOrders.FirstOrDefault(o => o.TableNumber == tableNum);

                var tableDto = new TableStatusDto
                {
                    TableNumber = tableNum,
                    Status = "Available", // Available, Dining, Billing, CallStaff
                    TotalAmount = 0
                };

                if (activeOrder != null)
                {
                    tableDto.ActiveOrderId = activeOrder.Id;
                    tableDto.TotalAmount = activeOrder.TotalAmount;

                    if (activeOrder.CallStaffRequest)
                    {
                        tableDto.Status = "CallStaff";
                    }
                    else if (activeOrder.Status == "Ready")
                    {
                        tableDto.Status = "Billing";
                    }
                    else
                    {
                        tableDto.Status = "Dining";
                    }
                }

                tablesList.Add(tableDto);
            }

            ViewBag.ActiveOrdersCount = activeOrders.Count;
            ViewBag.StaffCallsCount = activeOrders.Count(o => o.CallStaffRequest);

            return View(tablesList);
        }

        // Bill Preview Side panel details: /Cashier/Detail/{tableNumber}
        public async Task<IActionResult> Detail(string tableNumber)
        {
            var activeOrder = await _orderService.GetActiveOrderByTableAsync(tableNumber);
            if (activeOrder == null)
            {
                TempData["ErrorMessage"] = $"Table {tableNumber} has no active bill.";
                return RedirectToAction(nameof(Index));
            }

            return View(activeOrder);
        }

        // Clear Caller Notification: /Cashier/ClearCallStaff?tableNumber=05 (POST/AJAX)
        [HttpPost]
        public async Task<IActionResult> ClearCallStaff(string tableNumber)
        {
            await _orderService.ResetCallStaffAsync(tableNumber);
            return Json(new { success = true, message = "Call staff status reset." });
        }

        // Settle Payment and Archive Order: /Cashier/SettlePayment (POST)
        [HttpPost]
        public async Task<IActionResult> SettlePayment(string tableNumber, string paymentMethod)
        {
            var activeOrder = await _orderService.GetActiveOrderByTableAsync(tableNumber);
            if (activeOrder == null)
            {
                return Json(new { success = false, message = "No active order found for this table." });
            }

            // Begin Atomic SQL Server Transaction
            using (var transaction = await _sqlContext.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Create Historical Order row
                    var order = new Order
                    {
                        TableNumber = activeOrder.TableNumber,
                        OrderDate = DateTime.UtcNow,
                        SubTotal = (decimal)activeOrder.SubTotal,
                        DiscountAmount = (decimal)activeOrder.DiscountAmount,
                        TotalAmount = (decimal)activeOrder.TotalAmount,
                        Status = "Completed",
                        Note = activeOrder.VoucherCode != null ? $"Voucher: {activeOrder.VoucherCode}" : ""
                    };
                    await _sqlContext.Orders.AddAsync(order);
                    await _sqlContext.SaveChangesAsync();

                    // 2. Create OrderDetails rows
                    foreach (var item in activeOrder.Items)
                    {
                        var detail = new OrderDetail
                        {
                            OrderId = order.Id,
                            DishId = item.DishId,
                            DishName = item.DishName,
                            Quantity = item.Quantity,
                            BasePrice = (decimal)item.BasePrice,
                            Discount = 0,
                            SelectedOptions = $"Size: {item.SelectedSize}",
                            SelectedToppings = item.SelectedToppings != null ? string.Join(", ", item.SelectedToppings) : "",
                            CustomerNote = item.CustomerNote ?? string.Empty
                        };
                        await _sqlContext.OrderDetails.AddAsync(detail);
                    }
                    await _sqlContext.SaveChangesAsync();

                    // 3. Create Invoice row
                    var invoice = new Invoice
                    {
                        OrderId = order.Id,
                        PaymentDate = DateTime.UtcNow,
                        Total = (decimal)activeOrder.SubTotal,
                        Discount = (decimal)activeOrder.DiscountAmount,
                        FinalAmount = (decimal)activeOrder.TotalAmount,
                        CashierUsername = User.Identity?.Name ?? "cashier",
                        PaymentMethod = paymentMethod ?? "Cash"
                    };
                    await _sqlContext.Invoices.AddAsync(invoice);
                    await _sqlContext.SaveChangesAsync();

                    // Commit SQL Server
                    await transaction.CommitAsync();

                    // 4. Delete active order from MongoDB
                    await _mongoDb.ActiveOrders.DeleteOneAsync(o => o.Id == activeOrder.Id);

                    return Json(new { success = true, orderId = order.Id, message = "Payment settled and table cleared!" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = $"Failed to settle transaction: {ex.Message}" });
                }
            }
        }

        // Print Simulated Receipt layout: /Cashier/PrintReceipt/{orderId}
        public async Task<IActionResult> PrintReceipt(int id)
        {
            var order = await _sqlContext.Orders
                .Include(o => o.OrderDetails)
                .Include(o => o.Invoice)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound("Receipt not found in SQL records");
            }

            return View(order);
        }
    }

    // Helper Data Transfer Object for table list mapping
    public class TableStatusDto
    {
        public string TableNumber { get; set; } = string.Empty;
        public string Status { get; set; } = "Available"; // Available, Dining, Billing, CallStaff
        public string? ActiveOrderId { get; set; }
        public double TotalAmount { get; set; }
    }
}
