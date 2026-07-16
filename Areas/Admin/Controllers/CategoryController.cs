using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using MenuQr.Areas.Admin.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MenuQr.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CategoryController : Controller
    {
        private readonly IMongoCollection<Category> _categoryCollection;
        private readonly IMongoCollection<Dish> _dishCollection;
        private readonly Cloudinary _cloudinary;

        public CategoryController(IMongoDatabase mongoDatabase, IConfiguration configuration)
        {
            _categoryCollection = mongoDatabase.GetCollection<Category>("Categories");
            _dishCollection = mongoDatabase.GetCollection<Dish>("Dishes");

            var account = new Account(
                configuration["CloudinarySettings:CloudName"],
                configuration["CloudinarySettings:ApiKey"],
                configuration["CloudinarySettings:ApiSecret"]
            );
            _cloudinary = new Cloudinary(account);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var categories = await _categoryCollection
                .Find(c => c.IsActive)
                .SortBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return View(categories);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpGet("/api/admin/categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _categoryCollection
                .Find(c => c.IsActive)
                .SortBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return Ok(new { success = true, data = categories });
        }

        [HttpPost]
        public async Task<IActionResult> CreateApi([FromForm] CategoryUploadModel model)
        {
            var validation = await ValidateCategory(model.Name);
            if (validation != null) return validation;

            try
            {
                var imageUrl = "";
                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    using var stream = model.ImageFile.OpenReadStream();
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(model.ImageFile.FileName, stream),
                        Folder = "categories",
                        Overwrite = true
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    if (uploadResult.Error != null)
                        return StatusCode(500, new { success = false, code = "MS09", message = uploadResult.Error.Message });

                    imageUrl = uploadResult.SecureUrl.ToString();
                }

                var category = new Category
                {
                    Name = model.Name.Trim(),
                    Description = model.Description ?? "",
                    DisplayOrder = model.DisplayOrder <= 0 ? 1 : model.DisplayOrder,
                    IsActive = model.IsActive,
                    ImageUrl = imageUrl,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                await _categoryCollection.InsertOneAsync(category);
                return Ok(new { success = true, code = "MS04", message = "Them danh muc thanh cong.", id = category.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, code = "MS09", message = ex.Message });
            }
        }

        [HttpPost("/api/admin/categories")]
        public Task<IActionResult> CreateCategory([FromForm] CategoryUploadModel model)
        {
            return CreateApi(model);
        }

        [HttpPut("/api/admin/categories/{id}")]
        public async Task<IActionResult> UpdateCategory(string id, [FromBody] Category model)
        {
            var existing = await _categoryCollection.Find(c => c.Id == id && c.IsActive).FirstOrDefaultAsync();
            if (existing == null)
                return NotFound(new { success = false, code = "MS05", message = "Khong tim thay danh muc." });

            var validation = await ValidateCategory(model.Name, id);
            if (validation != null) return validation;

            var update = Builders<Category>.Update
                .Set(c => c.Name, model.Name.Trim())
                .Set(c => c.Description, model.Description ?? "")
                .Set(c => c.DisplayOrder, model.DisplayOrder <= 0 ? 1 : model.DisplayOrder)
                .Set(c => c.IsActive, model.IsActive)
                .Set(c => c.UpdatedAt, DateTime.Now);

            await _categoryCollection.UpdateOneAsync(c => c.Id == id, update);
            return Ok(new { success = true, code = "MS06", message = "Cap nhat danh muc thanh cong." });
        }

        [HttpPost]
        public Task<IActionResult> UpdateApi(string id, [FromBody] Category model)
        {
            return UpdateCategory(id, model);
        }

        [HttpDelete("/api/admin/categories/{id}")]
        public async Task<IActionResult> DeleteCategory(string id)
        {
            var hasDishes = await _dishCollection.Find(d => d.CategoryId == id && !d.IsDeleted).AnyAsync();
            if (hasDishes)
                return BadRequest(new { success = false, code = "MS03", message = "Khong the xoa vi danh muc dang chua mon an." });

            var result = await _categoryCollection.UpdateOneAsync(
                c => c.Id == id && c.IsActive,
                Builders<Category>.Update.Set(c => c.IsActive, false).Set(c => c.UpdatedAt, DateTime.Now));

            if (result.MatchedCount == 0)
                return NotFound(new { success = false, code = "MS05", message = "Khong tim thay danh muc." });

            return Ok(new { success = true, code = "MS07", message = "Xoa danh muc thanh cong." });
        }

        [HttpPost]
        public Task<IActionResult> DeleteApi(string id)
        {
            return DeleteCategory(id);
        }

        private async Task<IActionResult?> ValidateCategory(string? name, string? currentId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, code = "MS02", message = "Vui long nhap ten danh muc." });

            if (name.Trim().Length > 100)
                return BadRequest(new { success = false, code = "MS08", message = "Ten danh muc toi da 100 ky tu." });

            var escaped = System.Text.RegularExpressions.Regex.Escape(name.Trim());
            var filter = Builders<Category>.Filter.Regex(c => c.Name, new BsonRegularExpression($"^{escaped}$", "i"));
            filter &= Builders<Category>.Filter.Eq(c => c.IsActive, true);

            if (!string.IsNullOrEmpty(currentId))
                filter &= Builders<Category>.Filter.Ne(c => c.Id, currentId);

            if (await _categoryCollection.Find(filter).AnyAsync())
                return BadRequest(new { success = false, code = "MS01", message = "Ten danh muc da ton tai." });

            return null;
        }
    }
}
