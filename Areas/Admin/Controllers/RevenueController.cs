using MenuQr.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MenuQr.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class RevenueController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public RevenueController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string type = "month", DateTime? from = null, DateTime? to = null)
        {
            var data = await BuildStats(type, from, to);
            ViewBag.Type = type;
            ViewBag.From = data.From.ToString("yyyy-MM-dd");
            ViewBag.To = data.To.ToString("yyyy-MM-dd");
            return View(data);
        }

        [HttpGet("/api/admin/revenue-stats")]
        public async Task<IActionResult> GetStats(string type = "month", DateTime? from = null, DateTime? to = null)
        {
            try
            {
                var data = await BuildStats(type, from, to);
                if (data.From > data.To)
                    return BadRequest(new { success = false, code = "MS02", message = "Khoang thoi gian khong hop le." });

                return Ok(new
                {
                    success = true,
                    code = data.Summary.TotalOrders == 0 ? "MS01" : null,
                    summary = data.Summary,
                    chartData = data.ChartData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, code = "MS03", message = ex.Message });
            }
        }

        private async Task<RevenueStatsViewModel> BuildStats(string type, DateTime? from, DateTime? to)
        {
            var today = DateTime.Today;
            var range = ResolveRange(type, from, to, today);

            var invoices = await _dbContext.Invoices
                .Where(i => i.PaymentStatus == "Paid" && i.PaidAt >= range.From && i.PaidAt < range.To.AddDays(1))
                .Select(i => new { PaidAt = i.PaidAt!.Value, i.FinalAmount })
                .ToListAsync();

            var chartData = invoices
                .GroupBy(i => i.PaidAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new RevenueChartPoint(g.Key.ToString("yyyy-MM-dd"), g.Sum(x => x.FinalAmount), g.Count()))
                .ToList();

            var totalRevenue = invoices.Sum(i => i.FinalAmount);
            var totalOrders = invoices.Count;

            return new RevenueStatsViewModel(
                range.From,
                range.To,
                new RevenueSummary(totalRevenue, totalOrders, totalOrders == 0 ? 0 : totalRevenue / totalOrders),
                chartData);
        }

        private static (DateTime From, DateTime To) ResolveRange(string type, DateTime? from, DateTime? to, DateTime today)
        {
            return type?.ToLowerInvariant() switch
            {
                "day" => (today, today),
                "week" => (today.AddDays(-6), today),
                "year" => (new DateTime(today.Year, 1, 1), today),
                "custom" => (from ?? today, to ?? today),
                _ => (new DateTime(today.Year, today.Month, 1), today)
            };
        }
    }

    public record RevenueStatsViewModel(DateTime From, DateTime To, RevenueSummary Summary, List<RevenueChartPoint> ChartData);
    public record RevenueSummary(decimal TotalRevenue, int TotalOrders, decimal AvgOrderValue);
    public record RevenueChartPoint(string Period, decimal Revenue, int Orders);
}
