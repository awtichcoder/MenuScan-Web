// Areas/Admin/Services/PromotionStatusJob.cs
// BR10.6 - Job nền tự bật/tắt khuyến mãi theo thời gian (chạy mỗi giờ)
using MongoDB.Driver;
using MenuQr.Areas.Admin.Models;

namespace MenuQr.Areas.Admin.Services
{
    public class PromotionStatusJob : BackgroundService
    {
        private readonly IMongoClient _client;
        private readonly string _dbName;
        private readonly ILogger<PromotionStatusJob> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        public PromotionStatusJob(IMongoClient client, IConfiguration config, ILogger<PromotionStatusJob> logger)
        {
            _client = client;
            _dbName = config.GetValue<string>("MongoDBSettings:DatabaseName") ?? "MenuQrDb";
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var col = _client.GetDatabase(_dbName).GetCollection<Promotion>("Promotions");
            // Chạy ngay 1 lần lúc khởi động rồi lặp mỗi giờ
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await SyncStatusesAsync(col, stoppingToken); }
                catch (Exception ex) { _logger.LogWarning(ex, "PromotionStatusJob lỗi khi đồng bộ status"); }

                try { await Task.Delay(Interval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private static async Task SyncStatusesAsync(IMongoCollection<Promotion> col, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var all = await col.Find(FilterDefinition<Promotion>.Empty).ToListAsync(ct);
            foreach (var p in all)
            {
                var newStatus = Promotion.ComputeStatus(now, p.StartDate, p.EndDate);
                if (newStatus != p.Status)
                {
                    var update = Builders<Promotion>.Update.Set(x => x.Status, newStatus);
                    await col.UpdateOneAsync(x => x.Id == p.Id, update, cancellationToken: ct);
                }
            }
        }
    }
}
