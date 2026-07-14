// Controllers/OrderController.cs
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration; // THÊM DÒNG NÀY ĐỂ ĐỌC FILE CONFIG
using MenuQr.Hubs;
using MenuQr.Helpers;
using Microsoft.Extensions.Configuration;
using MenuQr.Models; // Chứa ActiveOrder (MongoDB)
 using MenuQr.Data; // Uncomment dòng này nếu ApplicationDbContext của bạn nằm trong thư mục Data
// using MenuQr.Areas.Admin.Models; // Uncomment nếu class Order, OrderDetail, Invoice nằm trong này

namespace MenuQr.Controllers
{
    public class OrderController : Controller
    {
        private readonly IMongoCollection<ActiveOrder> _activeOrderCollection;
        private readonly ApplicationDbContext _sqlDbContext; // Class DbContext của SQL Server (EF Core)
        private readonly IHubContext<StaffHub> _staffHub;
        private readonly IConfiguration _configuration;

        // Tiêm (Inject) cả 2 Database vào Controller
        public OrderController(
            IMongoDatabase mongoDatabase, 
            ApplicationDbContext sqlDbContext, 
            IHubContext<StaffHub> staffHub,
            IConfiguration configuration)
        {
            _activeOrderCollection = mongoDatabase.GetCollection<ActiveOrder>("ActiveOrders");
            _sqlDbContext = sqlDbContext;
            _staffHub = staffHub; // Khởi tạo Hub
            _configuration = configuration;
        }
        // 1. API: Tạo URL thanh toán VNPay
    [HttpPost]
    public async Task<IActionResult> CreateVnPayPayment(string tableId)
    {
        var order = await _activeOrderCollection
            .Find(o => o.TableNumber == tableId && o.Status == "Serving")
            .FirstOrDefaultAsync();

        if (order == null) return BadRequest(new { success = false, message = "Không tìm thấy đơn hàng!" });

        // Tính tổng tiền những món đã báo bếp (Ordered)
        decimal totalAmount = order.Items.Where(x => x.ItemStatus == "Ordered").Sum(x => x.FinalPrice * x.Quantity);
        if (totalAmount <= 0) return BadRequest(new { success = false, message = "Không có món nào để thanh toán!" });

        string vnp_Returnurl = $"{Request.Scheme}://{Request.Host}/Order/VnPayCallback"; 
        string vnp_Url = _configuration["VnPay:BaseUrl"];
        string vnp_TmnCode = _configuration["VnPay:TmnCode"];
        string vnp_HashSecret = _configuration["VnPay:HashSecret"];

        VnPayLibrary vnpay = new VnPayLibrary();
        vnpay.AddRequestData("vnp_Version", "2.1.0");
        vnpay.AddRequestData("vnp_Command", "pay");
        vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
        vnpay.AddRequestData("vnp_Amount", (totalAmount * 100).ToString()); // VNPay bắt buộc nhân 100
        vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
        vnpay.AddRequestData("vnp_CurrCode", "VND");
        vnpay.AddRequestData("vnp_IpAddr", Utils.GetIpAddress(HttpContext));
        vnpay.AddRequestData("vnp_Locale", "vn");
        vnpay.AddRequestData("vnp_OrderInfo", $"Thanh toan don hang ban {tableId}");
        vnpay.AddRequestData("vnp_OrderType", "other");
        vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
        vnpay.AddRequestData("vnp_TxnRef", order.Id.ToString()); // Dùng Mongo ID làm mã giao dịch để lúc về dễ tìm

        string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);

        // Trả về URL để Javascript tự động chuyển hướng khách hàng
        return Ok(new { success = true, url = paymentUrl });
    }

    // 2. API: Nhận kết quả từ VNPay và CHUYỂN DỮ LIỆU SANG SQL SERVER
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
                // THANH TOÁN THÀNH CÔNG -> BẮT ĐẦU ĐỒNG BỘ MONGODB SANG SQL SERVER
                var orderMongo = await _activeOrderCollection.Find(o => o.Id == orderIdMongo).FirstOrDefaultAsync();

