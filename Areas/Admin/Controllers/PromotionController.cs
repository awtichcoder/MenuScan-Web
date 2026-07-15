// Areas/Admin/Controllers/PromotionController.cs
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MenuQr.Areas.Admin.Models;

namespace MenuQr.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class PromotionController : Controller
    {
        private readonly IMongoCollection<Promotion> _promotions;
        private readonly IMongoCollection<Dish> _dishes;

        public PromotionController(IMongoDatabase db)
        {
            _promotions = db.GetCollection<Promotion>("Promotions");
            _dishes = db.GetCollection<Dish>("Dishes");
        }

        [HttpGet]
        public IActionResult Index() => View();

        // Danh sách KM (status tính lại theo hiện tại để hiển thị luôn đúng)
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var now = DateTime.UtcNow;
            var list = await _promotions.Find(_ => true).SortByDescending(p => p.CreatedAt).ToListAsync();
            var data = list.Select(p => new {
                p.Id, p.Name, p.DiscountPercent, p.StartDate, p.EndDate,
                p.DishNames,
                dishCount = p.DishIds.Count,
                status = Promotion.ComputeStatus(now, p.StartDate, p.EndDate)
            });
            return Ok(new { success = true, data });
        }

        // Món đang kinh doanh cho multi-select (BR10.5)
        [HttpGet]
        public async Task<IActionResult> ActiveDishes()
        {
            var list = await _dishes.Find(d => d.IsAvailable)
                .Project(d => new { d.Id, d.Name }).ToListAsync();
            return Ok(new { success = true, data = list });
        }

        // UC25 - Tạo khuyến mãi
        [HttpPost]
        public async Task<IActionResult> CreateApi([FromBody] Promotion input)
        {
            // MS01 - thiếu tên hoặc chưa chọn món
            if (input == null || string.IsNullOrWhiteSpace(input.Name) || input.DishIds == null || input.DishIds.Count == 0)
                return BadRequest(Err(PromotionMessages.MissingField));

            // MS02 - % trong 1..100
            if (input.DiscountPercent < 1 || input.DiscountPercent > 100)
                return BadRequest(Err(PromotionMessages.InvalidPercent));

            // MS03 - start <= end
            if (input.StartDate.Date > input.EndDate.Date)
                return BadRequest(Err(PromotionMessages.InvalidDate));

            // BR10.5 - chỉ món đang kinh doanh; đồng thời lấy tên để denormalize
            var selected = await _dishes.Find(d => input.DishIds.Contains(d.Id!)).ToListAsync();
            if (selected.Count != input.DishIds.Count || selected.Any(d => !d.IsAvailable))
                return BadRequest(Err(PromotionMessages.InactiveDish));

            // BR10.4 - overlap: món đã thuộc KM khác (chưa ended) trùng khoảng thời gian
            var overlap = Builders<Promotion>.Filter.And(
                Builders<Promotion>.Filter.AnyIn(p => p.DishIds, input.DishIds),
                Builders<Promotion>.Filter.Lte(p => p.StartDate, input.EndDate),
                Builders<Promotion>.Filter.Gte(p => p.EndDate, input.StartDate)
            );
            if (await _promotions.Find(overlap).AnyAsync())
                return Conflict(Err(PromotionMessages.Overlap));

            try
            {
                var now = DateTime.UtcNow;
                input.Name = input.Name.Trim();
                input.DishNames = selected.Select(d => d.Name).ToList();
                input.Status = Promotion.ComputeStatus(now, input.StartDate, input.EndDate);
                input.CreatedAt = now;
                await _promotions.InsertOneAsync(input);
                return Ok(new { success = true, code = PromotionMessages.CreateOk, message = PromotionMessages.Text(PromotionMessages.CreateOk), id = input.Id });
            }
            catch (Exception)
            {
                return StatusCode(500, Err(PromotionMessages.SaveError));
            }
        }

        // Xóa KM
        [HttpDelete]
        public async Task<IActionResult> DeleteApi(string id)
        {
            var res = await _promotions.DeleteOneAsync(p => p.Id == id);
            if (res.DeletedCount == 0)
                return NotFound(Err(PromotionMessages.NotFound));
            return Ok(new { success = true, message = "Đã xóa khuyến mãi." });
        }

        private static object Err(string code) => new { success = false, code, message = PromotionMessages.Text(code) };
    }
}
