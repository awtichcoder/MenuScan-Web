using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MenuQr.Models;
using MenuQr.Areas.Admin.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using System.IO;
using System;
using MenuQr.Data;

namespace MenuQr.Areas.Staff.Controllers
{
    [Area("Staff")] 
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _sqlContext;  
        private readonly IMongoCollection<DiningTable> _tableCollection;
        private readonly IMongoCollection<ActiveOrder> _orderCollection; 

        public StaffController(ApplicationDbContext sqlContext, IMongoDatabase mongoDatabase)
        {
            _sqlContext = sqlContext;
            _tableCollection = mongoDatabase.GetCollection<DiningTable>("DiningTables");
            _orderCollection = mongoDatabase.GetCollection<ActiveOrder>("ActiveOrders");
        }

        // ==========================================
        // 1. GIAO DIỆN CHÍNH
        // ==========================================
        [HttpGet]
public async Task<IActionResult> Index()
{
    ViewBag.StaffName = "Nhân viên ca sáng";

    var tables = await _tableCollection.Find(t => t.IsActive).ToListAsync();
    var activeOrders = await _orderCollection.Find(o => o.Status != "Completed" && o.Status != "Cancelled").ToListAsync();

    foreach (var order in activeOrders)
    {
        if (order.Items != null)
        {
            // Lọc giữ lại món đã xác nhận
            order.Items = order.Items.Where(i => i.ItemStatus == "Ordered" || i.ItemStatus == "Cooking" || i.ItemStatus == "Served").ToList();
        }
    }

    // ==========================================================
    // THÊM DÒNG NÀY ĐỂ SỬA LỖI TRẠNG THÁI BÀN:
    // Lọc bỏ luôn những đơn hàng mà sau khi lọc món xong chẳng còn món nào (toàn món nháp)
    // ==========================================================
    activeOrders = activeOrders.Where(o => o.Items != null && o.Items.Count > 0).ToList();

    var staffDashboard = new StaffDashboardViewModel
    {
        Tables = tables,
        ActiveOrders = activeOrders
    };

    return View(staffDashboard);
}

        // ==========================================
        // 2. LẤY CHI TIẾT ĐƠN HÀNG (AJAX CỦA NHÂN VIÊN)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(string orderId)
        {
            if (string.IsNullOrEmpty(orderId)) return BadRequest();

            var order = await _orderCollection.Find(o => o.Id == orderId).FirstOrDefaultAsync();
            if (order == null) return NotFound();

            // ====================================================================
            // BỘ LỌC 2: Loại bỏ món Nháp khi nhân viên bấm xem chi tiết bàn
            // ====================================================================
            if (order.Items != null)
            {
                order.Items = order.Items.Where(i => i.ItemStatus == "Ordered" || i.ItemStatus == "Cooking" || i.ItemStatus == "Served").ToList();
            }

            return Json(order);
        }

        // ==========================================
        // 3. API THANH TOÁN (LƯU XUỐNG CẢ MONGODB VÀ SQL SERVER)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Checkout(string orderId, string paymentMethod)
        {
            using var transaction = await _sqlContext.Database.BeginTransactionAsync();
            try
            {
                if (string.IsNullOrEmpty(orderId)) return BadRequest(new { success = false, message = "Thiếu mã đơn!" });

                var order = await _orderCollection.Find(o => o.Id == orderId).FirstOrDefaultAsync();
                if (order == null) return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });

                // CHỈ TÍNH TIỀN NHỮNG MÓN ĐÃ XÁC NHẬN GỌI
                var orderedItems = order.Items.Where(i => i.ItemStatus == "Ordered" || i.ItemStatus == "Served" || i.ItemStatus == "Cooking").ToList();
                
                // 1. TÍNH TỔNG TIỀN ĐÚNG LOGIC
                decimal totalAmount = 0;
                if (orderedItems.Any())
                {
                    totalAmount = (decimal)orderedItems.Sum(i => i.FinalPrice * i.Quantity);
                }

