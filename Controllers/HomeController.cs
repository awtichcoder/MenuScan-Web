using MenuQr.Models.Mongo;
using MenuQr.Services;
using MenuQr.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Linq;
using System.Threading.Tasks;

namespace MenuQr.Controllers
{
    public class HomeController : Controller
    {
        private readonly MongoDbService _mongoDb;
        private readonly OrderService _orderService;

        public HomeController(MongoDbService mongoDb, OrderService orderService)
        {
            _mongoDb = mongoDb;
            _orderService = orderService;
        }

        // QR Entry Point: /Home/Index?tableNumber=05
        public IActionResult Index(string? tableNumber)
        {
            if (!string.IsNullOrEmpty(tableNumber))
            {
                HttpContext.Session.SetString("TableNumber", tableNumber);
                return RedirectToAction(nameof(Menu));
            }

            // Check if table number is already set in the session
            var sessionTable = HttpContext.Session.GetString("TableNumber");
            if (!string.IsNullOrEmpty(sessionTable))
            {
                return RedirectToAction(nameof(Menu));
            }

            // If no table is set, show the Splash Screen where they can also select a mockup table for testing
            return View();
        }

        // Setup test table session
        [HttpPost]
        public IActionResult SetTable(string tableNumber)
        {
            if (!string.IsNullOrEmpty(tableNumber))
            {
                HttpContext.Session.SetString("TableNumber", tableNumber.Trim());
                return RedirectToAction(nameof(Menu));
            }
            return RedirectToAction(nameof(Index));
        }

        // Clear table session
        public IActionResult ClearTable()
        {
            HttpContext.Session.Remove("TableNumber");
            return RedirectToAction(nameof(Index));
        }

        // Customer Menu Browsing: /Home/Menu
        public async Task<IActionResult> Menu(string? categoryId, string? search)
        {
            var tableNumber = HttpContext.Session.GetString("TableNumber");
            if (string.IsNullOrEmpty(tableNumber))
            {
                return RedirectToAction(nameof(Index));
            }

            // Fetch categories
            var categories = await _mongoDb.Categories
                .Find(c => c.Available)
                .ToListAsync();

            // Fetch dishes
            var dishFilter = Builders<Dish>.Filter.Eq(d => d.Available, true);
            
            if (!string.IsNullOrEmpty(categoryId))
            {
                dishFilter &= Builders<Dish>.Filter.Eq(d => d.CategoryId, categoryId);
            }
            
            if (!string.IsNullOrEmpty(search))
            {
                dishFilter &= Builders<Dish>.Filter.Regex(d => d.Name, new MongoDB.Bson.BsonRegularExpression(search, "i"));
            }

            var dishes = await _mongoDb.Dishes.Find(dishFilter).ToListAsync();

            // Fetch current active order to show quick cart count on navigation
            var activeOrder = await _orderService.GetActiveOrderByTableAsync(tableNumber);

            var viewModel = new MenuViewModel
            {
                Categories = categories,
                Dishes = dishes,
                SelectedCategoryId = categoryId ?? string.Empty,
                SearchQuery = search ?? string.Empty,
                TableNumber = tableNumber,
                ActiveOrder = activeOrder
            };

            return View(viewModel);
        }

        // Food Details: /Home/DishDetail/{id}
        public async Task<IActionResult> DishDetail(string id)
        {
            var tableNumber = HttpContext.Session.GetString("TableNumber");
            if (string.IsNullOrEmpty(tableNumber))
            {
                return RedirectToAction(nameof(Index));
            }

            var dish = await _mongoDb.Dishes.Find(d => d.Id == id && d.Available).FirstOrDefaultAsync();
            if (dish == null)
            {
                return NotFound();
            }

            var viewModel = new DishDetailViewModel
            {
                Dish = dish,
                TableNumber = tableNumber
            };

            return View(viewModel);
        }
    }
}
