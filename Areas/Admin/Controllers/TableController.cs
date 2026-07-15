// Areas/Admin/Controllers/TableController.cs
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MenuQr.Areas.Admin.Models;
using QRCoder;

namespace MenuQr.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class TableController : Controller
    {
        private readonly IMongoCollection<Table> _tableCollection;

        public TableController(IMongoDatabase mongoDatabase)
        {
            _tableCollection = mongoDatabase.GetCollection<Table>("Tables");
        }

        [HttpGet]
        public IActionResult Index() => View();

        // UC - Danh sách bàn (sắp theo mã)
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var tables = await _tableCollection.Find(_ => true).SortBy(t => t.Code).ToListAsync();
            return Ok(new { success = true, data = tables });
        }

        // Helper: check tồn tại theo field (không phân biệt hoa/thường)
        private async Task<bool> ExistsAsync(System.Linq.Expressions.Expression<Func<Table, object>> field, string value)
        {
            var filter = Builders<Table>.Filter.Regex(field,
                new MongoDB.Bson.BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(value.Trim())}$", "i"));
            return await _tableCollection.Find(filter).AnyAsync();
        }

        // UC21 - Thêm bàn (BR06.2 code unique, BR06.3 name bắt buộc/unique, BR06.4 capacity>0)
        [HttpPost]
        public async Task<IActionResult> CreateApi([FromBody] Table table)
        {
            if (table == null || string.IsNullOrWhiteSpace(table.Name) || string.IsNullOrWhiteSpace(table.Code))
                return BadRequest(new { success = false, code = TableMessages.MissingField, message = TableMessages.Text(TableMessages.MissingField) });

            if (table.Capacity <= 0)
                return BadRequest(new { success = false, code = TableMessages.InvalidCapacity, message = TableMessages.Text(TableMessages.InvalidCapacity) });

            if (await ExistsAsync(t => t.Code, table.Code))
                return Conflict(new { success = false, code = TableMessages.DuplicateCode, message = TableMessages.Text(TableMessages.DuplicateCode) });

            if (await ExistsAsync(t => t.Name, table.Name))
                return Conflict(new { success = false, code = TableMessages.DuplicateName, message = TableMessages.Text(TableMessages.DuplicateName) });

            try
            {
                table.Code = table.Code.Trim();
                table.Name = table.Name.Trim();
                table.Status = "available"; // BR06.5
                table.CreatedAt = DateTime.UtcNow;
                table.UpdatedAt = DateTime.UtcNow;
                await _tableCollection.InsertOneAsync(table);
                return Ok(new { success = true, code = TableMessages.CreateOk, message = TableMessages.Text(TableMessages.CreateOk), id = table.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // UC22 - Xóa bàn (BR07.3: chặn nếu bàn đang có khách 'occupied')
        [HttpDelete]
        public async Task<IActionResult> DeleteApi(string id)
        {
            var table = await _tableCollection.Find(t => t.Id == id).FirstOrDefaultAsync();
            if (table == null)
                return NotFound(new { success = false, code = TableMessages.NotFound, message = TableMessages.Text(TableMessages.NotFound) });

            if (string.Equals(table.Status, "occupied", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { success = false, code = TableMessages.Occupied, message = TableMessages.Text(TableMessages.Occupied) });

            await _tableCollection.DeleteOneAsync(t => t.Id == id);
            return Ok(new { success = true, code = TableMessages.DeleteOk, message = TableMessages.Text(TableMessages.DeleteOk) });
        }

        // UC23 - Tạo/ghi đè mã QR cho bàn (BR08.2 mỗi bàn 1 QR, BR08.3 URL kèm token)
        [HttpPost]
        public async Task<IActionResult> GenerateQrApi(string id)
        {
            var table = await _tableCollection.Find(t => t.Id == id).FirstOrDefaultAsync();
            if (table == null)
                return NotFound(new { success = false, code = TableMessages.NotFound, message = TableMessages.Text(TableMessages.NotFound) });

            try
            {
                // Token định danh bàn (tránh đoán URL bàn khác)
                var token = Guid.NewGuid().ToString("N");
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var menuUrl = $"{baseUrl}/menu?table_id={table.Id}&token={token}";

                // Sinh PNG QR thuần managed (không phụ thuộc System.Drawing)
                using var generator = new QRCodeGenerator();
                using var data = generator.CreateQrCode(menuUrl, QRCodeGenerator.ECCLevel.Q);
                var pngBytes = new PngByteQRCode(data).GetGraphic(20);
                var dataUrl = "data:image/png;base64," + Convert.ToBase64String(pngBytes);

                var update = Builders<Table>.Update
                    .Set(t => t.QrToken, token)
                    .Set(t => t.QrCodeUrl, dataUrl)
                    .Set(t => t.UpdatedAt, DateTime.UtcNow);
                await _tableCollection.UpdateOneAsync(t => t.Id == id, update);

                return Ok(new { success = true, code = TableMessages.QrOk, message = TableMessages.Text(TableMessages.QrOk), qrCodeUrl = dataUrl, menuUrl });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, code = TableMessages.QrGenFail, message = TableMessages.Text(TableMessages.QrGenFail) });
            }
        }
    }
}
