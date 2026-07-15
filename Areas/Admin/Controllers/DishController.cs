// Areas/Admin/Controllers/DishController.cs
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MenuQr.Areas.Admin.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace MenuQr.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class DishController : Controller
    {
        private readonly IMongoCollection<Dish> _dishCollection;
        private readonly IMongoCollection<Category> _categoryCollection;
        private readonly Cloudinary _cloudinary;

        public DishController(IMongoDatabase mongoDatabase, Cloudinary cloudinary)
        {
            _dishCollection = mongoDatabase.GetCollection<Dish>("Dishes");
            _categoryCollection = mongoDatabase.GetCollection<Category>("Categories");
            _cloudinary = cloudinary;
        }

        // Trang quản lý món
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var categories = await _categoryCollection.Find(_ => true).ToListAsync();
            ViewBag.Categories = categories;
            return View();
        }

        // Giữ route cũ /Admin/Dish/Create -> chuyển về trang quản lý
        [HttpGet]
        public IActionResult Create() => RedirectToAction(nameof(Index));

        // UC20 - Danh sách món: keyword (không phân biệt hoa/thường), lọc category, phân trang, sắp xếp
        [HttpGet]
        public async Task<IActionResult> List(string? keyword = null, string? categoryId = null,
            int page = 1, int limit = 10, string sort = "name")
        {
            var builder = Builders<Dish>.Filter;
            var filter = builder.Empty;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = System.Text.RegularExpressions.Regex.Escape(keyword.Trim());
                filter &= builder.Regex(d => d.Name, new MongoDB.Bson.BsonRegularExpression(kw, "i"));
            }
            if (!string.IsNullOrWhiteSpace(categoryId))
                filter &= builder.Eq(d => d.CategoryId, categoryId);

            if (page < 1) page = 1;
            if (limit < 1) limit = 10;

            var total = await _dishCollection.CountDocumentsAsync(filter);
            var query = _dishCollection.Find(filter);
            query = sort == "price"
                ? query.SortBy(d => d.BasePrice)
                : query.SortBy(d => d.Name);

            var items = await query.Skip((page - 1) * limit).Limit(limit).ToListAsync();
            var totalPages = (int)Math.Ceiling(total / (double)limit);

            return Ok(new { success = true, data = items, total, page, totalPages });
        }

        private static readonly string[] AllowedImageExt = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxImageBytes = 10 * 1024 * 1024; // 10MB

        // Upload ảnh lên Cloudinary (BR02.4 định dạng, BR02.5 dung lượng)
        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, code = DishMessages.InvalidImage, message = "Chưa chọn file ảnh." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExt.Contains(ext))
                return BadRequest(new { success = false, code = DishMessages.InvalidImage, message = DishMessages.Text(DishMessages.InvalidImage) });

            if (file.Length > MaxImageBytes)
                return BadRequest(new { success = false, code = DishMessages.InvalidImage, message = "Ảnh vượt quá 10MB." });

            try
            {
                await using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "MenuQr_Dishes"
                };
                var result = await _cloudinary.UploadAsync(uploadParams);
                if (result.StatusCode == System.Net.HttpStatusCode.OK)
                    return Ok(new { success = true, url = result.SecureUrl.ToString() });

                return StatusCode(500, new { success = false, message = "Cloudinary từ chối ảnh." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // BR02.6/BR03.4 - trùng tên trong CÙNG danh mục (không phân biệt hoa/thường), loại trừ id đang sửa
        private async Task<bool> IsDishNameDuplicatedAsync(string name, string categoryId, string? excludeId = null)
        {
            var b = Builders<Dish>.Filter;
            var filter = b.Eq(d => d.CategoryId, categoryId) &
                b.Regex(d => d.Name, new MongoDB.Bson.BsonRegularExpression(
                    $"^{System.Text.RegularExpressions.Regex.Escape(name.Trim())}$", "i"));
            if (!string.IsNullOrEmpty(excludeId))
                filter &= b.Ne(d => d.Id, excludeId);
            return await _dishCollection.Find(filter).AnyAsync();
        }

        // UC17 - Thêm món (BR02.1 tên, BR02.2 giá>=0, BR02.3 category tồn tại, BR02.6 trùng tên)
        [HttpPost]
        public async Task<IActionResult> CreateApi([FromBody] Dish dish)
        {
            if (dish == null || string.IsNullOrWhiteSpace(dish.Name) || string.IsNullOrWhiteSpace(dish.CategoryId))
                return BadRequest(new { success = false, code = DishMessages.MissingField, message = DishMessages.Text(DishMessages.MissingField) });

            if (dish.BasePrice < 0)
                return BadRequest(new { success = false, code = DishMessages.InvalidPrice, message = DishMessages.Text(DishMessages.InvalidPrice) });

            var category = await _categoryCollection.Find(c => c.Id == dish.CategoryId).FirstOrDefaultAsync();
            if (category == null)
                return BadRequest(new { success = false, code = DishMessages.MissingField, message = "Danh mục không tồn tại." });

            if (await IsDishNameDuplicatedAsync(dish.Name, dish.CategoryId))
                return Conflict(new { success = false, code = DishMessages.DuplicateName, message = DishMessages.Text(DishMessages.DuplicateName) });

            try
            {
                dish.Name = dish.Name.Trim();
                dish.CategoryName = category.Name; // đồng bộ tên danh mục (denormalized)
                await _dishCollection.InsertOneAsync(dish);
                return Ok(new { success = true, code = DishMessages.CreateOk, message = DishMessages.Text(DishMessages.CreateOk), id = dish.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // UC18 - Sửa món
        [HttpPut]
        public async Task<IActionResult> UpdateApi(string id, [FromBody] Dish dish)
        {
            if (dish == null || string.IsNullOrWhiteSpace(dish.Name) || string.IsNullOrWhiteSpace(dish.CategoryId))
                return BadRequest(new { success = false, code = DishMessages.MissingField, message = DishMessages.Text(DishMessages.MissingField) });

            if (dish.BasePrice < 0)
                return BadRequest(new { success = false, code = DishMessages.InvalidPrice, message = DishMessages.Text(DishMessages.InvalidPrice) });

            var existing = await _dishCollection.Find(d => d.Id == id).FirstOrDefaultAsync();
            if (existing == null)
                return NotFound(new { success = false, code = DishMessages.NotFound, message = DishMessages.Text(DishMessages.NotFound) });

            var category = await _categoryCollection.Find(c => c.Id == dish.CategoryId).FirstOrDefaultAsync();
            if (category == null)
                return BadRequest(new { success = false, code = DishMessages.MissingField, message = "Danh mục không tồn tại." });

            if (await IsDishNameDuplicatedAsync(dish.Name, dish.CategoryId, id))
                return Conflict(new { success = false, code = DishMessages.DuplicateName, message = DishMessages.Text(DishMessages.DuplicateName) });

            // BR03.6: không upload ảnh mới thì giữ nguyên ảnh cũ
            var imageUrl = string.IsNullOrWhiteSpace(dish.ImageUrl) ? existing.ImageUrl : dish.ImageUrl;
            // Giữ nguyên Size/Topping cũ nếu form không gửi (tránh xóa nhầm cấu hình có sẵn)
            var specs = (dish.Specifications != null && dish.Specifications.Count > 0)
                ? dish.Specifications : existing.Specifications;

            var update = Builders<Dish>.Update
                .Set(d => d.Name, dish.Name.Trim())
                .Set(d => d.CategoryId, dish.CategoryId)
                .Set(d => d.CategoryName, category.Name)
                .Set(d => d.BasePrice, dish.BasePrice)
                .Set(d => d.DiscountPercent, dish.DiscountPercent)
                .Set(d => d.IsAvailable, dish.IsAvailable)
                .Set(d => d.ImageUrl, imageUrl)
                .Set(d => d.Specifications, specs);

            await _dishCollection.UpdateOneAsync(d => d.Id == id, update);
            return Ok(new { success = true, code = DishMessages.CreateOk, message = "Cập nhật món thành công." });
        }

        // UC19 - Xóa món
        [HttpDelete]
        public async Task<IActionResult> DeleteApi(string id)
        {
            var result = await _dishCollection.DeleteOneAsync(d => d.Id == id);
            if (result.DeletedCount == 0)
                return NotFound(new { success = false, code = DishMessages.NotFound, message = DishMessages.Text(DishMessages.NotFound) });

            return Ok(new { success = true, message = "Xóa món thành công." });
        }
    }
}
