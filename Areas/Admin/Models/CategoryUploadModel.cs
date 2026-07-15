
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MenuQr.Areas.Admin.Models
{


     public class CategoryUploadModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public IFormFile? ImageFile { get; set; } // Thuộc tính quan trọng nhất để nhận File
    }
}