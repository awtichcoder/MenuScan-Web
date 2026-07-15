// Areas/Admin/Models/Promotion.cs
// Sự kiện giảm giá theo % (UC25) - lưu MongoDB, collection "Promotions"
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MenuQr.Areas.Admin.Models
{
    public class Promotion
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("discount_percent")]
        public int DiscountPercent { get; set; }   // 1..100

        [BsonElement("start_date")]
        public DateTime StartDate { get; set; }

        [BsonElement("end_date")]
        public DateTime EndDate { get; set; }

        // Danh sách ObjectId món (khớp Dishes._id bên Mongo)
        [BsonElement("dish_ids")]
        public List<string> DishIds { get; set; } = new();

        // Denormalize tên món để hiển thị nhanh (không phải join)
        [BsonElement("dish_names")]
        public List<string> DishNames { get; set; } = new();

        [BsonElement("status")]
        public string Status { get; set; } = "upcoming"; // upcoming | active | ended

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; }

        // Tính status theo thời điểm now (dùng chung controller + job nền BR10.6)
        public static string ComputeStatus(DateTime now, DateTime start, DateTime end)
        {
            if (now < start) return "upcoming";
            if (now > end) return "ended";
            return "active";
        }
    }
}
