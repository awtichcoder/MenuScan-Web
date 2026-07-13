// Areas/Admin/Models/Dish.cs
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace MenuQr.Areas.Admin.Models
{
    public class Dish
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("category_id")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? CategoryId { get; set; }

        [BsonElement("category_name")]
        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        public string CategoryName { get; set; } = null!;

        [BsonElement("name")]
        [Required(ErrorMessage = "Tên món không được để trống")]
        public string Name { get; set; } = null!;

        [BsonElement("base_price")]
        public decimal BasePrice { get; set; }

        [BsonElement("discount_percent")]
        public int DiscountPercent { get; set; }

        [BsonElement("is_available")]
        public bool IsAvailable { get; set; } = true;

        [BsonElement("image_url")]
        public string? ImageUrl { get; set; }

        [BsonElement("specifications")]
        public List<Specification> Specifications { get; set; } = new();
    }

    public class Specification
    {
        [BsonElement("spec_name")]
        public string SpecName { get; set; } = null!;

        [BsonElement("is_required")]
        public bool IsRequired { get; set; }

        [BsonElement("max_choices")]
        public int MaxChoices { get; set; }

        [BsonElement("options")]
        public List<Option> Options { get; set; } = new();
    }

    public class Option
    {
        [BsonElement("name")]
        public string Name { get; set; } = null!;

        [BsonElement("extra_price")]
        public decimal ExtraPrice { get; set; }
    }
}