using MenuQr.Models.Mongo;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace MenuQr.Services
{
    public class MongoDbService
    {
        private readonly IMongoDatabase _database;

        public MongoDbService(IConfiguration configuration)
        {
            var connectionString = configuration.GetSection("MongoDbSettings:ConnectionString").Value 
                                   ?? "mongodb://localhost:27017";
            var databaseName = configuration.GetSection("MongoDbSettings:DatabaseName").Value 
                               ?? "MenuScanDb";

            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);
        }

        public IMongoCollection<Category> Categories => 
            _database.GetCollection<Category>("Categories");

        public IMongoCollection<Dish> Dishes => 
            _database.GetCollection<Dish>("Dishes");

        public IMongoCollection<ActiveOrder> ActiveOrders => 
            _database.GetCollection<ActiveOrder>("ActiveOrders");
    }
}
