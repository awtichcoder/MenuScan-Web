using MenuQr.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace MenuQr.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PromotionController : Controller
    {
        private readonly IMongoCollection<Promotion> _promotionCollection;
        private readonly IMongoCollection<Dish> _dishCollection;

        public PromotionController(IMongoClient mongoClient, IMongoDatabase operationalDatabase, IConfiguration configuration)
        {
            var adminDatabaseName = configuration["MongoDBSettings:AdminDatabaseName"] ?? "MenuQrAdminDb";
            var adminDatabase = mongoClient.GetDatabase(adminDatabaseName);

            _promotionCollection = adminDatabase.GetCollection<Promotion>("Promotions");
            _dishCollection = operationalDatabase.GetCollection<Dish>("Dishes");
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            await SyncPromotionStatuses();
            ViewBag.Dishes = await _dishCollection.Find(d => !d.IsDeleted && d.IsAvailable).SortBy(d => d.Name).ToListAsync();

            var promotions = await _promotionCollection.Find(_ => true)
                .SortByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(promotions);
        }

        [HttpGet("/api/admin/promotions")]
        public async Task<IActionResult> GetPromotions()
        {
            await SyncPromotionStatuses();
            var promotions = await _promotionCollection.Find(_ => true).SortByDescending(p => p.CreatedAt).ToListAsync();
            return Ok(new { success = true, data = promotions });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Promotion model)
        {
            var validation = await ValidatePromotion(model);
            if (validation != null) return validation;

            model.Name = model.Name.Trim();
            model.DishIds = model.DishIds.Distinct().ToList();
            model.Status = ResolveStatus(model.StartDate, model.EndDate, DateTime.Now);
            model.CreatedAt = DateTime.Now;
            model.UpdatedAt = DateTime.Now;

            await _promotionCollection.InsertOneAsync(model);
            await ApplyPromotionDiscounts(model);

            return Ok(new { success = true, code = "MS06", message = "Tao su kien giam gia thanh cong.", id = model.Id });
        }

        [HttpPost("/api/admin/promotions")]
        public Task<IActionResult> CreatePromotion([FromBody] Promotion model)
        {
            return Create(model);
        }

        private async Task<IActionResult?> ValidatePromotion(Promotion model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Name) || model.DishIds == null || model.DishIds.Count == 0)
                return BadRequest(new { success = false, code = "MS01", message = "Vui long nhap ten va chon it nhat mot mon." });

            if (model.DiscountPercent < 1 || model.DiscountPercent > 100)
                return BadRequest(new { success = false, code = "MS02", message = "Phan tram giam gia phai tu 1 den 100." });

            if (model.StartDate > model.EndDate)
                return BadRequest(new { success = false, code = "MS03", message = "Ngay bat dau phai nho hon hoac bang ngay ket thuc." });

            var activeDishCount = await _dishCollection
                .CountDocumentsAsync(d => model.DishIds.Contains(d.Id!) && d.IsAvailable && !d.IsDeleted);
            if (activeDishCount != model.DishIds.Distinct().Count())
                return BadRequest(new { success = false, code = "MS01", message = "Chi duoc chon mon dang kinh doanh." });

            var overlapFilter = Builders<Promotion>.Filter.AnyIn(p => p.DishIds, model.DishIds);
            overlapFilter &= Builders<Promotion>.Filter.Ne(p => p.Status, PromotionStatus.Ended);
            overlapFilter &= Builders<Promotion>.Filter.Lte(p => p.StartDate, model.EndDate);
            overlapFilter &= Builders<Promotion>.Filter.Gte(p => p.EndDate, model.StartDate);

            if (await _promotionCollection.Find(overlapFilter).AnyAsync())
                return BadRequest(new { success = false, code = "MS04", message = "Mon da thuoc chuong trinh khuyen mai trung thoi gian." });

            return null;
        }

        private async Task SyncPromotionStatuses()
        {
            var promotions = await _promotionCollection.Find(p => p.Status != PromotionStatus.Ended).ToListAsync();
            foreach (var promotion in promotions)
            {
                var status = ResolveStatus(promotion.StartDate, promotion.EndDate, DateTime.Now);
                if (status != promotion.Status)
                {
                    await _promotionCollection.UpdateOneAsync(
                        p => p.Id == promotion.Id,
                        Builders<Promotion>.Update.Set(p => p.Status, status).Set(p => p.UpdatedAt, DateTime.Now));

                    promotion.Status = status;
                }

                await ApplyPromotionDiscounts(promotion);
            }
        }

        private async Task ApplyPromotionDiscounts(Promotion promotion)
        {
            if (promotion.Status == PromotionStatus.Active)
            {
                await _dishCollection.UpdateManyAsync(
                    d => promotion.DishIds.Contains(d.Id!) && !d.IsDeleted,
                    Builders<Dish>.Update.Set(d => d.DiscountPercent, promotion.DiscountPercent).Set(d => d.UpdatedAt, DateTime.Now));
            }
            else if (promotion.Status == PromotionStatus.Ended)
            {
                await _dishCollection.UpdateManyAsync(
                    d => promotion.DishIds.Contains(d.Id!) && !d.IsDeleted,
                    Builders<Dish>.Update.Set(d => d.DiscountPercent, 0).Set(d => d.UpdatedAt, DateTime.Now));
            }
        }

        private static string ResolveStatus(DateTime startDate, DateTime endDate, DateTime now)
        {
            if (now.Date > endDate.Date) return PromotionStatus.Ended;
            if (now.Date >= startDate.Date) return PromotionStatus.Active;
            return PromotionStatus.Upcoming;
        }
    }
}
