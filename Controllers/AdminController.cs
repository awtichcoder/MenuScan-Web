using MenuQr.Data;
using MenuQr.Models.Mongo;
using MenuQr.Models.Sql;
using MenuQr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MenuQr.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly MongoDbService _mongoDb;
        private readonly MenuDbContext _sqlContext;
        private readonly IWebHostEnvironment _env;

        public AdminController(MongoDbService mongoDb, MenuDbContext sqlContext, IWebHostEnvironment env)
        {
            _mongoDb = mongoDb;
            _sqlContext = sqlContext;
            _env = env;
        }

        // 1. Dashboard: /Admin
        public async Task<IActionResult> Index()
        {
            // Fetch SQL sales analytics
            var invoices = await _sqlContext.Invoices.ToListAsync();
            var totalRevenue = invoices.Sum(i => i.FinalAmount);
            var completedOrdersCount = invoices.Count;

            // Fetch MongoDB counts
            var dishesCount = await _mongoDb.Dishes.CountDocumentsAsync(_ => true);
            var categoriesCount = await _mongoDb.Categories.CountDocumentsAsync(_ => true);

            // Fetch recent 5 invoices
            var recentInvoices = await _sqlContext.Invoices
                .Include(i => i.Order)
                .OrderByDescending(i => i.PaymentDate)
                .Take(5)
                .ToListAsync();

            // Prepare chart data: aggregate sales by date (last 7 days)
            var last7Days = Enumerable.Range(0, 7)
                .Select(i => DateTime.Today.AddDays(-i))
                .OrderBy(d => d)
                .ToList();

            var chartLabels = last7Days.Select(d => d.ToString("dd/MM")).ToList();
            var chartData = last7Days.Select(d => 
                invoices.Where(i => i.PaymentDate.Date == d.Date).Sum(i => (double)i.FinalAmount)
            ).ToList();

            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.CompletedOrdersCount = completedOrdersCount;
            ViewBag.DishesCount = dishesCount;
            ViewBag.CategoriesCount = categoriesCount;
            ViewBag.RecentInvoices = recentInvoices;
            ViewBag.ChartLabels = chartLabels;
            ViewBag.ChartData = chartData;

            return View();
        }

        // 2. Dishes List: /Admin/Dishes
        public async Task<IActionResult> Dishes(string? categoryId, string? search)
        {
            var filterBuilder = Builders<Dish>.Filter;
            var filter = filterBuilder.Empty;

            if (!string.IsNullOrEmpty(categoryId))
            {
                filter &= filterBuilder.Eq(d => d.CategoryId, categoryId);
            }

            if (!string.IsNullOrEmpty(search))
            {
                filter &= filterBuilder.Regex(d => d.Name, new MongoDB.Bson.BsonRegularExpression(search, "i"));
            }

            var dishes = await _mongoDb.Dishes.Find(filter).ToListAsync();
            var categories = await _mongoDb.Categories.Find(_ => true).ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.SearchQuery = search;

            return View(dishes);
        }

        // Create Dish (GET)
        [HttpGet]
        public async Task<IActionResult> CreateDish()
        {
            var categories = await _mongoDb.Categories.Find(_ => true).ToListAsync();
            ViewBag.Categories = categories;
            return View();
        }

        // Create Dish (POST)
        [HttpPost]
        public async Task<IActionResult> CreateDish(Dish dish, IFormFile? imageFile, string? sizesJson, string? toppingsJson)
        {
            try
            {
                // Parse Options and Toppings JSON from dynamic layout elements
                if (!string.IsNullOrEmpty(sizesJson))
                {
                    dish.Sizes = System.Text.Json.JsonSerializer.Deserialize<List<DishSize>>(sizesJson) ?? new List<DishSize>();
                }
                if (!string.IsNullOrEmpty(toppingsJson))
                {
                    dish.Toppings = System.Text.Json.JsonSerializer.Deserialize<List<DishTopping>>(toppingsJson) ?? new List<DishTopping>();
                }

                // Handle Image Upload
                if (imageFile != null && imageFile.Length > 0)
                {
                    dish.Image = await SaveUploadedImageAsync(imageFile);
                }
                else
                {
                    dish.Image = "/images/menu/default.webp";
                }

                dish.Available = true;
                await _mongoDb.Dishes.InsertOneAsync(dish);

                TempData["SuccessMessage"] = $"Đã thêm món ăn '{dish.Name}' thành công!";
                return RedirectToAction(nameof(Dishes));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Lỗi thêm món: {ex.Message}");
                var categories = await _mongoDb.Categories.Find(_ => true).ToListAsync();
                ViewBag.Categories = categories;
                return View(dish);
            }
        }

        // Edit Dish (GET)
        [HttpGet]
        public async Task<IActionResult> EditDish(string id)
        {
            var dish = await _mongoDb.Dishes.Find(d => d.Id == id).FirstOrDefaultAsync();
            if (dish == null)
            {
                return NotFound("Không tìm thấy món ăn");
            }

            var categories = await _mongoDb.Categories.Find(_ => true).ToListAsync();
            ViewBag.Categories = categories;
            return View(dish);
        }

        // Edit Dish (POST)
        [HttpPost]
        public async Task<IActionResult> EditDish(string id, Dish model, IFormFile? imageFile, string? sizesJson, string? toppingsJson)
        {
            try
            {
                var existingDish = await _mongoDb.Dishes.Find(d => d.Id == id).FirstOrDefaultAsync();
                if (existingDish == null)
                {
                    return NotFound("Không tìm thấy món ăn");
                }

                existingDish.Name = model.Name;
                existingDish.Description = model.Description;
                existingDish.Price = model.Price;
                existingDish.CategoryId = model.CategoryId;
                existingDish.Available = model.Available;

                // Handle sizes & toppings
                if (!string.IsNullOrEmpty(sizesJson))
                {
                    existingDish.Sizes = System.Text.Json.JsonSerializer.Deserialize<List<DishSize>>(sizesJson) ?? new List<DishSize>();
                }
                if (!string.IsNullOrEmpty(toppingsJson))
                {
                    existingDish.Toppings = System.Text.Json.JsonSerializer.Deserialize<List<DishTopping>>(toppingsJson) ?? new List<DishTopping>();
                }

                // Handle new image upload
                if (imageFile != null && imageFile.Length > 0)
                {
                    existingDish.Image = await SaveUploadedImageAsync(imageFile);
                }

                await _mongoDb.Dishes.ReplaceOneAsync(d => d.Id == id, existingDish);

                TempData["SuccessMessage"] = $"Đã cập nhật món '{existingDish.Name}' thành công!";
                return RedirectToAction(nameof(Dishes));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Lỗi cập nhật: {ex.Message}");
                var categories = await _mongoDb.Categories.Find(_ => true).ToListAsync();
                ViewBag.Categories = categories;
                return View(model);
            }
        }

        // Delete Dish (POST)
        [HttpPost]
        public async Task<IActionResult> DeleteDish(string id)
        {
            var result = await _mongoDb.Dishes.DeleteOneAsync(d => d.Id == id);
            if (result.DeletedCount > 0)
            {
                TempData["SuccessMessage"] = "Đã xóa món ăn thành công.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không thể xóa món ăn.";
            }
            return RedirectToAction(nameof(Dishes));
        }

        // 3. Helper to save image file
        private async Task<string> SaveUploadedImageAsync(IFormFile imageFile)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "menu");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            return $"/images/menu/{uniqueFileName}";
        }
    }
}
