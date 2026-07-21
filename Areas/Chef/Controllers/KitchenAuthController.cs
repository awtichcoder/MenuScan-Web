using Microsoft.AspNetCore.Mvc;

namespace MenuQr.Areas.Chef.Controllers
{
    [Area("Chef")]
    public class KitchenAuthController : Controller
    {
        // GET: /Chef/KitchenAuth/Login
        [HttpGet]
        public IActionResult Login()
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // GET: /Chef/KitchenAuth/Logout
        [HttpGet]
        public IActionResult Logout()
        {
            return RedirectToAction("LogoutDirect", "Account", new { area = "" });
        }
    }
}
