using MenuQr.Data;
using MenuQr.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace MenuQr.Controllers
{
    public class OrderController : Controller
    {
        private readonly OrderService _orderService;
        private readonly MenuDbContext _sqlContext;

        public OrderController(OrderService orderService, MenuDbContext sqlContext)
        {
            _orderService = orderService;
            _sqlContext = sqlContext;
        }

        // Live Order Tracking: /Order/Tracking
        public async Task<IActionResult> Tracking()
        {
            var tableNumber = HttpContext.Session.GetString("TableNumber");
            if (string.IsNullOrEmpty(tableNumber))
            {
                return RedirectToAction("Index", "Home");
            }

            var activeOrder = await _orderService.GetActiveOrderByTableAsync(tableNumber);
            if (activeOrder == null || activeOrder.Status == "Cart")
            {
                // No active order submitted yet
                return RedirectToAction("Menu", "Home");
            }

            return View(activeOrder);
        }

        // Call Staff Action (POST / AJAX)
        [HttpPost]
        public async Task<IActionResult> CallStaff()
        {
            var tableNumber = HttpContext.Session.GetString("TableNumber");
            if (string.IsNullOrEmpty(tableNumber))
            {
                return Json(new { success = false, message = "Session expired." });
            }

            await _orderService.CallStaffAsync(tableNumber);
            return Json(new { success = true, message = "Staff notified! Someone will be with you shortly." });
        }

        // View Customer Profile / Table Order History
        public async Task<IActionResult> Profile()
        {
            var tableNumber = HttpContext.Session.GetString("TableNumber");
            if (string.IsNullOrEmpty(tableNumber))
            {
                return RedirectToAction("Index", "Home");
            }

            // Fetch completed orders from SQL Server history for this specific table number
            var orderHistory = await _sqlContext.Orders
                .Where(o => o.TableNumber == tableNumber)
                .Include(o => o.OrderDetails)
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();

            ViewBag.TableNumber = tableNumber;
            return View(orderHistory);
        }
    }
}
