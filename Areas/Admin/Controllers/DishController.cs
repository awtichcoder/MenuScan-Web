using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using MenuQr.Areas.Admin.Models;
using MenuQr.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MenuQr.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class DishController : Controller
    {
        private readonly IMongoCollection<Dish> _dishCollection;
        private readonly IMongoCollection<Category> _categoryCollection;
        private readonly IMongoCollection<ActiveOrder> _activeOrderCollection;
        private readonly Cloudinary _cloudinary;

        public DishController(IMongoDatabase mongoDatabase, Cloudinary cloudinary)
        {
            _dishCollection = mongoDatabase.GetCollection<Dish>("Dishes");
            _categoryCollection = mongoDatabase.GetCollection<Category>("Categories");
            _activeOrderCollection = mongoDatabase.GetCollection<ActiveOrder>("ActiveOrders");
            _cloudinary = cloudinary;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? keyword, string? categoryId, int page = 1, int limit = 20)
        {
            var data = await QueryDishes(keyword, categoryId, page, limit);
            ViewBag.Categories = await _categoryCollection.Find(c => c.IsActive).SortBy(c => c.Name).ToListAsync();
            ViewBag.Keyword = keyword ?? "";
            ViewBag.CategoryId = categoryId ?? "";
            ViewBag.Page = page;
            ViewBag.Limit = limit;
            ViewBag.Total = data.Total;
            ViewBag.TotalPages = data.TotalPages;
            return View(data.Items);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var categories = await _categoryCollection.Find(c => c.IsActive).SortBy(c => c.Name).ToListAsync();
            ViewBag.Categories = categories;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var dish = await _dishCollection.Find(d => d.Id == id && !d.IsDeleted).FirstOrDefaultAsync();
            if (dish == null) return NotFound("Khong tim thay mon an.");

            ViewBag.Categories = await _categoryCollection.Find(c => c.IsActive).SortBy(c => c.Name).ToListAsync();
            return View(dish);
        }

        [HttpGet("/api/admin/dishes")]
        public async Task<IActionResult> GetDishes(string? keyword, string? category_id, int page = 1, int limit = 20)
        {
            var result = await QueryDishes(keyword, category_id, page, limit);
            var code = result.Total == 0
                ? (string.IsNullOrWhiteSpace(keyword) && string.IsNullOrWhiteSpace(category_id) ? "MS01" : "MS02")
                : null;

            return Ok(new
            {
                success = true,
                code,
                data = result.Items,
                total = result.Total,
                totalPages = result.TotalPages,
                permissions = new { canCreate = true, canEdit = true, canDelete = true }
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateApi([FromBody] Dish dish)
        {
            var validation = await ValidateDish(dish);
            if (validation != null) return validation;

            if (string.IsNullOrWhiteSpace(dish.ImageUrl))
                dish.ImageUrl = "https://via.placeholder.com/400x300?text=No+Image";

            dish.Name = dish.Name.Trim();
            dish.CategoryName = dish.CategoryName.Trim();
            dish.IsAvailable = true;
            dish.IsDeleted = false;
            dish.CreatedAt = DateTime.Now;
            dish.UpdatedAt = DateTime.Now;

            await _dishCollection.InsertOneAsync(dish);
            return Ok(new { success = true, code = "MS06", message = "Them mon an thanh cong.", id = dish.Id });
        }

        [HttpPost("/api/admin/dishes")]
        public Task<IActionResult> CreateDish([FromBody] Dish dish)
        {
            return CreateApi(dish);
        }

        [HttpPut("/api/admin/dishes/{id}")]
        public async Task<IActionResult> UpdateDish(string id, [FromBody] Dish model)
        {
            var existing = await _dishCollection.Find(d => d.Id == id && !d.IsDeleted).FirstOrDefaultAsync();
            if (existing == null)
                return NotFound(new { success = false, code = "MS01", message = "Mon an khong ton tai." });

            var validation = await ValidateDish(model, id);
            if (validation != null) return validation;

            var imageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? existing.ImageUrl : model.ImageUrl;
            var update = Builders<Dish>.Update
                .Set(d => d.CategoryId, model.CategoryId)
                .Set(d => d.CategoryName, model.CategoryName.Trim())
                .Set(d => d.Name, model.Name.Trim())
                .Set(d => d.BasePrice, model.BasePrice)
                .Set(d => d.DiscountPercent, model.DiscountPercent)
                .Set(d => d.IsAvailable, model.IsAvailable)
                .Set(d => d.ImageUrl, imageUrl)
                .Set(d => d.Specifications, model.Specifications ?? new List<Specification>())
                .Set(d => d.UpdatedAt, DateTime.Now);

            await _dishCollection.UpdateOneAsync(d => d.Id == id, update);
            return Ok(new { success = true, code = "MS06", message = "Cap nhat mon an thanh cong." });
        }

        [HttpPost]
        public Task<IActionResult> UpdateApi(string id, [FromBody] Dish model)
        {
            return UpdateDish(id, model);
        }

        [HttpDelete("/api/admin/dishes/{id}")]
        public async Task<IActionResult> DeleteDish(string id)
        {
            var dish = await _dishCollection.Find(d => d.Id == id && !d.IsDeleted).FirstOrDefaultAsync();
            if (dish == null)
                return NotFound(new { success = false, code = "MS01", message = "Mon an khong ton tai." });

            var inActiveOrder = await _activeOrderCollection
                .Find(o => o.Status == "Serving" && o.Items.Any(i => i.DishId == id && i.ItemStatus != "Cancelled"))
                .AnyAsync();

            if (inActiveOrder)
                return BadRequest(new { success = false, code = "MS02", message = "Khong the xoa mon dang nam trong don chua hoan tat." });

            var update = Builders<Dish>.Update
                .Set(d => d.IsDeleted, true)
                .Set(d => d.IsAvailable, false)
                .Set(d => d.UpdatedAt, DateTime.Now);

            await _dishCollection.UpdateOneAsync(d => d.Id == id, update);
            return Ok(new { success = true, code = "MS05", message = "Xoa mon an thanh cong." });
        }

        [HttpPost]
        public Task<IActionResult> DeleteApi(string id)
        {
            return DeleteDish(id);
        }

        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, code = "MS04", message = "Khong tim thay file." });

            if (!IsAllowedImage(file))
                return BadRequest(new { success = false, code = "MS04", message = "Anh chi nhan jpg, png, webp va toi da 10MB." });

            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "MenuQr_Dishes"
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            if (uploadResult.Error != null)
                return StatusCode(500, new { success = false, message = uploadResult.Error.Message });

            return Ok(new { success = true, url = uploadResult.SecureUrl.ToString() });
        }

        private async Task<(List<Dish> Items, long Total, int TotalPages)> QueryDishes(string? keyword, string? categoryId, int page, int limit)
        {
            page = Math.Max(1, page);
            limit = Math.Clamp(limit, 1, 100);

            var filter = Builders<Dish>.Filter.Eq(d => d.IsDeleted, false);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var escaped = System.Text.RegularExpressions.Regex.Escape(keyword.Trim());
                filter &= Builders<Dish>.Filter.Regex(d => d.Name, new BsonRegularExpression(escaped, "i"));
            }

            if (!string.IsNullOrWhiteSpace(categoryId))
                filter &= Builders<Dish>.Filter.Eq(d => d.CategoryId, categoryId);

            var total = await _dishCollection.CountDocumentsAsync(filter);
            var items = await _dishCollection
                .Find(filter)
                .SortBy(d => d.Name)
                .Skip((page - 1) * limit)
                .Limit(limit)
                .ToListAsync();

            return (items, total, (int)Math.Ceiling(total / (double)limit));
        }

        private async Task<IActionResult?> ValidateDish(Dish dish, string? currentId = null)
        {
            if (dish == null || string.IsNullOrWhiteSpace(dish.Name) || string.IsNullOrWhiteSpace(dish.CategoryId))
                return BadRequest(new { success = false, code = "MS01", message = "Vui long nhap day du ten mon va danh muc." });

            if (dish.BasePrice < 0)
                return BadRequest(new { success = false, code = "MS02", message = "Gia mon an khong hop le." });

            if (dish.DiscountPercent < 0 || dish.DiscountPercent > 100)
                return BadRequest(new { success = false, code = "MS02", message = "Phan tram giam gia khong hop le." });

            var category = await _categoryCollection.Find(c => c.Id == dish.CategoryId && c.IsActive).FirstOrDefaultAsync();
            if (category == null)
                return BadRequest(new { success = false, code = "MS05", message = "Danh muc khong ton tai." });

            dish.CategoryName = category.Name;

            var escaped = System.Text.RegularExpressions.Regex.Escape(dish.Name.Trim());
            var filter = Builders<Dish>.Filter.Eq(d => d.CategoryId, dish.CategoryId);
            filter &= Builders<Dish>.Filter.Eq(d => d.IsDeleted, false);
            filter &= Builders<Dish>.Filter.Regex(d => d.Name, new BsonRegularExpression($"^{escaped}$", "i"));

            if (!string.IsNullOrEmpty(currentId))
                filter &= Builders<Dish>.Filter.Ne(d => d.Id, currentId);

            if (await _dishCollection.Find(filter).AnyAsync())
                return BadRequest(new { success = false, code = "MS03", message = "Ten mon da ton tai trong danh muc." });

            return null;
        }

        private static bool IsAllowedImage(IFormFile file)
        {
            if (file.Length > 10 * 1024 * 1024) return false;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return extension is ".jpg" or ".jpeg" or ".png" or ".webp";
        }
    }
}
