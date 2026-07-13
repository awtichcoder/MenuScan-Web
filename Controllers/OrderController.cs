// Controllers/OrderController.cs
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Text.Json;
using MenuQr.Models; // Chứa ActiveOrder (MongoDB)
 using MenuQr.Data; // Uncomment dòng này nếu ApplicationDbContext của bạn nằm trong thư mục Data
// using MenuQr.Areas.Admin.Models; // Uncomment nếu class Order, OrderDetail, Invoice nằm trong này

namespace MenuQr.Controllers
{
    public class OrderController : Controller
    {
        private readonly IMongoCollection<ActiveOrder> _activeOrderCollection;
        private readonly ApplicationDbContext _sqlDbContext; // Class DbContext của SQL Server (EF Core)

        // Tiêm (Inject) cả 2 Database vào Controller
        public OrderController(IMongoDatabase mongoDatabase, ApplicationDbContext sqlDbContext)
        {
            _activeOrderCollection = mongoDatabase.GetCollection<ActiveOrder>("ActiveOrders");
            _sqlDbContext = sqlDbContext;
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