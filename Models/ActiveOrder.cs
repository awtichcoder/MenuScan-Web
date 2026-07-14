using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MenuQr.Models
{
    public class ActiveOrder
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("table_number")]
        public string TableNumber { get; set; } = null!;

        [BsonElement("status")]
        public string Status { get; set; } = "Serving";

        // Thêm: Thời gian bàn bắt đầu vào ăn
        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Thêm: Thời gian giỏ hàng có sự thay đổi mới nhất
        [BsonElement("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // ĐÂY NÈ: Thêm trường PaidAt để hết báo lỗi đỏ ở Controller 👇
        [BsonElement("paid_at")]
        public DateTime? PaidAt { get; set; }

        [BsonElement("items")]
        public List<ActiveOrderItem> Items { get; set; } = new List<ActiveOrderItem>();
    }

    public class ActiveOrderItem
    {
        [BsonElement("cart_item_id")]
        public string CartItemId { get; set; } = Guid.NewGuid().ToString();

        [BsonElement("dish_id")]
        public string DishId { get; set; } = null!;

        [BsonElement("dish_name")]
        public string DishName { get; set; } = null!;

        [BsonElement("image_url")]
        public string ImageUrl { get; set; } = string.Empty;

        [BsonElement("quantity")]
        public int Quantity { get; set; }

        [BsonElement("base_price")]
        public decimal BasePrice { get; set; }

        [BsonElement("final_price")]
        public decimal FinalPrice { get; set; } 

        [BsonElement("selected_options")]
        public List<SelectedOption> SelectedOptions { get; set; } = new List<SelectedOption>();

        [BsonElement("note")]
        public string Note { get; set; } = string.Empty;

        [BsonElement("item_status")]
        public string ItemStatus { get; set; } = "Pending"; 

        // Thêm: Lưu lại chính xác thời điểm món này được bấm "Xác nhận báo bếp"
        [BsonElement("ordered_at")]
        public DateTime? OrderedAt { get; set; } 
    }

    public class SelectedOption
    {
        [BsonElement("spec_name")]
        public string SpecName { get; set; } = string.Empty;

        [BsonElement("name")]
        public string Name { get; set; } = null!;

        [BsonElement("extra_price")]
        public decimal ExtraPrice { get; set; }
    }

    public class AddItemRequest
    {
        public string TableNumber { get; set; } = null!;
        public ActiveOrderItem Item { get; set; } = null!;
    }
}