using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MenuQr.Areas.Admin.Models; 
using MenuQr.Models;             

namespace MenuQr.Controllers
{
    public class HomeController : Controller
    {
        private readonly IMongoCollection<DiningTable> _tableCollection;
        private readonly IMongoCollection<Category> _categoryCollection;
        private readonly IMongoCollection<Dish> _dishCollection;

        public HomeController(IMongoDatabase mongoDatabase)
        {
            _tableCollection = mongoDatabase.GetCollection<DiningTable>("DiningTables");
            _categoryCollection = mongoDatabase.GetCollection<Category>("Categories");
            _dishCollection = mongoDatabase.GetCollection<Dish>("Dishes");
        }
        public IActionResult PaymentSuccess()
    {
        return View();
    }

        // 1. TRANG CHỦ (HIỂN THỊ DANH MỤC)
       // 1. TRANG CHỦ (CHÀO MỪNG)
        [HttpGet]
        public async Task<IActionResult> Index(string tableId)
        {
            if (string.IsNullOrEmpty(tableId))
            {
                return Content("Vui lòng quét mã QR tại bàn để xem Menu.");
            }

            // 🛑 CHỐT CHẶN TỬ THẦN: Kiểm tra bàn có tồn tại trong hệ thống không!
            var activeTable = await _tableCollection.Find(t => t.TableNumber == tableId && t.IsActive).FirstOrDefaultAsync();
            if (activeTable == null)
            {
                return Content("Mã bàn này không tồn tại trên hệ thống (Bàn ảo)!");
            }

            ViewBag.TableId = activeTable.TableNumber;
            ViewBag.TableName = activeTable.Name; // Gửi thêm tên bàn ra giao diện nếu cần

            var categories = await _categoryCollection.Find(c => c.IsActive).ToListAsync();
            
            return View(categories);
        }

        // 2. TRANG MENU (HIỂN THỊ DANH SÁCH MÓN ĂN TRƯỢT NGANG)
        [HttpGet]
        public async Task<IActionResult> Menu(string tableId, string id = "all")
        {
            if (string.IsNullOrEmpty(tableId)) return Content("Vui lòng quét mã QR.");

            // 🛑 CHỐT CHẶN TỬ THẦN LẦN 2: Chống gõ URL ảo thẳng vào Menu
            var activeTable = await _tableCollection.Find(t => t.TableNumber == tableId && t.IsActive).FirstOrDefaultAsync();
            if (activeTable == null)
            {
                return Content("Mã bàn này không tồn tại trên hệ thống (Bàn ảo)!");
            }

            ViewBag.TableId = activeTable.TableNumber;

            // Lấy danh mục, SẮP XẾP THEO display_order
            var categories = await _categoryCollection.Find(c => c.IsActive)
                                                      .SortBy(c => c.DisplayOrder)
                                                      .ToListAsync();

            List<Dish> dishes;
            string currentName = "Đặt đề cử";

            if (id == "all")
            {
                var allDishes = await _dishCollection.Find(d => d.IsAvailable && !d.IsDeleted).ToListAsync();
                
                // Thuật toán đan xen món ăn (Round-Robin)
                var groupedDishes = allDishes.GroupBy(d => d.CategoryId).ToList();
                dishes = new List<Dish>();
                
                int maxItemsInAGroup = groupedDishes.Any() ? groupedDishes.Max(g => g.Count()) : 0;
                for (int i = 0; i < maxItemsInAGroup; i++)
                {
                    foreach (var group in groupedDishes)
                    {
                        var list = group.ToList();
                        if (i < list.Count)
                        {
                            dishes.Add(list[i]);
                        }
                    }
                }
            }
            else
            {
                dishes = await _dishCollection.Find(d => d.IsAvailable && !d.IsDeleted && d.CategoryId == id).ToListAsync();
                currentName = categories.FirstOrDefault(c => c.Id == id)?.Name ?? "Danh mục";
            }

            var vm = new MenuViewModel
            {
                Categories = categories,
                Dishes = dishes,
                CurrentCategoryId = id,
                CurrentCategoryName = currentName
            };

            return View(vm);
        }
        // Thêm vào trong class HomeController
public IActionResult CheckoutVnPay(string tableId, decimal amount)
{
    // Ở thực tế, bạn sẽ lấy dữ liệu từ Db (ActiveOrders), tạo URL VNPAY và Redirect.
    // Tạm thời hiển thị trang demo báo thành công.
    
    string vnpayDemoUrl = $"https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?amount={amount}&table={tableId}";
    
    // Demo trả về Content cho bạn dễ hình dung luồng đi
    return Content($@"
        <html>
            <body style='font-family:sans-serif; text-align:center; padding: 50px;'>
                <h1 style='color: green;'>Chuyển hướng đến VNPAY thành công!</h1>
                <p>Bàn: <b>{tableId}</b></p>
                <p>Số tiền cần thanh toán: <b style='color:red;'>{amount:N0} VNĐ</b></p>
                <hr/>
                <p>Trong thực tế, hệ thống sẽ tự động redirect sang cổng thanh toán tại đây.</p>
                <a href='/Home/Index?tableId={tableId}'>Quay lại trang chủ</a>
            </body>
        </html>", "text/html");
}
        
    }
}
