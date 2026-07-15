// Areas/Admin/Controllers/DashboardController.cs
using Microsoft.AspNetCore.Mvc;

namespace MenuQr.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class DashboardController : Controller
    {
        // Trang menu chính của khu vực Admin
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}
