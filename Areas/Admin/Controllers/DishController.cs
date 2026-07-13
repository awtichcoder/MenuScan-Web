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

        // Tiêm (Inject) thêm Cloudinary vào Constructor
        public DishController(IMongoDatabase mongoDatabase, Cloudinary cloudinary)
        {
            _dishCollection = mongoDatabase.GetCollection<Dish>("Dishes");
            _categoryCollection = mongoDatabase.GetCollection<Category>("Categories");
            _cloudinary = cloudinary;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var categories = await _categoryCollection.Find(_ => true).ToListAsync();
            ViewBag.Categories = categories;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateApi([FromBody] Dish newDish)
        {
            try
            {
                // Nếu người dùng không up ảnh, gắn ảnh mặc định
                if(string.IsNullOrEmpty(newDish.ImageUrl)) 
                    newDish.ImageUrl = "https://via.placeholder.com/400x300?text=No+Image";

                await _dishCollection.InsertOneAsync(newDish);
                return Ok(new { success = true, id = newDish.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // API MỚI: Xử lý Upload ảnh lên Cloudinary
        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Không tìm thấy file!" });

            try
            {
                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "MenuQr_Dishes" // Tạo thư mục riêng trên Cloudinary cho gọn
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.Error != null)
                    return StatusCode(500, new { success = false, message = uploadResult.Error.Message });

                // Trả về link URL thật của ảnh trên Cloudinary
                return Ok(new { success = true, url = uploadResult.SecureUrl.ToString() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}