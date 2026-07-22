// Controllers/OrderController.cs
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration; // THÃŠM DÃ’NG NÃ€Y Äá»‚ Äá»ŒC FILE CONFIG
using MenuQr.Hubs;
using MenuQr.Helpers;
using Microsoft.Extensions.Configuration;
using MenuQr.Models; // Chá»©a ActiveOrder (MongoDB)
 using MenuQr.Data; // Uncomment dÃ²ng nÃ y náº¿u ApplicationDbContext cá»§a báº¡n náº±m trong thÆ° má»¥c Data
// using MenuQr.Areas.Admin.Models; // Uncomment náº¿u class Order, OrderDetail, Invoice náº±m trong nÃ y

namespace MenuQr.Controllers
{
    public class OrderController : Controller
    {
        private readonly IMongoCollection<ActiveOrder> _activeOrderCollection;
        private readonly IMongoCollection<DiningTable> _tableCollection;
        private readonly ApplicationDbContext _sqlDbContext; // Class DbContext của SQL Server (EF Core)
        private readonly IHubContext<StaffHub> _staffHub;
        private readonly IConfiguration _configuration;

        // TiÃªm (Inject) cáº£ 2 Database vÃ o Controller
        public OrderController(
            IMongoDatabase mongoDatabase, 
            ApplicationDbContext sqlDbContext, 
            IHubContext<StaffHub> staffHub,

            IConfiguration configuration)
        {
            _activeOrderCollection = mongoDatabase.GetCollection<ActiveOrder>("ActiveOrders");
            _tableCollection = mongoDatabase.GetCollection<DiningTable>("DiningTables");
            _sqlDbContext = sqlDbContext;
            _staffHub = staffHub; // Khá»Ÿi táº¡o Hub
            _configuration = configuration;
        }
        // 1. API: Tạo URL thanh toán VNPay
    [HttpPost]
    public async Task<IActionResult> CreateVnPayPayment(string tableId)
    {
        var order = await _activeOrderCollection
            .Find(o => o.TableNumber == tableId && o.Status == "Serving")
            .FirstOrDefaultAsync();

        if (order == null) return BadRequest(new { success = false, message = "KhÃ´ng tÃ¬m tháº¥y Ä‘Æ¡n hÃ ng!" });

        // TÃ­nh tá»•ng tiá»n nhá»¯ng mÃ³n Ä‘Ã£ bÃ¡o báº¿p (Ordered)
        decimal totalAmount = order.Items.Where(x => x.ItemStatus == "Ordered").Sum(x => x.FinalPrice * x.Quantity);
        if (totalAmount <= 0) return BadRequest(new { success = false, message = "KhÃ´ng cÃ³ mÃ³n nÃ o Ä‘á»ƒ thanh toÃ¡n!" });

        string vnp_Returnurl = $"{Request.Scheme}://{Request.Host}/Order/VnPayCallback"; 
        string vnp_Url = _configuration["VnPay:BaseUrl"];
        string vnp_TmnCode = _configuration["VnPay:TmnCode"];
        string vnp_HashSecret = _configuration["VnPay:HashSecret"];

        VnPayLibrary vnpay = new VnPayLibrary();
        vnpay.AddRequestData("vnp_Version", "2.1.0");
        vnpay.AddRequestData("vnp_Command", "pay");
        vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
        vnpay.AddRequestData("vnp_Amount", (totalAmount * 100).ToString()); // VNPay báº¯t buá»™c nhÃ¢n 100
        vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
        vnpay.AddRequestData("vnp_CurrCode", "VND");
        vnpay.AddRequestData("vnp_IpAddr", Utils.GetIpAddress(HttpContext));
        vnpay.AddRequestData("vnp_Locale", "vn");
        vnpay.AddRequestData("vnp_OrderInfo", $"Thanh toan don hang ban {tableId}");
        vnpay.AddRequestData("vnp_OrderType", "other");
        vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
        vnpay.AddRequestData("vnp_TxnRef", order.Id.ToString()); // DÃ¹ng Mongo ID lÃ m mÃ£ giao dá»‹ch Ä‘á»ƒ lÃºc vá» dá»… tÃ¬m

        string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);