                if (orderMongo != null && orderMongo.Status == "Serving")
                {
                    // Lọc những món đã ordered để lưu
                    var orderedItems = orderMongo.Items.Where(i => i.ItemStatus == "Ordered").ToList();
                    decimal subTotal = orderedItems.Sum(i => i.FinalPrice * i.Quantity);

                    // 1. LƯU BẢNG Orders
                    var sqlOrder = new Order { // Giả sử class Model SQL của bạn tên là Order
                        TableNumber = orderMongo.TableNumber,
                        OrderType = "DineIn",
                        Status = "Completed",
                        CreatedAt = DateTime.Now
                    };
                    _sqlDbContext.Orders.Add(sqlOrder);
                    await _sqlDbContext.SaveChangesAsync(); // Lưu để lấy sqlOrder.OrderId (Khóa chính)

                    // 2. LƯU BẢNG OrderDetails
                    foreach (var item in orderedItems)
                    {
                        var detail = new OrderDetail {
                            OrderId = sqlOrder.OrderId, // Khóa ngoại
                            DishId = item.DishId,
                            DishName = item.DishName,
                            CategoryName = "", // MongoDB không lưu tên danh mục, bạn có thể bổ sung sau
                            Quantity = item.Quantity,
                            BasePrice = item.BasePrice,
                            DiscountPercent = 0,
                            PriceAfterDiscount = item.FinalPrice,
                            TotalToppingPrice = item.FinalPrice - item.BasePrice,
                            SelectedOptionsJson = JsonSerializer.Serialize(item.SelectedOptions), // Ép mảng thành chuỗi JSON
                            ItemNote = item.Note
                        };
                        _sqlDbContext.OrderDetails.Add(detail);
                    }

                    // 3. LƯU BẢNG Invoices
                    var invoice = new Invoice {
                        OrderId = sqlOrder.OrderId,
                        SubTotal = subTotal,
                        TotalDiscount = 0,
                        FinalAmount = subTotal,
                        PaymentMethod = "VNPay",
                        PaymentStatus = "Paid",
                        PaidAt = DateTime.Now
                    };
                    _sqlDbContext.Invoices.Add(invoice);

                    // 4. LƯU TẤT CẢ VÀO SQL SERVER
                    await _sqlDbContext.SaveChangesAsync();

                    // 5. CẬP NHẬT TRẠNG THÁI MONGODB THÀNH COMPLETED (Khóa sổ)
                    foreach (var item in orderMongo.Items.Where(i => i.ItemStatus == "Ordered"))
                    {
                        item.ItemStatus = "Paid";
                    }
                    var update = Builders<ActiveOrder>.Update
                        .Set(o => o.Status, "Completed")
                        .Set(o => o.PaidAt, DateTime.Now)
                        .Set(o => o.Items, orderMongo.Items);
                    await _activeOrderCollection.UpdateOneAsync(o => o.Id == orderIdMongo, update);

                    // Chuyển hướng khách về trang thông báo thành công
                    return RedirectToAction("PaymentSuccess", "Home"); 
                }
            }
        }
        
        // Nếu thất bại hoặc huỷ thanh toán
        return Content("Thanh toán thất bại hoặc chữ ký không hợp lệ!");
    }

        // ==============================================================
        // 1. API NHẬN YÊU CẦU "XÁC NHẬN GỌI MÓN" TỪ GIỎ HÀNG GIAO DIỆN
        // ==============================================================
        [HttpPost]
        public async Task<IActionResult> ConfirmOrderApi([FromBody] ActiveOrder incomingOrder)
        {
            if (incomingOrder == null || string.IsNullOrEmpty(incomingOrder.TableNumber))
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
            }

            // Tìm xem bàn này đã có phiên ăn nào đang "Serving" chưa
            var existingOrder = await _activeOrderCollection
                .Find(o => o.TableNumber == incomingOrder.TableNumber && o.Status == "Serving")
                .FirstOrDefaultAsync();

            if (existingOrder == null)
            {
                // Bàn trống (lần đầu gọi món) -> Lưu thẳng vào MongoDB
                incomingOrder.Status = "Serving";
                await _activeOrderCollection.InsertOneAsync(incomingOrder);
            }
            else
            {
                // Bàn đang ăn (gọi thêm món) -> Ghép danh sách món mới vào danh sách món cũ
                existingOrder.Items.AddRange(incomingOrder.Items);
                
                var update = Builders<ActiveOrder>.Update.Set(o => o.Items, existingOrder.Items);
                await _activeOrderCollection.UpdateOneAsync(o => o.Id == existingOrder.Id, update);
            }

            return Ok(new { success = true, message = "Đã gửi thông tin xuống bếp!" });
        }


        // ==============================================================
        // 2. URL XỬ LÝ KHI VNPAY THANH TOÁN XONG TRẢ KẾT QUẢ VỀ
        // ==============================================================
        [HttpGet]
        public async Task<IActionResult> VnpayReturn()
        {
            // Trong thực tế, VNPAY sẽ trả về một đống tham số qua QueryString.
            // Ở đây mình lấy ví dụ 2 tham số quan trọng nhất:
            string tableNumber = Request.Query["vnp_OrderInfo"].ToString(); 
            string responseCode = Request.Query["vnp_ResponseCode"].ToString();

            if (responseCode == "00") // "00" là mã Giao dịch thành công của VNPAY
            {
                // 1. Rút toàn bộ dữ liệu giỏ hàng của bàn này từ MongoDB ra
                var activeOrder = await _activeOrderCollection
                    .Find(o => o.TableNumber == tableNumber && o.Status == "Serving")
                    .FirstOrDefaultAsync();

                if (activeOrder != null)
                {
                    // 2. BẮT ĐẦU TRANSACTION ĐỂ LƯU VÀO SQL SERVER
                    using var transaction = await _sqlDbContext.Database.BeginTransactionAsync();
                    try
                    {
                        // 2.1 Tạo Đơn hàng chính (Orders)
                        var sqlOrder = new Order // Class SQL của bạn
                        {
                            TableNumber = activeOrder.TableNumber,
                            OrderType = "Dine-in",
                            Status = "Completed",
                            CreatedAt = DateTime.Now
                        };
                        _sqlDbContext.Orders.Add(sqlOrder);
                        await _sqlDbContext.SaveChangesAsync(); // Chạy để EF Core sinh ra sqlOrder.OrderId

                        // 2.2 Tạo Chi tiết từng món (OrderDetails)
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
                                // Chuyển mảng Topping thành JSON String để nhét vào 1 cột SQL
                                SelectedOptionsJson = JsonSerializer.Serialize(item.SelectedOptions),
                                ItemNote = item.Note
                            };
                            
                            subTotal += (sqlDetail.PriceAfterDiscount + sqlDetail.TotalToppingPrice) * sqlDetail.Quantity;
                            _sqlDbContext.OrderDetails.Add(sqlDetail);
                        }

                        // 2.3 Tạo Hóa đơn (Invoices)
                        var sqlInvoice = new Invoice
                        {
                            OrderId = sqlOrder.OrderId,
                            CashierId = null, // VNPAY nên không qua thu ngân
                            SubTotal = subTotal,
                            TotalDiscount = 0, 
                            FinalAmount = subTotal,
                            PaymentMethod = "VNPAY",
                            PaymentStatus = "Success",
                            PaidAt = DateTime.Now
                        };
                        _sqlDbContext.Invoices.Add(sqlInvoice);
                        
                        // 2.4 Lưu toàn bộ vào SQL Server
                        await _sqlDbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // 3. XÓA TRỐNG GIỎ HÀNG TRONG MONGODB (Khóa bàn / Giải phóng bàn)
                        await _activeOrderCollection.DeleteOneAsync(o => o.Id == activeOrder.Id);

                        // Trả về giao diện thông báo thành công cho khách hàng
                        return View("PaymentSuccess", new { Table = tableNumber, Amount = subTotal });
                    }
                    catch (Exception ex)
                    {
                        // Nếu có bất kỳ lỗi nào khi lưu SQL (Rớt mạng, lỗi khóa ngoại...) -> Hủy toàn bộ thao tác SQL
                        await transaction.RollbackAsync();
                        return Content($"Lỗi hệ thống khi lưu hóa đơn: {ex.Message}");
                    }
                }

                return Content("Bàn này đã được thanh toán trước đó hoặc không tồn tại đơn hàng.");
            }

            // Nếu responseCode khác "00" -> Khách hàng hủy giao dịch hoặc thẻ không đủ tiền
            return Content("Giao dịch thanh toán VNPAY bị hủy hoặc thất bại.");
        }
        // Lấy dữ liệu giỏ hàng hiện tại của bàn
