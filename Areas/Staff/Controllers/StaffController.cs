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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MenuQr.Hubs;

namespace MenuQr.Areas.Staff.Controllers
{
    public class StaffAddItemRequest
    {
        public string OrderId { get; set; } = null!;
        public string DishId { get; set; } = null!;
        public string DishName { get; set; } = null!;
        public decimal Price { get; set; }
        public int Quantity { get; set; } = 1;
        // Bổ sung List Option để nhận dữ liệu Topping từ Giao diện
        public List<SelectedOption> SelectedOptions { get; set; } = new List<SelectedOption>();
    }

    [Area("Staff")] 
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _sqlContext;  
        private readonly IMongoCollection<DiningTable> _tableCollection;
        private readonly IMongoCollection<ActiveOrder> _orderCollection;
        private readonly IHubContext<StaffHub> _staffHub; 

        public StaffController(ApplicationDbContext sqlContext, IMongoDatabase mongoDatabase, IHubContext<StaffHub> staffHub)
        {
            _sqlContext = sqlContext;
            _tableCollection = mongoDatabase.GetCollection<DiningTable>("DiningTables");
            _orderCollection = mongoDatabase.GetCollection<ActiveOrder>("ActiveOrders");
            _staffHub = staffHub;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllDishes()
        {
            try
            {
                var dishesCollection = _tableCollection.Database.GetCollection<Dish>("Dishes");
                var dishes = await dishesCollection.Find(d => d.IsAvailable).ToListAsync();

                var result = dishes.Select(d => new {
                    id = d.Id,
                    name = d.Name,
                    price = d.BasePrice,
                    specifications = d.Specifications // Gửi kèm thông tin Option cho Javascript
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddItemToOrder([FromBody] StaffAddItemRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.OrderId))
                    return Json(new { success = false, message = "Thiếu thông tin mã đơn hàng!" });

                var filter = Builders<ActiveOrder>.Filter.Eq(o => o.Id, request.OrderId);
                
                // Tính tổng tiền Topping
                decimal extraPriceTotal = request.SelectedOptions != null ? request.SelectedOptions.Sum(o => o.ExtraPrice) : 0;
                decimal finalPrice = request.Price + extraPriceTotal;
                
                var newItem = new ActiveOrderItem 
                { 
                    DishId = request.DishId, 
                    DishName = request.DishName,
                    Quantity = request.Quantity,
                    BasePrice = request.Price,           
                    FinalPrice = finalPrice, // Đã cộng tiền topping        
                    SelectedOptions = request.SelectedOptions ?? new List<SelectedOption>(),
                    ItemStatus = "Ordered",              
                    OrderedAt = DateTime.Now
                };
                
                var update = Builders<ActiveOrder>.Update
                    .Push(o => o.Items, newItem)
                    .Set(o => o.UpdatedAt, DateTime.Now);
                    
                var result = await _orderCollection.UpdateOneAsync(filter, update);
                
                if (result.ModifiedCount > 0)
                {
                    return Json(new { success = true, message = "Thêm món thành công!" });
                }
                
                return Json(new { success = false, message = "Không tìm thấy đơn hàng trên hệ thống." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi Server: " + ex.Message });
            }
        }

        [HttpPost]
[HttpPost]
public async Task<IActionResult> CreateTakeaway()
{
    try
    {
        // Tạo mã TKW dựa trên timestamp để đảm bảo không bao giờ trùng nhau
        string tableCode = "TKW-" + DateTime.UtcNow.ToString("yyMMddHHmmss"); 

        var newOrder = new ActiveOrder 
        {
            TableNumber = tableCode,
            Status = "Serving", 
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = new List<ActiveOrderItem>()
        };
        
        await _orderCollection.InsertOneAsync(newOrder); 

        return Json(new { success = true, tableNumber = tableCode, orderId = newOrder.Id });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = "Lỗi khi tạo đơn mang về: " + ex.Message });
    }
}

        [HttpPost]
        public async Task<IActionResult> ClearServiceAlert(string tableNumber)
        {
            try
            {
                var filter = Builders<DiningTable>.Filter.Eq("table_number", tableNumber);
                var update = Builders<DiningTable>.Update.Set("needs_service", false);
                var result = await _tableCollection.UpdateOneAsync(filter, update);

                if (result.ModifiedCount > 0 || result.MatchedCount > 0)
                {
                    return Json(new { success = true });
                }
                
                return Json(new { success = false, message = "Không tìm thấy bàn này" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

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
                    order.Items = order.Items.Where(i => i.ItemStatus == "Ordered" || i.ItemStatus == "Cooking" || i.ItemStatus == "Served").ToList();
                }
            }

            activeOrders = activeOrders.Where(o => o.Items != null && o.Items.Count > 0).ToList();

            var staffDashboard = new StaffDashboardViewModel
            {
                Tables = tables,
                ActiveOrders = activeOrders
            };

            return View(staffDashboard);
        }

        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(string orderId)
        {
            if (string.IsNullOrEmpty(orderId)) return BadRequest();

            var order = await _orderCollection.Find(o => o.Id == orderId).FirstOrDefaultAsync();
            if (order == null) return NotFound();

            if (order.Items != null)
            {
                order.Items = order.Items.Where(i => i.ItemStatus == "Ordered" || i.ItemStatus == "Cooking" || i.ItemStatus == "Served").ToList();
            }

            return Json(order);
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(string orderId, string paymentMethod)
        {
            using var transaction = await _sqlContext.Database.BeginTransactionAsync();
            try
            {
                if (string.IsNullOrEmpty(orderId)) return BadRequest(new { success = false, message = "Thiếu mã đơn!" });

                var order = await _orderCollection.Find(o => o.Id == orderId).FirstOrDefaultAsync();
                if (order == null) return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });

                var orderedItems = order.Items.Where(i => i.ItemStatus == "Ordered" || i.ItemStatus == "Served" || i.ItemStatus == "Cooking").ToList();
                
                decimal totalAmount = 0;
                if (orderedItems.Any())
                {
                    totalAmount = (decimal)orderedItems.Sum(i => i.FinalPrice * i.Quantity);
                }

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

                foreach (var item in orderedItems)
                {
                    var detail = new OrderDetail {
                        OrderId = orderId, 
                        DishId = item.DishId,
                        DishName = item.DishName,
                        CategoryName = "Mặc định",
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

                await transaction.CommitAsync();

                foreach (var item in order.Items.Where(i => i.ItemStatus == "Ordered" || i.ItemStatus == "Served" || i.ItemStatus == "Cooking"))
                {
                    item.ItemStatus = "Paid";
                }
                var update = Builders<ActiveOrder>.Update
                                .Set(o => o.Status, "Completed")
                                .Set(o => o.PaidAt, DateTime.Now)
                                .Set(o => o.Items, order.Items); 
                                
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

        [HttpGet]
        public async Task<IActionResult> GetUnprintedPayments()
        {
            var timeLimit = DateTime.UtcNow.AddMinutes(-2);
            var recentOrders = await _orderCollection.Find(o => o.Status == "Completed").ToListAsync();
            var result = recentOrders.Select(o => new { id = o.Id }).ToList();
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> ExportInvoicePdf(string orderId, string paymentMethod)
        {
            var order = await _orderCollection.Find(o => o.Id == orderId).FirstOrDefaultAsync();
            if (order == null) return NotFound("Không tìm thấy đơn hàng");

            using (var stream = new MemoryStream())
            {
                PdfDocument document = new PdfDocument();
                document.Info.Title = $"Hoa Don - Ban {order.TableNumber}";

                PdfPage page = document.AddPage();
                page.Width = XUnit.FromMillimeter(80);  
                page.Height = XUnit.FromMillimeter(250); 
                XGraphics gfx = XGraphics.FromPdfPage(page);

                XFont fontTitle = new XFont("Arial", 16, XFontStyleEx.Bold);
                XFont fontSubTitle = new XFont("Arial", 9, XFontStyleEx.Regular);
                XFont fontBold = new XFont("Arial", 10, XFontStyleEx.Bold);
                XFont fontRegular = new XFont("Arial", 9, XFontStyleEx.Regular);
                XFont fontSmallItalic = new XFont("Arial", 8, XFontStyleEx.Italic);

                XPen dashedPen = new XPen(XColors.Gray, 1);
                dashedPen.DashStyle = XDashStyle.Dash;

                int yPosition = 15; 

                gfx.DrawString("THE COFFEE HOUSE", fontTitle, XBrushes.Black, new XRect(0, yPosition, page.Width, 20), XStringFormats.Center);
                yPosition += 20;
                gfx.DrawString("ĐC: 123 Nguyễn Văn Cừ, Quận 5, TP.HCM", fontSubTitle, XBrushes.DarkGray, new XRect(0, yPosition, page.Width, 15), XStringFormats.Center);
                yPosition += 15;
                gfx.DrawString("Tel: 0909 123 456", fontSubTitle, XBrushes.DarkGray, new XRect(0, yPosition, page.Width, 15), XStringFormats.Center);
                yPosition += 20;

                gfx.DrawString("PHIẾU THANH TOÁN", new XFont("Arial", 12, XFontStyleEx.Bold), XBrushes.Black, new XRect(0, yPosition, page.Width, 20), XStringFormats.Center);
                yPosition += 25;
                
                gfx.DrawString($"Bàn: {order.TableNumber}", fontBold, XBrushes.Black, 5, yPosition);
                gfx.DrawString($"Ngày: {DateTime.Now:dd/MM/yy HH:mm}", fontRegular, XBrushes.Black, page.Width - 110, yPosition);
                yPosition += 15;
                gfx.DrawString($"Số HĐ: #{orderId.Substring(orderId.Length - 6).ToUpper()}", fontRegular, XBrushes.Black, 5, yPosition);
                yPosition += 15;

                gfx.DrawLine(dashedPen, 5, yPosition, page.Width - 5, yPosition);
                yPosition += 10;

                gfx.DrawString("TÊN MÓN", fontBold, XBrushes.Black, 5, yPosition);
                gfx.DrawString("SL", fontBold, XBrushes.Black, page.Width - 75, yPosition);
                gfx.DrawString("T.TIỀN", fontBold, XBrushes.Black, page.Width - 45, yPosition);
                yPosition += 15;
                gfx.DrawLine(dashedPen, 5, yPosition, page.Width - 5, yPosition);
                yPosition += 10;

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

                        gfx.DrawString(name, fontBold, XBrushes.Black, 5, yPosition);
                        gfx.DrawString(qty.ToString(), fontRegular, XBrushes.Black, page.Width - 70, yPosition);
                        gfx.DrawString($"{subTotal:N0}", fontRegular, XBrushes.Black, page.Width - 45, yPosition);
                        yPosition += 15;

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
                                        var namePropOpt = optType.GetProperty("Name") ?? optType.GetProperty("name") ?? optType.GetProperty("OptionName");
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

                                string cleanOpts = string.Join(" • ", optList);
                                
                                if (!string.IsNullOrWhiteSpace(cleanOpts))
                                {
                                    gfx.DrawString($"+ {cleanOpts}", fontSmallItalic, XBrushes.DarkGray, 15, yPosition);
                                    yPosition += 12; 
                                }
                            }
                            catch { } 
                        }
                    }
                }

                yPosition += 5;
                gfx.DrawLine(dashedPen, 5, yPosition, page.Width - 5, yPosition);
                yPosition += 15;

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
