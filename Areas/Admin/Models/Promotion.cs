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
        public int DiscountPercent { get; set; }

        [BsonElement("start_date")]
        public DateTime StartDate { get; set; }

        [BsonElement("end_date")]
        public DateTime EndDate { get; set; }

        [BsonElement("dish_ids")]
        public List<string> DishIds { get; set; } = new();

        [BsonElement("status")]
        public string Status { get; set; } = PromotionStatus.Upcoming;

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [BsonElement("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public static class PromotionStatus
    {
        public const string Upcoming = "upcoming";
        public const string Active = "active";
        public const string Ended = "ended";
    }
}