[HttpGet]
public async Task<IActionResult> GetCartMongo(string tableId)
{
    var order = await _activeOrderCollection
        .Find(o => o.TableNumber == tableId && o.Status == "Serving")
        .FirstOrDefaultAsync();
    
    return Json(order?.Items ?? new List<ActiveOrderItem>());
}

// Thêm 1 món vào MongoDB
[HttpPost]
public async Task<IActionResult> AddItemToMongo([FromBody] AddItemRequest req)
{
    var order = await _activeOrderCollection
        .Find(o => o.TableNumber == req.TableNumber && o.Status == "Serving")
        .FirstOrDefaultAsync();

    if (order == null)
    {
        // Nếu bàn chưa gọi gì, tạo order mới
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
        // Bàn đang ăn, nhét thêm món vào mảng Items
        var update = Builders<ActiveOrder>.Update.Push(o => o.Items, req.Item);
        await _activeOrderCollection.UpdateOneAsync(o => o.Id == order.Id, update);
    }
    return Ok(new { success = true });
}

// Xóa 1 món "Pending"
[HttpPost]
public async Task<IActionResult> RemovePendingItem(string tableId, string cartItemId)
{
    // Dùng PullFilter để lôi cái món có CartItemId tương ứng ra khỏi mảng Items
    var update = Builders<ActiveOrder>.Update.PullFilter(o => o.Items, 
        i => i.CartItemId == cartItemId && i.ItemStatus == "Pending");
        
    await _activeOrderCollection.UpdateOneAsync(o => o.TableNumber == tableId && o.Status == "Serving", update);
    return Ok(new { success = true });
}
[HttpPost]
        public async Task<IActionResult> CallStaff(string tableId)
        {
            string time = DateTime.Now.ToString("HH:mm");
            
            // "ReceiveStaffCall" là tên mã sự kiện. Màn hình nhân viên phải đăng ký tên này mới nghe được.
            await _staffHub.Clients.All.SendAsync("ReceiveStaffCall", tableId, time);
            
            return Ok(new { success = true });
        }
        