        // Tráº£ vá» URL Ä‘á»ƒ Javascript tá»± Ä‘á»™ng chuyá»ƒn hÆ°á»›ng khÃ¡ch hÃ ng
        return Ok(new { success = true, url = paymentUrl });
    }

    // 2. API: Nháº­n káº¿t quáº£ tá»« VNPay vÃ  CHUYá»‚N Dá»® LIá»†U SANG SQL SERVER
    [HttpGet]
    public async Task<IActionResult> VnPayCallback()
    {
        if (Request.Query.Count > 0)
        {
            string vnp_HashSecret = _configuration["VnPay:HashSecret"];
            VnPayLibrary vnpay = new VnPayLibrary();

            foreach (var (key, value) in Request.Query)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    vnpay.AddResponseData(key, value.ToString());
                }
            }

            string orderIdMongo = vnpay.GetResponseData("vnp_TxnRef");
            string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
            string vnp_SecureHash = Request.Query["vnp_SecureHash"];

            bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);

            if (checkSignature && vnp_ResponseCode == "00")
            {
                // THANH TOÃN THÃ€NH CÃ”NG -> Báº®T Äáº¦U Äá»’NG Bá»˜ MONGODB SANG SQL SERVER
                var orderMongo = await _activeOrderCollection.Find(o => o.Id == orderIdMongo).FirstOrDefaultAsync();

                if (orderMongo != null && orderMongo.Status == "Serving")
                {
                    // Lá»c nhá»¯ng mÃ³n Ä‘Ã£ ordered Ä‘á»ƒ lÆ°u
                    var orderedItems = orderMongo.Items.Where(i => i.ItemStatus == "Ordered").ToList();
                    decimal subTotal = orderedItems.Sum(i => i.FinalPrice * i.Quantity);

                    // 1. LÆ¯U Báº¢NG Orders
                    var sqlOrder = new Order { // Giáº£ sá»­ class Model SQL cá»§a báº¡n tÃªn lÃ  Order
                        OrderId = orderIdMongo,
                        TableNumber = orderMongo.TableNumber,
                        OrderType = "DineIn",
                        Status = "Completed",
                        CreatedAt = DateTime.Now
                    };
                    _sqlDbContext.Orders.Add(sqlOrder);
                    await _sqlDbContext.SaveChangesAsync(); // LÆ°u Ä‘á»ƒ láº¥y sqlOrder.OrderId (KhÃ³a chÃ­nh)

                    // 2. LÆ¯U Báº¢NG OrderDetails
                    foreach (var item in orderedItems)
                    {
                        var detail = new OrderDetail {
                            OrderId = sqlOrder.OrderId, // KhÃ³a ngoáº¡i
                            DishId = item.DishId,
                            DishName = item.DishName,
                            CategoryName = "", // MongoDB khÃ´ng lÆ°u tÃªn danh má»¥c, báº¡n cÃ³ thá»ƒ bá»• sung sau
                            Quantity = item.Quantity,
                            BasePrice = item.BasePrice,
                            DiscountPercent = 0,
                            PriceAfterDiscount = item.FinalPrice,
                            TotalToppingPrice = item.FinalPrice - item.BasePrice,
                            SelectedOptionsJson = JsonSerializer.Serialize(item.SelectedOptions), // Ã‰p máº£ng thÃ nh chuá»—i JSON
                            ItemNote = item.Note
                        };
                        _sqlDbContext.OrderDetails.Add(detail);
                    }

                    // 3. LÆ¯U Báº¢NG Invoices
                    var invoice = new Invoice {
                       OrderId = sqlOrder.OrderId.ToString(),
                        SubTotal = subTotal,
                        TotalDiscount = 0,
                        FinalAmount = subTotal,
                        PaymentMethod = "VNPay",
                        PaymentStatus = "Paid",
                        PaidAt = DateTime.Now
                    };
                    _sqlDbContext.Invoices.Add(invoice);

                    // 4. LÆ¯U Táº¤T Cáº¢ VÃ€O SQL SERVER
                    await _sqlDbContext.SaveChangesAsync();

                    // 5. Cáº¬P NHáº¬T TRáº NG THÃI MONGODB THÃ€NH COMPLETED (KhÃ³a sá»•)
                    foreach (var item in orderMongo.Items.Where(i => i.ItemStatus == "Ordered"))
                    {
                        item.ItemStatus = "Paid";
                    }
                    var update = Builders<ActiveOrder>.Update
                        .Set(o => o.Status, "Completed")
                        .Set(o => o.PaidAt, DateTime.Now)
                        .Set(o => o.Items, orderMongo.Items);
                    await _activeOrderCollection.UpdateOneAsync(o => o.Id == orderIdMongo, update);

                    // Broadcast order status change to kitchen so cards vanish in real-time
                    await _staffHub.Clients.All.SendAsync("OrderUpdated", new { orderId = orderIdMongo, status = "Completed", tableNumber = orderMongo.TableNumber });

                    // Chuyá»ƒn hÆ°á»›ng khÃ¡ch vá»  trang thÃ´ng bÃ¡o thÃ nh cÃ´ng
                    return RedirectToAction("PaymentSuccess", "Home"); 
                }
            }
        }
        
        // Náº¿u tháº¥t báº¡i hoáº·c huá»· thanh toÃ¡n
        return Content("Thanh toÃ¡n tháº¥t báº¡i hoáº·c chá»¯ kÃ½ khÃ´ng há»£p lá»‡!");
    }

        // ==============================================================
        // 1. API NHáº¬N YÃŠU Cáº¦U "XÃC NHáº¬N Gá»ŒI MÃ“N" Tá»ª GIá»Ž HÃ€NG GIAO DIá»†N
        // ==============================================================
        [HttpPost]
        public async Task<IActionResult> ConfirmOrderApi([FromBody] ActiveOrder incomingOrder)
        {
            if (incomingOrder == null || string.IsNullOrEmpty(incomingOrder.TableNumber))
            {
                return BadRequest(new { success = false, message = "Dá»¯ liá»‡u khÃ´ng há»£p lá»‡" });
            }

            // TÃ¬m xem bÃ n nÃ y Ä‘Ã£ cÃ³ phiÃªn Äƒn nÃ o Ä‘ang "Serving" chÆ°a
            var existingOrder = await _activeOrderCollection
                .Find(o => o.TableNumber == incomingOrder.TableNumber && o.Status == "Serving")
                .FirstOrDefaultAsync();

            if (existingOrder == null)
            {
                // BÃ n trá»‘ng (láº§n Ä‘áº§u gá» i mÃ³n) -> LÆ°u tháº³ng vÃ o MongoDB
                incomingOrder.Status = "Serving";
                await _activeOrderCollection.InsertOneAsync(incomingOrder);
            }
            else
            {
                // BÃ n Ä‘ang Äƒn (gá» i thÃªm mÃ³n) -> GhÃ©p danh sÃ¡ch mÃ³n má»›i vÃ o danh sÃ¡ch mÃ³n cÅ©
                existingOrder.Items.AddRange(incomingOrder.Items);
                
                var update = Builders<ActiveOrder>.Update.Set(o => o.Items, existingOrder.Items);
                await _activeOrderCollection.UpdateOneAsync(o => o.Id == existingOrder.Id, update);
            }

            // Broadcast real-time signal via SignalR to kitchen
            await _staffHub.Clients.All.SendAsync("NewOrder", incomingOrder.TableNumber);

            return Ok(new { success = true, message = "Ä Ã£ gá»­i thÃ´ng tin xuá»‘ng báº¿p!" });
        }


        // ==============================================================
        // 2. URL Xá»¬ LÃ  KHI VNPAY THANH TOÃ N XONG TRáº¢ Káº¾T QUáº¢ Vá»€
        // ==============================================================
        [HttpGet]
        public async Task<IActionResult> VnpayReturn()
        {
            // Trong thá»±c táº¿, VNPAY sáº½ tráº£ vá» má»™t Ä‘á»‘ng tham sá»‘ qua QueryString.
            // á»ž Ä‘Ã¢y mÃ¬nh láº¥y vÃ­ dá»¥ 2 tham sá»‘ quan trá»ng nháº¥t:
            string tableNumber = Request.Query["vnp_OrderInfo"].ToString(); 
            string responseCode = Request.Query["vnp_ResponseCode"].ToString();

            if (responseCode == "00") // "00" lÃ  mÃ£ Giao dá»‹ch thÃ nh cÃ´ng cá»§a VNPAY
            {
                // 1. RÃºt toÃ n bá»™ dá»¯ liá»‡u giá» hÃ ng cá»§a bÃ n nÃ y tá»« MongoDB ra
                var activeOrder = await _activeOrderCollection
                    .Find(o => o.TableNumber == tableNumber && o.Status == "Serving")
                    .FirstOrDefaultAsync();

                if (activeOrder != null)
                {
                    // 2. Báº®T Äáº¦U TRANSACTION Äá»‚ LÆ¯U VÃ€O SQL SERVER
                    using var transaction = await _sqlDbContext.Database.BeginTransactionAsync();
                    try
                    {
                        // 2.1 Táº¡o ÄÆ¡n hÃ ng chÃ­nh (Orders)
                        var sqlOrder = new Order // Class SQL cá»§a báº¡n
                        {
                            OrderId = activeOrder.Id,
                            TableNumber = activeOrder.TableNumber,
                            OrderType = "Dine-in",
                            Status = "Completed",
                            CreatedAt = DateTime.Now
                        };
                        _sqlDbContext.Orders.Add(sqlOrder);
                        await _sqlDbContext.SaveChangesAsync(); // Cháº¡y Ä‘á»ƒ EF Core sinh ra sqlOrder.OrderId

                        // 2.2 Táº¡o Chi tiáº¿t tá»«ng mÃ³n (OrderDetails)
                        decimal subTotal = 0;
                        foreach (var item in activeOrder.Items)
                        {
                            var totalTopping = item.SelectedOptions.Sum(x => x.ExtraPrice);

                            var sqlDetail = new OrderDetail
                            {
                                OrderId = sqlOrder.OrderId,
                                DishId = item.DishId,
                                DishName = item.DishName,
                                CategoryName = "N/A", 
                                Quantity = item.Quantity,
                                BasePrice = item.BasePrice,
                                PriceAfterDiscount = item.FinalPrice, 
                                TotalToppingPrice = totalTopping,
                                // Chuyá»ƒn máº£ng Topping thÃ nh JSON String Ä‘á»ƒ nhÃ©t vÃ o 1 cá»™t SQL
                                SelectedOptionsJson = JsonSerializer.Serialize(item.SelectedOptions),
                                ItemNote = item.Note
                            };
                            
                            subTotal += (sqlDetail.PriceAfterDiscount + sqlDetail.TotalToppingPrice) * sqlDetail.Quantity;
                            _sqlDbContext.OrderDetails.Add(sqlDetail);
                        }

                        // 2.3 Táº¡o HÃ³a Ä‘Æ¡n (Invoices)
                        var sqlInvoice = new Invoice
                        {
                            OrderId = sqlOrder.OrderId.ToString(),
                            CashierId = null, // VNPAY nÃªn khÃ´ng qua thu ngÃ¢n
                            SubTotal = subTotal,
                            TotalDiscount = 0, 
                            FinalAmount = subTotal,
                            PaymentMethod = "VNPAY",
                            PaymentStatus = "Success",
                            PaidAt = DateTime.Now
                        };
                        _sqlDbContext.Invoices.Add(sqlInvoice);
                        
                        // 2.4 LÆ°u toÃ n bá»™ vÃ o SQL Server
                        await _sqlDbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // 3. XÃ“A TRá» NG GIá»Ž HÃ€NG TRONG MONGODB (KhÃ³a bÃ n / Giáº£i phÃ³ng bÃ n)
                        await _activeOrderCollection.DeleteOneAsync(o => o.Id == activeOrder.Id);

                        // Broadcast order status change to kitchen so cards vanish in real-time
                        await _staffHub.Clients.All.SendAsync("OrderUpdated", new { orderId = activeOrder.Id, status = "Completed", tableNumber = activeOrder.TableNumber });

                        // Tráº£ vá»  giao diá»‡n thÃ´ng bÃ¡o thÃ nh cÃ´ng cho khÃ¡ch hÃ ng
                        return View("PaymentSuccess", new { Table = tableNumber, Amount = subTotal });
                    }
                    catch (Exception ex)
                    {
                        // Náº¿u cÃ³ báº¥t ká»³ lá»—i nÃ o khi lÆ°u SQL (Rá»›t máº¡ng, lá»—i khÃ³a ngoáº¡i...) -> Há»§y toÃ n bá»™ thao tÃ¡c SQL
                        await transaction.RollbackAsync();
                        return Content($"Lá»—i há»‡ thá»‘ng khi lÆ°u hÃ³a Ä‘Æ¡n: {ex.Message}");
                    }
                }

                return Content("BÃ n nÃ y Ä‘Ã£ Ä‘Æ°á»£c thanh toÃ¡n trÆ°á»›c Ä‘Ã³ hoáº·c khÃ´ng tá»“n táº¡i Ä‘Æ¡n hÃ ng.");
            }

            // Náº¿u responseCode khÃ¡c "00" -> KhÃ¡ch hÃ ng há»§y giao dá»‹ch hoáº·c tháº» khÃ´ng Ä‘á»§ tiá»n
            return Content("Giao dá»‹ch thanh toÃ¡n VNPAY bá»‹ há»§y hoáº·c tháº¥t báº¡i.");
        }
        // Láº¥y dá»¯ liá»‡u giá» hÃ ng hiá»‡n táº¡i cá»§a bÃ n
