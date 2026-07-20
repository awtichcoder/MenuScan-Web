using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MenuQr.Models;
using QRCoder;
using PdfSharp.Pdf;
using PdfSharp.Fonts;
using PdfSharp.Drawing;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MenuQr.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class TableController : Controller
    {
        private readonly IMongoCollection<DiningTable> _tableCollection;

        public TableController(IMongoDatabase mongoDatabase)
        {
            _tableCollection = mongoDatabase.GetCollection<DiningTable>("DiningTables");
            
            // KÍCH HOẠT FONT CHỮ CHO PDF
            if (GlobalFontSettings.FontResolver == null)
            {
                GlobalFontSettings.FontResolver = new CustomFontResolver();
            }
        }

        // 1. Trang danh sách bàn
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var tables = await _tableCollection.Find(t => t.IsActive).ToListAsync();
            
            var qrList = new Dictionary<string, string>();
            var linkList = new Dictionary<string, string>();

            foreach (var table in tables)
            {
                string orderUrl = $"{Request.Scheme}://{Request.Host}/Home/?tableId={table.TableNumber}";
                qrList[table.TableNumber] = GenerateQrBase64(orderUrl);
                linkList[table.TableNumber] = orderUrl;
            }
            
            ViewBag.QrList = qrList;
            ViewBag.LinkList = linkList;

            return View(tables);
        }

        // 2. Thêm bàn mới
        [HttpPost]
        public async Task<IActionResult> Create(string tableNumber, string name)
        {
            if (string.IsNullOrEmpty(tableNumber) || string.IsNullOrEmpty(name))
            {
                return BadRequest("Thiếu thông tin!");
            }

            var existing = await _tableCollection.Find(t => t.TableNumber == tableNumber).FirstOrDefaultAsync();
            if (existing != null)
            {
                if (!existing.IsActive)
                {
                    var updateActivate = Builders<DiningTable>.Update.Set(t => t.IsActive, true).Set(t => t.Name, name);
                    await _tableCollection.UpdateOneAsync(t => t.Id == existing.Id, updateActivate);
                    return Json(new { success = true, message = "Kích hoạt lại bàn cũ thành công!" });
                }
                return Json(new { success = false, message = "Mã bàn này đã tồn tại!" });
            }

            var table = new DiningTable
            {
                TableNumber = tableNumber,
                Name = name,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            await _tableCollection.InsertOneAsync(table);
            return Json(new { success = true });
        }

        // 3. Xóa mềm bàn
        [HttpPost]
        public async Task<IActionResult> DeleteSoft(string tableNumber)
        {
            var update = Builders<DiningTable>.Update.Set(t => t.IsActive, false);
            var result = await _tableCollection.UpdateOneAsync(t => t.TableNumber == tableNumber, update);

            if (result.ModifiedCount > 0)
            {
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Không tìm thấy bàn yêu cầu." });
        }

        // 4. Xuất file PDF cho TẤT CẢ các bàn
        [HttpGet]
        public async Task<IActionResult> ExportPdf()
        {
            var tables = await _tableCollection.Find(t => t.IsActive).ToListAsync();

            using (var stream = new MemoryStream())
            {
                PdfDocument document = new PdfDocument();
                document.Info.Title = "Danh sach ma QR cac ban";

                foreach (var table in tables)
                {
                    PdfPage page = document.AddPage();
                    page.Width = XUnit.FromMillimeter(100);  
                    page.Height = XUnit.FromMillimeter(100);
                    XGraphics gfx = XGraphics.FromPdfPage(page);

                    XPen borderPen = new XPen(XColor.FromArgb(243, 193, 120), 3); 
                    gfx.DrawRectangle(borderPen, 10, 10, page.Width - 20, page.Height - 20);

                    XFont titleFont = new XFont("Arial", 16, XFontStyleEx.Bold);
                    XStringFormat format = new XStringFormat { Alignment = XStringAlignment.Center };
                    gfx.DrawString(table.Name.ToUpper(), titleFont, XBrushes.Black, new XPoint(page.Width / 2, 30), format);

                    // ĐÃ SỬA: Dùng hàm DrawQrCodeVector thay vì nén thành ảnh
                    string orderUrl = $"{Request.Scheme}://{Request.Host}/Home/?tableId={table.TableNumber}";
                    DrawQrCodeVector(gfx, orderUrl, (page.Width - 160) / 2, 45, 160);

                    XFont footerFont = new XFont("Arial", 9, XFontStyleEx.Italic);
                    gfx.DrawString("Quet QR de xem Menu va goi mon", footerFont, XBrushes.Gray, new XPoint(page.Width / 2, page.Height - 20), format);
                }

                document.Save(stream, false);
                byte[] fileBytes = stream.ToArray();
                return File(fileBytes, "application/pdf", "MenuQR_DanhSachBan.pdf");
            }
        }

        // 5. Xuất file PDF lẻ cho CHỈ 1 BÀN
        [HttpGet]
        public async Task<IActionResult> ExportSinglePdf(string tableNumber)
        {
            var table = await _tableCollection.Find(t => t.TableNumber == tableNumber && t.IsActive).FirstOrDefaultAsync();
            if (table == null)
            {
                return NotFound("Không tìm thấy bàn hoạt động!");
            }

            using (var stream = new MemoryStream())
            {
                PdfDocument document = new PdfDocument();
                document.Info.Title = $"Ma QR - {table.Name}";

                PdfPage page = document.AddPage();
                page.Width = XUnit.FromMillimeter(100);  
                page.Height = XUnit.FromMillimeter(100);
                XGraphics gfx = XGraphics.FromPdfPage(page);

                XPen borderPen = new XPen(XColor.FromArgb(243, 193, 120), 3);
                gfx.DrawRectangle(borderPen, 10, 10, page.Width - 20, page.Height - 20);

                XFont titleFont = new XFont("Arial", 16, XFontStyleEx.Bold);
                XStringFormat format = new XStringFormat { Alignment = XStringAlignment.Center };
                gfx.DrawString(table.Name.ToUpper(), titleFont, XBrushes.Black, new XPoint(page.Width / 2, 30), format);

                // ĐÃ SỬA: Dùng hàm DrawQrCodeVector thay vì nén thành ảnh
                string orderUrl = $"{Request.Scheme}://{Request.Host}/Home/?tableId={table.TableNumber}";
                DrawQrCodeVector(gfx, orderUrl, (page.Width - 160) / 2, 45, 160);

                XFont footerFont = new XFont("Arial", 9, XFontStyleEx.Italic);
                gfx.DrawString("Quet QR de xem Menu va goi mon", footerFont, XBrushes.Gray, new XPoint(page.Width / 2, page.Height - 20), format);

                document.Save(stream, false);
                byte[] fileBytes = stream.ToArray();
                return File(fileBytes, "application/pdf", $"MenuQR_{table.TableNumber}.pdf");
            }
        }

        // ==========================================
        // HÀM VẼ QR VECTOR TRỰC TIẾP LÊN PDF
        // ==========================================
        private void DrawQrCodeVector(XGraphics gfx, string text, double x, double y, double size)
        {
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
                var matrix = qrCodeData.ModuleMatrix;
                
                int moduleCount = matrix.Count;
                double moduleSize = size / moduleCount; 

                XBrush blackBrush = XBrushes.Black;

                for (int row = 0; row < moduleCount; row++)
                {
                    for (int col = 0; col < moduleCount; col++)
                    {
                        if (matrix[row][col]) 
                        {
                            gfx.DrawRectangle(blackBrush, x + (col * moduleSize), y + (row * moduleSize), moduleSize, moduleSize);
                        }
                    }
                }
            }
        }

        // ==========================================
        // CÁC HÀM TRỢ GIÚP TẠO MÃ QR (DÙNG CHO WEB)
        // ==========================================
        private byte[] GenerateQrBytes(string text)
        {
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new PngByteQRCode(qrCodeData))
                {
                    return qrCode.GetGraphic(15); 
                }
            }
        }

        private string GenerateQrBase64(string text)
        {
            byte[] bytes = GenerateQrBytes(text);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }

        // ==========================================
        // CẤU HÌNH FONT CHỮ
        // ==========================================
        public class CustomFontResolver : IFontResolver
        {
            public string DefaultFontName => "Arial";

            public byte[] GetFont(string faceName)
            {
                if (faceName == "ArialBold") 
                    return System.IO.File.ReadAllBytes(@"C:\Windows\Fonts\arialbd.ttf");
                
                if (faceName == "ArialItalic") 
                    return System.IO.File.ReadAllBytes(@"C:\Windows\Fonts\ariali.ttf");
                
                return System.IO.File.ReadAllBytes(@"C:\Windows\Fonts\arial.ttf");
            }

            public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
            {
                string name = "Arial";
                if (isBold) name = "ArialBold";
                else if (isItalic) name = "ArialItalic";
                
                return new FontResolverInfo(name);
            }
        }
    }
}