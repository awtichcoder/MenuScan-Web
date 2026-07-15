// Areas/Admin/Controllers/CategoryController.cs
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MenuQr.Areas.Admin.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace MenuQr.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CategoryController : Controller
    {
        private readonly IMongoCollection<Category> _categoryCollection;
        private readonly Cloudinary _cloudinary;

        // 1. Cấu hình MongoDB và Cloudinary
        public CategoryController(IMongoDatabase mongoDatabase, IConfiguration configuration)
        {
            _categoryCollection = mongoDatabase.GetCollection<Category>("Categories");

            // Khởi tạo Cloudinary từ appsettings.json
            var account = new Account(
                configuration["CloudinarySettings:CloudName"],
                configuration["CloudinarySettings:ApiKey"],
                configuration["CloudinarySettings:ApiSecret"]
            );
            _cloudinary = new Cloudinary(account);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // 2. Nhận FormData (bao gồm File ảnh) từ giao diện
        [HttpPost]
        public async Task<IActionResult> CreateApi([FromForm] CategoryUploadModel model)
        {
            try
            {
                string imageUrl = ""; // Mặc định ảnh rỗng

                // 3. Nếu người dùng có chọn ảnh -> Upload lên Cloudinary
                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    using var stream = model.ImageFile.OpenReadStream();
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(model.ImageFile.FileName, stream),
                        Folder = "categories", // Tạo thư mục "categories" trên Cloudinary cho gọn
                        Overwrite = true
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    imageUrl = uploadResult.SecureUrl.ToString(); // Lấy link HTTPS từ Cloudinary
                }

                // 4. Lưu thông tin vào MongoDB
                var newCategory = new Category
                {
                    Name = model.Name,
                    Description = model.Description ?? "",
                    DisplayOrder = model.DisplayOrder,
                    IsActive = model.IsActive,
                    ImageUrl = imageUrl,
                    UpdatedAt = DateTime.Now
                };

                await _categoryCollection.InsertOneAsync(newCategory);
                
                return Ok(new { success = true, id = newCategory.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    // Class phụ để hứng dữ liệu + File từ HTML Form

}