                // 2. LƯU BẢNG ORDERS TRƯỚC (Bắt buộc để tạo Khóa Chính)
                var sqlOrder = new Order 
                {
                    OrderId = orderId,
                    TableNumber = order.TableNumber ?? "Tại quán",
                    OrderType = "Dine-in",
                    Status = "Completed",
                    CreatedAt = DateTime.Now
                };
                _sqlContext.Orders.Add(sqlOrder);
                await _sqlContext.SaveChangesAsync(); 

                // 3. LƯU BẢNG ORDER DETAILS
                foreach (var item in orderedItems)
                {
                    var detail = new OrderDetail {
                        OrderId = orderId, 
                        DishId = item.DishId,
                        DishName = item.DishName,
                        CategoryName = "Mặc định", // Tránh lỗi null nếu SQL yêu cầu Not Null
                        Quantity = item.Quantity,
                        BasePrice = (decimal)item.BasePrice,
                        DiscountPercent = 0,
                        PriceAfterDiscount = (decimal)item.FinalPrice,
                        TotalToppingPrice = (decimal)(item.FinalPrice - item.BasePrice),
                        SelectedOptionsJson = System.Text.Json.JsonSerializer.Serialize(item.SelectedOptions), 
                        ItemNote = item.Note
                    };
                    _sqlContext.OrderDetails.Add(detail);
                }
                await _sqlContext.SaveChangesAsync(); 

                // 4. LƯU BẢNG INVOICES (Đã có tổng tiền chuẩn)
                var invoice = new Invoice 
                { 
                    OrderId = orderId, 
                    SubTotal = totalAmount, 
                    TotalDiscount = 0,
                    FinalAmount = totalAmount,
                    PaymentMethod = paymentMethod, 
                    PaymentStatus = "Paid", 
                    PaidAt = DateTime.Now 
                };
                _sqlContext.Invoices.Add(invoice);
                await _sqlContext.SaveChangesAsync(); 

                // Commit Transaction khi đã lưu xong cả 3 bảng
                await transaction.CommitAsync();

                // 5. CẬP NHẬT TRẠNG THÁI MONGODB LÀ ĐÃ THANH TOÁN
                foreach (var item in order.Items.Where(i => i.ItemStatus == "Ordered" || i.ItemStatus == "Served" || i.ItemStatus == "Cooking"))
                {
                    item.ItemStatus = "Paid";
                }
                var update = Builders<ActiveOrder>.Update
                                .Set(o => o.Status, "Completed")
                                .Set(o => o.PaidAt, DateTime.Now)
                                .Set(o => o.Items, order.Items); // Cập nhật luôn trạng thái món thành Paid
                                
                await _orderCollection.UpdateOneAsync(o => o.Id == orderId, update);

                return Json(new { success = true, message = "Thanh toán thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(); 
                string errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi SQL: " + errorMessage });
            }
        }

        // ==========================================
        // 4. API QUÉT TÌM ĐƠN VNPAY ONLINE CHO CHỨC NĂNG REALTIME
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetUnprintedPayments()
        {
            var timeLimit = DateTime.UtcNow.AddMinutes(-2);
            var recentOrders = await _orderCollection.Find(o => o.Status == "Completed").ToListAsync();
            var result = recentOrders.Select(o => new { id = o.Id }).ToList();
            return Json(result);
        }