[HttpGet]
public async Task<IActionResult> GetCartMongo(string tableId)
{
    var order = await _activeOrderCollection
        .Find(o => o.TableNumber == tableId && o.Status == "Serving")
        .FirstOrDefaultAsync();
    
    return Json(order?.Items ?? new List<ActiveOrderItem>());
}

// ThÃªm 1 mÃ³n vÃ o MongoDB
[HttpPost]
public async Task<IActionResult> AddItemToMongo([FromBody] AddItemRequest req)
{
    var order = await _activeOrderCollection
        .Find(o => o.TableNumber == req.TableNumber && o.Status == "Serving")
        .FirstOrDefaultAsync();

    if (order == null)
    {
        // Náº¿u bÃ n chÆ°a gá»i gÃ¬, táº¡o order má»›i
        var newOrder = new ActiveOrder 
        { 
            TableNumber = req.TableNumber, 
            Status = "Serving", 
            Items = new List<ActiveOrderItem> { req.Item } 
        };
        await _activeOrderCollection.InsertOneAsync(newOrder);
    }
    else
    {
        // BÃ n Ä‘ang Äƒn, nhÃ©t thÃªm mÃ³n vÃ o máº£ng Items
        var update = Builders<ActiveOrder>.Update.Push(o => o.Items, req.Item);
        await _activeOrderCollection.UpdateOneAsync(o => o.Id == order.Id, update);
    }
    return Ok(new { success = true });
}

