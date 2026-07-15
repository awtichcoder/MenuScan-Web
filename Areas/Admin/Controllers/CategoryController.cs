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
        private readonly IMongoCollection<Dish> _dishCollection;

        public CategoryController(IMongoDatabase mongoDatabase)
        {
            _categoryCollection = mongoDatabase.GetCollection<Category>("Categories");
            _dishCollection = mongoDatabase.GetCollection<Dish>("Dishes");
        }

        // Render giao diện quản lý danh mục
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // Giữ lại route cũ /Admin/Category/Create trỏ về trang quản lý
        [HttpGet]
        public IActionResult Create()
        {
            return RedirectToAction(nameof(Index));
        }

        // UC16 - Xem danh sách danh mục
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var categories = await _categoryCollection
                .Find(_ => true)
                .SortBy(c => c.DisplayOrder)
                .ToListAsync();
            return Ok(new { success = true, data = categories });
        }

        // Helper: kiểm tra trùng tên (không phân biệt hoa/thường), loại trừ id đang sửa
        private async Task<bool> IsNameDuplicatedAsync(string name, string? excludeId = null)
        {
            var normalized = name.Trim();
            var filter = Builders<Category>.Filter.Regex(
                c => c.Name,
                new MongoDB.Bson.BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalized)}$", "i"));

            if (!string.IsNullOrEmpty(excludeId))
                filter &= Builders<Category>.Filter.Ne(c => c.Id, excludeId);

            return await _categoryCollection.Find(filter).AnyAsync();
        }

        // UC16 - Thêm danh mục (BR01.1 trống, BR01.2 trùng, BR01.4 max 100)
        [HttpPost]
        public async Task<IActionResult> CreateApi([FromBody] Category newCategory)
        {
            if (newCategory == null)
                return BadRequest(new { success = false, code = CategoryMessages.EmptyName, message = CategoryMessages.Text(CategoryMessages.EmptyName) });

            var name = newCategory.Name?.Trim() ?? "";

            if (string.IsNullOrEmpty(name))
                return BadRequest(new { success = false, code = CategoryMessages.EmptyName, message = CategoryMessages.Text(CategoryMessages.EmptyName) });

            if (name.Length > 100)
                return BadRequest(new { success = false, code = CategoryMessages.EmptyName, message = "Tên danh mục tối đa 100 ký tự." });

            if (await IsNameDuplicatedAsync(name))
                return Conflict(new { success = false, code = CategoryMessages.DuplicateName, message = CategoryMessages.Text(CategoryMessages.DuplicateName) });

            try
            {
                newCategory.Name = name;
                newCategory.CreatedAt = DateTime.UtcNow;
                newCategory.UpdatedAt = DateTime.UtcNow;
                await _categoryCollection.InsertOneAsync(newCategory);
                return Ok(new { success = true, code = CategoryMessages.CreateOk, message = CategoryMessages.Text(CategoryMessages.CreateOk), id = newCategory.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // UC16 - Sửa danh mục (validate như thêm, loại trừ chính bản ghi)
        [HttpPut]
        public async Task<IActionResult> UpdateApi(string id, [FromBody] Category input)
        {
            if (input == null)
                return BadRequest(new { success = false, code = CategoryMessages.EmptyName, message = CategoryMessages.Text(CategoryMessages.EmptyName) });

            var name = input.Name?.Trim() ?? "";

            if (string.IsNullOrEmpty(name))
                return BadRequest(new { success = false, code = CategoryMessages.EmptyName, message = CategoryMessages.Text(CategoryMessages.EmptyName) });

            if (name.Length > 100)
                return BadRequest(new { success = false, code = CategoryMessages.EmptyName, message = "Tên danh mục tối đa 100 ký tự." });

            if (await IsNameDuplicatedAsync(name, id))
                return Conflict(new { success = false, code = CategoryMessages.DuplicateName, message = CategoryMessages.Text(CategoryMessages.DuplicateName) });

            var update = Builders<Category>.Update
                .Set(c => c.Name, name)
                .Set(c => c.Description, input.Description ?? "")
                .Set(c => c.DisplayOrder, input.DisplayOrder)
                .Set(c => c.IsActive, input.IsActive)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);

            var result = await _categoryCollection.UpdateOneAsync(c => c.Id == id, update);
            if (result.MatchedCount == 0)
                return NotFound(new { success = false, message = "Không tìm thấy danh mục." });

            return Ok(new { success = true, message = "Cập nhật danh mục thành công." });
        }

        // UC16 - Xóa danh mục (BR01.3: chặn nếu danh mục còn chứa món ăn)
        [HttpDelete]
        public async Task<IActionResult> DeleteApi(string id)
        {
            var dishCount = await _dishCollection
                .CountDocumentsAsync(d => d.CategoryId == id);

            if (dishCount > 0)
                return Conflict(new { success = false, code = CategoryMessages.HasDishes, message = CategoryMessages.Text(CategoryMessages.HasDishes) });

            var result = await _categoryCollection.DeleteOneAsync(c => c.Id == id);
            if (result.DeletedCount == 0)
                return NotFound(new { success = false, message = "Không tìm thấy danh mục." });

            return Ok(new { success = true, message = "Xóa danh mục thành công." });
        }
    }
}