        // ==========================================
        // 5. API XUẤT HÓA ĐƠN PDF
        // ==========================================
        [HttpGet]
public async Task<IActionResult> ExportInvoicePdf(string orderId, string paymentMethod)
{
    var order = await _orderCollection.Find(o => o.Id == orderId).FirstOrDefaultAsync();
    if (order == null) return NotFound("Không tìm thấy đơn hàng");

    using (var stream = new MemoryStream())
    {
        PdfDocument document = new PdfDocument();
        document.Info.Title = $"Hoa Don - Ban {order.TableNumber}";

        // Tăng chiều dài trang PDF lên 250mm để tránh bị cắt chữ nếu khách gọi nhiều món
        PdfPage page = document.AddPage();
        page.Width = XUnit.FromMillimeter(80);  
        page.Height = XUnit.FromMillimeter(250); 
        XGraphics gfx = XGraphics.FromPdfPage(page);

        // Khai báo bộ font "Đẹp"
        XFont fontTitle = new XFont("Arial", 16, XFontStyleEx.Bold);
        XFont fontSubTitle = new XFont("Arial", 9, XFontStyleEx.Regular);
        XFont fontBold = new XFont("Arial", 10, XFontStyleEx.Bold);
        XFont fontRegular = new XFont("Arial", 9, XFontStyleEx.Regular);
        XFont fontSmallItalic = new XFont("Arial", 8, XFontStyleEx.Italic);

        // Bút vẽ đường nét đứt (Giống máy in nhiệt)
        XPen dashedPen = new XPen(XColors.Gray, 1);
        dashedPen.DashStyle = XDashStyle.Dash;

        int yPosition = 15; 

        // 1. HEADER (TÊN QUÁN & THÔNG TIN)
        gfx.DrawString("THE COFFEE HOUSE", fontTitle, XBrushes.Black, new XRect(0, yPosition, page.Width, 20), XStringFormats.Center);
        yPosition += 20;
        gfx.DrawString("ĐC: 123 Nguyễn Văn Cừ, Quận 5, TP.HCM", fontSubTitle, XBrushes.DarkGray, new XRect(0, yPosition, page.Width, 15), XStringFormats.Center);
        yPosition += 15;
        gfx.DrawString("Tel: 0909 123 456", fontSubTitle, XBrushes.DarkGray, new XRect(0, yPosition, page.Width, 15), XStringFormats.Center);
        yPosition += 20;

        gfx.DrawString("PHIẾU THANH TOÁN", new XFont("Arial", 12, XFontStyleEx.Bold), XBrushes.Black, new XRect(0, yPosition, page.Width, 20), XStringFormats.Center);
        yPosition += 25;
        
        // 2. THÔNG TIN BÀN & THỜI GIAN
        gfx.DrawString($"Bàn: {order.TableNumber}", fontBold, XBrushes.Black, 5, yPosition);
        gfx.DrawString($"Ngày: {DateTime.Now:dd/MM/yy HH:mm}", fontRegular, XBrushes.Black, page.Width - 110, yPosition);
        yPosition += 15;
        gfx.DrawString($"Số HĐ: #{orderId.Substring(orderId.Length - 6).ToUpper()}", fontRegular, XBrushes.Black, 5, yPosition); // Lấy 6 mã đuôi của ID làm mã hóa đơn
        yPosition += 15;

        gfx.DrawLine(dashedPen, 5, yPosition, page.Width - 5, yPosition);
        yPosition += 10;

        // 3. TIÊU ĐỀ CỘT MÓN ĂN
        gfx.DrawString("TÊN MÓN", fontBold, XBrushes.Black, 5, yPosition);
        gfx.DrawString("SL", fontBold, XBrushes.Black, page.Width - 75, yPosition);
        gfx.DrawString("T.TIỀN", fontBold, XBrushes.Black, page.Width - 45, yPosition);
        yPosition += 15;
        gfx.DrawLine(dashedPen, 5, yPosition, page.Width - 5, yPosition);
        yPosition += 10;

        // 4. DANH SÁCH MÓN ĂN
        double totalAmount = 0;
        dynamic items = order.GetType().GetProperty("Items")?.GetValue(order, null) ?? order.GetType().GetProperty("items")?.GetValue(order, null);

        if (items != null)
        {
            foreach (var item in items)
            {
                var itemType = item.GetType();
                var nameProp = itemType.GetProperty("DishName") ?? itemType.GetProperty("Name");
                string name = nameProp?.GetValue(item)?.ToString() ?? "Món ăn";

                var priceProp = itemType.GetProperty("FinalPrice") ?? itemType.GetProperty("BasePrice");
                double price = Convert.ToDouble(priceProp?.GetValue(item) ?? 0);

                var qtyProp = itemType.GetProperty("Quantity");
                int qty = Convert.ToInt32(qtyProp?.GetValue(item) ?? 1);
                double subTotal = price * qty;
                totalAmount += subTotal;

                // In Tên món, Số lượng, Thành tiền
                gfx.DrawString(name, fontBold, XBrushes.Black, 5, yPosition);
                gfx.DrawString(qty.ToString(), fontRegular, XBrushes.Black, page.Width - 70, yPosition);
                gfx.DrawString($"{subTotal:N0}", fontRegular, XBrushes.Black, page.Width - 45, yPosition);
                yPosition += 15;

                // Trích xuất Options in nhỏ ở dưới (nếu có)
               // Trích xuất Options in nhỏ ở dưới (nếu có)
var optionsProp = itemType.GetProperty("SelectedOptions") ?? itemType.GetProperty("selectedOptions");
var optionsVal = optionsProp?.GetValue(item);
if (optionsVal != null)
{
    try
    {
        var optList = new List<string>();
        var enumerable = optionsVal as System.Collections.IEnumerable;
        
        if (enumerable != null)
        {
            foreach (var opt in enumerable)
            {
                var optType = opt.GetType();
                // Lấy tên tuỳ chọn
                var namePropOpt = optType.GetProperty("Name") ?? optType.GetProperty("name") ?? optType.GetProperty("OptionName");
                // Lấy giá phụ thu
                var pricePropOpt = optType.GetProperty("ExtraPrice") ?? optType.GetProperty("extraPrice");

                string oName = namePropOpt?.GetValue(opt)?.ToString() ?? "";
                double oPrice = Convert.ToDouble(pricePropOpt?.GetValue(opt) ?? 0);

                if (!string.IsNullOrEmpty(oName))
                {
                    if (oPrice > 0)
                        optList.Add($"{oName} (+{oPrice:N0}đ)");
                    else
                        optList.Add(oName);
                }
            }
        }

        // Nối các option bằng dấu chấm tròn
        string cleanOpts = string.Join(" • ", optList);
        
        if (!string.IsNullOrWhiteSpace(cleanOpts))
        {
            gfx.DrawString($"+ {cleanOpts}", fontSmallItalic, XBrushes.DarkGray, 15, yPosition);
            yPosition += 12; // Nhích xuống 1 chút cho dòng option
        }
    }
    catch { } // Bỏ qua an toàn nếu lỗi parse
}
            }
        }

        yPosition += 5;
        gfx.DrawLine(dashedPen, 5, yPosition, page.Width - 5, yPosition);
        yPosition += 15;

        // 5. TỔNG TIỀN VÀ FOOTER
        gfx.DrawString("TỔNG CỘNG:", new XFont("Arial", 11, XFontStyleEx.Bold), XBrushes.Black, 5, yPosition);
        gfx.DrawString($"{totalAmount:N0} VNĐ", new XFont("Arial", 12, XFontStyleEx.Bold), XBrushes.Black, page.Width - 85, yPosition);
        yPosition += 25;

        gfx.DrawString($"Thanh toán: {paymentMethod}", fontSmallItalic, XBrushes.Black, new XRect(0, yPosition, page.Width, 20), XStringFormats.Center);
        yPosition += 20;

        gfx.DrawString("Xin cảm ơn và hẹn gặp lại quý khách!", fontBold, XBrushes.Black, new XRect(0, yPosition, page.Width, 20), XStringFormats.Center);
        yPosition += 15;
        gfx.DrawString("Powered by AwtichDev", fontSmallItalic, XBrushes.LightGray, new XRect(0, yPosition, page.Width, 20), XStringFormats.Center);

        document.Save(stream, false);
        return File(stream.ToArray(), "application/pdf", $"HoaDon_{order.TableNumber}_{DateTime.Now:HHmmss}.pdf");
    }

        }
    }

    public class StaffDashboardViewModel
    {
        public List<DiningTable> Tables { get; set; }
        public List<ActiveOrder> ActiveOrders { get; set; }
    }
}