[HttpPost]
public async Task<IActionResult> CheckoutOrder(string tableId, string paymentMethod = "Cash")
{
    // Tìm cái bàn đang ăn ("Serving")
    var order = await _activeOrderCollection
        .Find(o => o.TableNumber == tableId && o.Status == "Serving")
        .FirstOrDefaultAsync();

    if (order != null)
    {
        // Lặp qua từng món ăn và đổi item_status sang "Paid"
        foreach(var item in order.Items)
        {
            if (item.ItemStatus == "Ordered")
            {
                item.ItemStatus = "Paid"; 
            }
        }

        // Cập nhật toàn bộ thông tin
        var update = Builders<ActiveOrder>.Update
            .Set(o => o.Status, "Completed")     // Khóa sổ cái bàn
            .Set(o => o.PaidAt, DateTime.Now)    // Lưu giờ tính tiền
            .Set(o => o.UpdatedAt, DateTime.Now)
            .Set(o => o.Items, order.Items);     // Ghi đè lại mảng món ăn

        await _activeOrderCollection.UpdateOneAsync(o => o.Id == order.Id, update);
        
        return Ok(new { success = true, message = "Thanh toán thành công!" });
    }
    
    return BadRequest(new { success = false, message = "Không tìm thấy đơn hàng!" });
}
// Xác nhận toàn bộ món "Pending" thành "Ordered"
// Xác nhận toàn bộ món "Pending" thành "Ordered" đẩy xuống Bếp
[HttpPost]
public async Task<IActionResult> ConfirmPendingItems(string tableId)
{
    // 1. Tìm order của bàn hiện tại
    var order = await _activeOrderCollection
        .Find(o => o.TableNumber == tableId && o.Status == "Serving")
        .FirstOrDefaultAsync();

    if (order != null)
    {
        bool hasChanges = false;
        
        // 2. Lặp qua danh sách món, đổi trạng thái và gắn thời gian báo bếp
        foreach(var item in order.Items)
        {
            if (item.ItemStatus == "Pending")
            {
                item.ItemStatus = "Ordered"; // Bếp sẽ chỉ query những món có chữ "Ordered" này
                item.OrderedAt = DateTime.Now; // Giúp Bếp biết làm món nào trước
                hasChanges = true;
            }
        }

        // 3. Nếu có món được cập nhật, lưu thẳng xuống MongoDB
        if (hasChanges)
        {
            order.UpdatedAt = DateTime.Now; // Cập nhật luôn thời gian thay đổi của giỏ hàng

            var update = Builders<ActiveOrder>.Update
                .Set(o => o.Items, order.Items) // Ghi đè lại mảng Items đã đổi trạng thái
                .Set(o => o.UpdatedAt, order.UpdatedAt);

            await _activeOrderCollection.UpdateOneAsync(o => o.Id == order.Id, update);
        }
    }
    
    return Ok(new { success = true });
}
    }
    
}