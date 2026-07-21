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
        public string TableNumber { get; set; } = string.Empty; // VÃ­ dá»¥: "Ban01", "Ban02"

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty; // VÃ­ dá»¥: "BÃ n sá»‘ 1", "BÃ n sá»‘ 2"

        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true; // Tráº¡ng thÃ¡i bÃ n (cÃ³ cho phÃ©p dÃ¹ng hay khÃ´ng)

        [BsonElement("needs_service")]
        public bool NeedsService { get; set; } = false;

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}