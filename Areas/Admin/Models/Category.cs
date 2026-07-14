// Areas/Admin/Models/Category.cs
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MenuQr.Areas.Admin.Models
{
    public class Category
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("display_order")]
        public int DisplayOrder { get; set; } = 1;

        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true;
        // BỔ SUNG THUỘC TÍNH NÀY
        [BsonElement("image_url")]
        public string? ImageUrl { get; set; }
        [BsonElement("updated_at")]
            public DateTime UpdatedAt { get; set; }
    }
}