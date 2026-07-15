// Areas/Admin/Models/AdminMessages.cs
// Tập trung message theo mã (MSxx) cho các use case Admin.
// Mục đích: tái sử dụng, dễ đổi ngôn ngữ sau này (chỉ sửa 1 chỗ).
namespace MenuQr.Areas.Admin.Models
{
    // UC16 - Quản lý danh mục món ăn
    public static class CategoryMessages
    {
        public const string DuplicateName = "MS01"; // Tên danh mục đã tồn tại
        public const string EmptyName = "MS02";     // Vui lòng nhập tên danh mục
        public const string HasDishes = "MS03";     // Không thể xóa vì đang chứa món ăn
        public const string CreateOk = "MS04";      // Thêm danh mục thành công

        public static string Text(string code) => code switch
        {
            DuplicateName => "Tên danh mục đã tồn tại.",
            EmptyName => "Vui lòng nhập tên danh mục.",
            HasDishes => "Không thể xóa danh mục vì đang chứa món ăn.",
            CreateOk => "Thêm danh mục thành công.",
            _ => "Có lỗi xảy ra."
        };
    }

    // UC17-UC20 - Quản lý món ăn
    public static class DishMessages
    {
        public const string MissingField = "MS01";  // Thiếu thông tin bắt buộc
        public const string InvalidPrice = "MS02";   // Giá không hợp lệ
        public const string DuplicateName = "MS03";  // Tên món trùng trong danh mục
        public const string InvalidImage = "MS04";   // Ảnh sai định dạng/dung lượng
        public const string NoCategory = "MS05";     // Chưa có danh mục nào
        public const string CreateOk = "MS06";       // Thêm/cập nhật món thành công
        public const string NotFound = "MS07";       // Không tìm thấy món

        public static string Text(string code) => code switch
        {
            MissingField => "Vui lòng nhập đầy đủ thông tin bắt buộc.",
            InvalidPrice => "Giá món không hợp lệ (phải là số ≥ 0).",
            DuplicateName => "Tên món đã tồn tại trong danh mục này.",
            InvalidImage => "Ảnh không hợp lệ (chỉ nhận jpg/png/webp, tối đa 10MB).",
            NoCategory => "Chưa có danh mục nào, vui lòng tạo danh mục trước.",
            CreateOk => "Lưu món thành công.",
            NotFound => "Không tìm thấy món ăn.",
            _ => "Có lỗi xảy ra."
        };
    }

    // UC21-UC23 - Quản lý bàn ăn & mã QR
    public static class TableMessages
    {
        // UC21 - Thêm bàn
        public const string MissingField = "MS01";   // Thiếu tên/mã bàn
        public const string DuplicateCode = "MS02";   // Mã bàn đã tồn tại
        public const string DuplicateName = "MS03";   // Tên bàn đã tồn tại
        public const string InvalidCapacity = "MS04"; // Sức chứa không hợp lệ
        public const string CreateOk = "MS06";        // Thêm bàn thành công
        // UC22 - Xóa bàn
        public const string Occupied = "MS21";        // Bàn đang có khách/đơn chưa xong
        public const string NotFound = "MS22";        // Không tìm thấy bàn
        public const string DeleteOk = "MS25";        // Xóa thành công
        // UC23 - Tạo QR
        public const string QrGenFail = "MS32";       // Lỗi sinh mã QR
        public const string QrOk = "MS36";            // Tạo QR thành công

        public static string Text(string code) => code switch
        {
            MissingField => "Vui lòng nhập tên và mã bàn.",
            DuplicateCode => "Mã bàn đã tồn tại.",
            DuplicateName => "Tên bàn đã tồn tại.",
            InvalidCapacity => "Sức chứa phải lớn hơn 0.",
            CreateOk => "Thêm bàn thành công.",
            Occupied => "Không thể xóa: bàn đang có khách hoặc đơn chưa hoàn tất.",
            NotFound => "Không tìm thấy bàn.",
            DeleteOk => "Xóa bàn thành công.",
            QrGenFail => "Lỗi khi sinh mã QR.",
            QrOk => "Tạo mã QR thành công.",
            _ => "Có lỗi xảy ra."
        };
    }

    // UC24 - Thống kê doanh thu
    public static class StatisticsMessages
    {
        public const string InvalidRange = "MS01"; // Khoảng ngày không hợp lệ
        public const string NoData = "MS02";        // Không có dữ liệu

        public static string Text(string code) => code switch
        {
            InvalidRange => "Khoảng thời gian không hợp lệ (từ ngày phải ≤ đến ngày).",
            NoData => "Không có dữ liệu trong khoảng thời gian đã chọn.",
            _ => "Có lỗi xảy ra."
        };
    }

    // UC25 - Sự kiện giảm giá theo %
    public static class PromotionMessages
    {
        public const string MissingField = "MS01";   // Thiếu tên / chưa chọn món
        public const string InvalidPercent = "MS02";  // % ngoài 1..100
        public const string InvalidDate = "MS03";      // start > end
        public const string Overlap = "MS04";          // món đã thuộc KM khác trùng thời gian
        public const string SaveError = "MS05";        // lỗi lưu DB
        public const string CreateOk = "MS06";         // thành công
        public const string InactiveDish = "MS07";     // chọn món không còn kinh doanh (BR10.5)
        public const string NotFound = "MS08";

        public static string Text(string code) => code switch
        {
            MissingField => "Vui lòng nhập tên chương trình và chọn ít nhất một món.",
            InvalidPercent => "Phần trăm giảm phải trong khoảng 1–100.",
            InvalidDate => "Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.",
            Overlap => "Có món đã nằm trong một khuyến mãi khác trùng thời gian.",
            SaveError => "Lỗi khi lưu khuyến mãi.",
            CreateOk => "Tạo khuyến mãi thành công.",
            InactiveDish => "Chỉ được chọn món đang kinh doanh.",
            NotFound => "Không tìm thấy khuyến mãi.",
            _ => "Có lỗi xảy ra."
        };
    }
}
