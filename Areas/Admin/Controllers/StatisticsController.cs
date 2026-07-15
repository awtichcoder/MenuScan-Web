// Areas/Admin/Controllers/StatisticsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MenuQr.Data;
using MenuQr.Areas.Admin.Models;

namespace MenuQr.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class StatisticsController : Controller
    {
        private readonly AppDbContext _db;

        public StatisticsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Index() => View();

        // Chuẩn hóa khoảng ngày: mặc định 30 ngày gần nhất; to lấy hết cả ngày (23:59:59)
        private (DateTime from, DateTime to, bool valid) NormalizeRange(DateTime? from, DateTime? to)
        {
            var toDate = (to ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            var fromDate = (from ?? DateTime.Today.AddDays(-29)).Date;
            return (fromDate, toDate, fromDate <= toDate);
        }

        // UC24 - Tổng quan doanh thu + chuỗi thời gian (groupBy: day | month)
        [HttpGet]
        public async Task<IActionResult> SummaryApi(DateTime? from = null, DateTime? to = null, string groupBy = "day")
        {
            var (fromDate, toDate, valid) = NormalizeRange(from, to);
            if (!valid)
                return BadRequest(new { success = false, code = StatisticsMessages.InvalidRange, message = StatisticsMessages.Text(StatisticsMessages.InvalidRange) });

            // Chỉ tính hóa đơn đã thanh toán trong khoảng
            var paid = _db.Invoices.AsNoTracking()
                .Where(i => i.PaymentStatus == "Paid" && i.PaidAt >= fromDate && i.PaidAt <= toDate);

            var totalRevenue = await paid.SumAsync(i => (decimal?)i.FinalAmount) ?? 0m;
            var totalDiscount = await paid.SumAsync(i => (decimal?)i.TotalDiscount) ?? 0m;
            var invoiceCount = await paid.CountAsync();
            var avgInvoice = invoiceCount > 0 ? totalRevenue / invoiceCount : 0m;

            List<object> series;
            if (groupBy == "month")
            {
                series = await paid
                    .GroupBy(i => new { i.PaidAt.Year, i.PaidAt.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .Select(g => (object)new {
                        label = g.Key.Month + "/" + g.Key.Year,
                        revenue = g.Sum(x => x.FinalAmount),
                        count = g.Count()
                    }).ToListAsync();
            }
            else
            {
                series = await paid
                    .GroupBy(i => i.PaidAt.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => (object)new {
                        label = g.Key,
                        revenue = g.Sum(x => x.FinalAmount),
                        count = g.Count()
                    }).ToListAsync();
            }

            return Ok(new {
                success = true,
                from = fromDate, to = toDate.Date,
                totalRevenue, totalDiscount, invoiceCount, avgInvoice,
                series
            });
        }

        // UC24 - Top món bán chạy (theo số lượng) trong khoảng, chỉ đơn có hóa đơn Paid
        [HttpGet]
        public async Task<IActionResult> TopDishesApi(DateTime? from = null, DateTime? to = null, int top = 10)
        {
            var (fromDate, toDate, valid) = NormalizeRange(from, to);
            if (!valid)
                return BadRequest(new { success = false, code = StatisticsMessages.InvalidRange, message = StatisticsMessages.Text(StatisticsMessages.InvalidRange) });
            if (top < 1) top = 10;

            // OrderDetails của các Order có Invoice Paid trong khoảng
            var query =
                from d in _db.OrderDetails.AsNoTracking()
                join inv in _db.Invoices.AsNoTracking() on d.OrderId equals inv.OrderId
                where inv.PaymentStatus == "Paid" && inv.PaidAt >= fromDate && inv.PaidAt <= toDate
                group d by d.DishName into g
                orderby g.Sum(x => x.Quantity) descending
                select new {
                    dishName = g.Key,
                    quantity = g.Sum(x => x.Quantity),
                    revenue = g.Sum(x => (x.PriceAfterDiscount + x.TotalToppingPrice) * x.Quantity)
                };

            var data = await query.Take(top).ToListAsync();
            return Ok(new { success = true, data });
        }
    }
}