// XÃ³a 1 mÃ³n "Pending"
[HttpPost]
public async Task<IActionResult> RemovePendingItem(string tableId, string cartItemId)
{
    // DÃ¹ng PullFilter Ä‘á»ƒ lÃ´i cÃ¡i mÃ³n cÃ³ CartItemId tÆ°Æ¡ng á»©ng ra khá»i máº£ng Items
    var update = Builders<ActiveOrder>.Update.PullFilter(o => o.Items, 
        i => i.CartItemId == cartItemId && i.ItemStatus == "Pending");
        
    await _activeOrderCollection.UpdateOneAsync(o => o.TableNumber == tableId && o.Status == "Serving", update);
    return Ok(new { success = true });
}
[HttpPost]
        public async Task<IActionResult> CallStaff(string tableId)
        {
            if (string.IsNullOrWhiteSpace(tableId))
            {
                return BadRequest(new { success = false, message = "Khong xac dinh duoc ban." });
            }

            string time = DateTime.Now.ToString("HH:mm");

            await _tableCollection.UpdateOneAsync(
                t => t.TableNumber == tableId && t.IsActive,
                Builders<DiningTable>.Update.Set(t => t.NeedsService, true));
            
            // "ReceiveStaffCall" la ten ma su kien. Man hinh nhan vien phai dang ky ten nay moi nghe duoc.
            await _staffHub.Clients.All.SendAsync("ReceiveStaffCall", tableId, time);
            
            return Ok(new { success = true });
        }
        
