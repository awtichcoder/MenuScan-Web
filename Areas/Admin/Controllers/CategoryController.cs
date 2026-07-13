// Areas/Admin/Controllers/CategoryController.cs
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MenuQr.Areas.Admin.Models;

namespace MenuQr.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CategoryController : Controller
    {
        private readonly IMongoCollection<Category> _categoryCollection;

        public CategoryController(IMongoDatabase mongoDatabase)
        {
            // Trỏ đúng vào collection Categories
            _categoryCollection = mongoDatabase.GetCollection<Category>("Categories");
        }

        // Render giao diện
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // Nhận data lưu vào DB
        [HttpPost]
        public async Task<IActionResult> CreateApi([FromBody] Category newCategory)
        {
            try
            {
                await _categoryCollection.InsertOneAsync(newCategory);
                return Ok(new { success = true, id = newCategory.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}