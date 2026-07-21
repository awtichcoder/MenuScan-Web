using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MenuQr.Areas.Chef.Controllers
{
    [Area("Chef")]
    [Authorize(Roles = "Kitchen,Chef,Admin")]
    public class KitchenBaseController : Controller
    {
    }
}
