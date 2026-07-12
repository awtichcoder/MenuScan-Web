using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MenuQr.Models.Mongo
{
    public class Category
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Name { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        public string ImageUrl { get; set; } = string.Empty;
        
        public bool Available { get; set; } = true;
    }
}
