using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace MenuQr.Models.Mongo
{
    public class Dish
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string CategoryId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        public double Price { get; set; }
        
        public string Image { get; set; } = string.Empty;
        
        public bool Available { get; set; } = true;

        public List<DishSize> Sizes { get; set; } = new();
        
        public List<DishTopping> Toppings { get; set; } = new();
    }

    public class DishSize
    {
        public string Name { get; set; } = string.Empty;
        public double PriceAdjustment { get; set; }
    }

    public class DishTopping
    {
        public string Name { get; set; } = string.Empty;
        public double Price { get; set; }
    }
}
