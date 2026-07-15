using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace MenuQr.Models
{
    public class DiningTable
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("table_number")]
        public string TableNumber { get; set; } = string.Empty; // Ví dụ: "Ban01", "Ban02"

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty; // Ví dụ: "Bàn số 1", "Bàn số 2"

        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true; // Trạng thái bàn (có cho phép dùng hay không)

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}