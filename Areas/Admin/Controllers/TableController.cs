using MenuQr.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using QRCoder;

namespace MenuQr.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class TableController : Controller
    {
        private readonly IMongoCollection<DiningTable> _tableCollection;
        private readonly IMongoCollection<ActiveOrder> _activeOrderCollection;

        public TableController(IMongoDatabase mongoDatabase)
        {
            _tableCollection = mongoDatabase.GetCollection<DiningTable>("DiningTables");
            _activeOrderCollection = mongoDatabase.GetCollection<ActiveOrder>("ActiveOrders");

            if (GlobalFontSettings.FontResolver == null)
                GlobalFontSettings.FontResolver = new CustomFontResolver();
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var tables = await _tableCollection.Find(t => t.IsActive).SortBy(t => t.TableNumber).ToListAsync();
            var qrList = new Dictionary<string, string>();
            var linkList = new Dictionary<string, string>();

            foreach (var table in tables)
            {
                var orderUrl = BuildOrderUrl(table.TableNumber);
                qrList[table.TableNumber] = GenerateQrBase64(orderUrl);
                linkList[table.TableNumber] = orderUrl;
            }

            ViewBag.QrList = qrList;
            ViewBag.LinkList = linkList;
            return View(tables);
        }

        [HttpPost]
        public async Task<IActionResult> Create(string tableNumber, string name)
        {
            if (string.IsNullOrWhiteSpace(tableNumber) || string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, code = "MS01", message = "Vui long nhap ma ban va ten ban." });

            tableNumber = tableNumber.Trim();
            name = name.Trim();

            var existingName = await _tableCollection.Find(t => t.Name == name && t.IsActive).FirstOrDefaultAsync();
            if (existingName != null)
                return BadRequest(new { success = false, code = "MS03", message = "Ten ban nay da ton tai." });

            var existing = await _tableCollection.Find(t => t.TableNumber == tableNumber).FirstOrDefaultAsync();
            if (existing != null)
            {
                if (!existing.IsActive)
                {
                    var update = Builders<DiningTable>.Update.Set(t => t.IsActive, true).Set(t => t.Name, name);
                    await _tableCollection.UpdateOneAsync(t => t.Id == existing.Id, update);
                    return Json(new { success = true, code = "MS06", message = "Kich hoat lai ban cu thanh cong." });
                }

                return BadRequest(new { success = false, code = "MS02", message = "Ma ban nay da ton tai." });
            }

            var table = new DiningTable
            {
                TableNumber = tableNumber,
                Name = name,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            await _tableCollection.InsertOneAsync(table);
            return Json(new { success = true, code = "MS06", message = "Them ban thanh cong." });
        }

        [HttpPost("/api/admin/tables")]
        public Task<IActionResult> CreateTable(string tableNumber, string name)
        {
            return Create(tableNumber, name);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSoft(string tableNumber)
        {
            if (string.IsNullOrWhiteSpace(tableNumber))
                return BadRequest(new { success = false, code = "MS02", message = "Khong tim thay ban yeu cau." });

            var hasActiveOrder = await _activeOrderCollection
                .Find(o => o.TableNumber == tableNumber && o.Status == "Serving")
                .AnyAsync();

            if (hasActiveOrder)
                return BadRequest(new { success = false, code = "MS01", message = "Khong the xoa ban dang co don hang chua hoan tat." });

            var result = await _tableCollection.UpdateOneAsync(
                t => t.TableNumber == tableNumber && t.IsActive,
                Builders<DiningTable>.Update.Set(t => t.IsActive, false));

            if (result.ModifiedCount > 0)
                return Json(new { success = true, code = "MS05", message = "Xoa ban thanh cong." });

            return NotFound(new { success = false, code = "MS02", message = "Khong tim thay ban yeu cau." });
        }

        [HttpDelete("/api/admin/tables/{tableNumber}")]
        public Task<IActionResult> DeleteTable(string tableNumber)
        {
            return DeleteSoft(tableNumber);
        }

        [HttpPost("/api/admin/tables/{tableNumber}/qr-code")]
        public async Task<IActionResult> GenerateQrCode(string tableNumber)
        {
            var table = await _tableCollection.Find(t => t.TableNumber == tableNumber && t.IsActive).FirstOrDefaultAsync();
            if (table == null)
                return NotFound(new { success = false, code = "MS01", message = "Khong tim thay ban." });

            var url = BuildOrderUrl(table.TableNumber);
            return Ok(new { success = true, code = "MS06", url, qrCode = GenerateQrBase64(url) });
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf()
        {
            var tables = await _tableCollection.Find(t => t.IsActive).SortBy(t => t.TableNumber).ToListAsync();

            using var stream = new MemoryStream();
            var document = new PdfDocument();
            document.Info.Title = "Danh sach ma QR cac ban";

            foreach (var table in tables)
                AddQrPdfPage(document, table);

            document.Save(stream, false);
            return File(stream.ToArray(), "application/pdf", "MenuQR_DanhSachBan.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> ExportSinglePdf(string tableNumber)
        {
            var table = await _tableCollection.Find(t => t.TableNumber == tableNumber && t.IsActive).FirstOrDefaultAsync();
            if (table == null) return NotFound("Khong tim thay ban hoat dong.");

            using var stream = new MemoryStream();
            var document = new PdfDocument();
            document.Info.Title = $"Ma QR - {table.Name}";
            AddQrPdfPage(document, table);
            document.Save(stream, false);
            return File(stream.ToArray(), "application/pdf", $"MenuQR_{table.TableNumber}.pdf");
        }

        private void AddQrPdfPage(PdfDocument document, DiningTable table)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromMillimeter(100);
            page.Height = XUnit.FromMillimeter(100);

            using var gfx = XGraphics.FromPdfPage(page);
            var format = new XStringFormat { Alignment = XStringAlignment.Center };

            gfx.DrawRectangle(new XPen(XColor.FromArgb(243, 193, 120), 3), 10, 10, page.Width - 20, page.Height - 20);
            gfx.DrawString(table.Name.ToUpperInvariant(), new XFont("Arial", 16, XFontStyleEx.Bold), XBrushes.Black, new XPoint(page.Width / 2, 30), format);
            DrawQrCodeVector(gfx, BuildOrderUrl(table.TableNumber), (page.Width - 160) / 2, 45, 160);
            gfx.DrawString("Quet QR de xem Menu va goi mon", new XFont("Arial", 9, XFontStyleEx.Italic), XBrushes.Gray, new XPoint(page.Width / 2, page.Height - 20), format);
        }

        private string BuildOrderUrl(string tableNumber)
        {
            return $"{Request.Scheme}://{Request.Host}/Home/?tableId={tableNumber}";
        }

        private void DrawQrCodeVector(XGraphics gfx, string text, double x, double y, double size)
        {
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            var matrix = qrCodeData.ModuleMatrix;
            var moduleSize = size / matrix.Count;

            for (var row = 0; row < matrix.Count; row++)
            {
                for (var col = 0; col < matrix.Count; col++)
                {
                    if (matrix[row][col])
                        gfx.DrawRectangle(XBrushes.Black, x + col * moduleSize, y + row * moduleSize, moduleSize, moduleSize);
                }
            }
        }

        private byte[] GenerateQrBytes(string text)
        {
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(15);
        }

        private string GenerateQrBase64(string text)
        {
            return "data:image/png;base64," + Convert.ToBase64String(GenerateQrBytes(text));
        }

        public class CustomFontResolver : IFontResolver
        {
            public string DefaultFontName => "Arial";

            public byte[] GetFont(string faceName)
            {
                if (faceName == "ArialBold") return System.IO.File.ReadAllBytes(@"C:\Windows\Fonts\arialbd.ttf");
                if (faceName == "ArialItalic") return System.IO.File.ReadAllBytes(@"C:\Windows\Fonts\ariali.ttf");
                return System.IO.File.ReadAllBytes(@"C:\Windows\Fonts\arial.ttf");
            }

            public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
            {
                if (isBold) return new FontResolverInfo("ArialBold");
                if (isItalic) return new FontResolverInfo("ArialItalic");
                return new FontResolverInfo("Arial");
            }
        }
    }
}
