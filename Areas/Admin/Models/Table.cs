// Areas/Admin/Models/Table.cs
// Bàn ăn - lưu trong MongoDB, collection "Tables"
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MenuQr.Areas.Admin.Models
{
    public class Table
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("code")]
        public string Code { get; set; } = string.Empty;   // Mã bàn, duy nhất (VD: B01)

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;   // Tên bàn, duy nhất

        [BsonElement("area")]
        public string? Area { get; set; }                  // Khu vực (optional)

        [BsonElement("capacity")]
        public int Capacity { get; set; } = 4;             // Sức chứa > 0

        [BsonElement("status")]
        public string Status { get; set; } = "available";  // available | occupied

        [BsonElement("qr_code_url")]
        public string? QrCodeUrl { get; set; }             // Ảnh QR dạng data URL (base64)

        [BsonElement("qr_token")]
        public string? QrToken { get; set; }               // Token nhúng trong URL QR

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