[HttpPost]
public async Task<IActionResult> CheckoutOrder(string tableId, string paymentMethod = "Cash")
{
    // TÃ¬m cÃ¡i bÃ n Ä‘ang Äƒn ("Serving")
    var order = await _activeOrderCollection
        .Find(o => o.TableNumber == tableId && o.Status == "Serving")
        .FirstOrDefaultAsync();

    if (order != null)
    {
        // Láº·p qua tá»«ng mÃ³n Äƒn vÃ  Ä‘á»•i item_status sang "Paid"
        foreach(var item in order.Items)
        {
            if (item.ItemStatus == "Ordered")
            {
                item.ItemStatus = "Paid"; 
            }
        }

        // Cáº­p nháº­t toÃ n bá»™ thÃ´ng tin
        var update = Builders<ActiveOrder>.Update
            .Set(o => o.Status, "Completed")     // KhÃ³a sá»• cÃ¡i bÃ n
            .Set(o => o.PaidAt, DateTime.Now)    // LÆ°u giá» tÃ­nh tiá»n
            .Set(o => o.UpdatedAt, DateTime.Now)
            .Set(o => o.Items, order.Items);     // Ghi Ä‘Ã¨ láº¡i máº£ng mÃ³n Äƒn

        await _activeOrderCollection.UpdateOneAsync(o => o.Id == order.Id, update);
        
        return Ok(new { success = true, message = "Thanh toÃ¡n thÃ nh cÃ´ng!" });
    }
    
    return BadRequest(new { success = false, message = "KhÃ´ng tÃ¬m tháº¥y Ä‘Æ¡n hÃ ng!" });
}
// XÃ¡c nháº­n toÃ n bá»™ mÃ³n "Pending" thÃ nh "Ordered"
// XÃ¡c nháº­n toÃ n bá»™ mÃ³n "Pending" thÃ nh "Ordered" Ä‘áº©y xuá»‘ng Báº¿p
[HttpPost]
public async Task<IActionResult> ConfirmPendingItems(string tableId)
{
    // 1. TÃ¬m order cá»§a bÃ n hiá»‡n táº¡i
    var order = await _activeOrderCollection
        .Find(o => o.TableNumber == tableId && o.Status == "Serving")
        .FirstOrDefaultAsync();

    if (order != null)
    {
        bool hasChanges = false;
        
        // 2. Láº·p qua danh sÃ¡ch mÃ³n, Ä‘á»•i tráº¡ng thÃ¡i vÃ  gáº¯n thá»i gian bÃ¡o báº¿p
        foreach(var item in order.Items)
        {
            if (item.ItemStatus == "Pending")
            {
                item.ItemStatus = "Ordered"; // Báº¿p sáº½ chá»‰ query nhá»¯ng mÃ³n cÃ³ chá»¯ "Ordered" nÃ y
                item.OrderedAt = DateTime.Now; // GiÃºp Báº¿p biáº¿t lÃ m mÃ³n nÃ o trÆ°á»›c
                hasChanges = true;
            }
        }

        // 3. Náº¿u cÃ³ mÃ³n Ä‘Æ°á»£c cáº­p nháº­t, lÆ°u tháº³ng xuá»‘ng MongoDB
        if (hasChanges)
        {
            order.UpdatedAt = DateTime.Now; // Cáº­p nháº­t luÃ´n thá»i gian thay Ä‘á»•i cá»§a giá» hÃ ng

            var update = Builders<ActiveOrder>.Update
                .Set(o => o.Items, order.Items) // Ghi Ä‘Ã¨ láº¡i máº£ng Items Ä‘Ã£ Ä‘á»•i tráº¡ng thÃ¡i
                .Set(o => o.UpdatedAt, order.UpdatedAt);

            await _activeOrderCollection.UpdateOneAsync(o => o.Id == order.Id, update);
            
            // Broadcast real-time signal via SignalR
            await _staffHub.Clients.All.SendAsync("NewOrder", tableId);
        }
    }
    
    return Ok(new { success = true });
}
    }
    
}
