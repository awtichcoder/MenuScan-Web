using Microsoft.AspNetCore.SignalR;

namespace MenuQr.Hubs
{
    // Đây là nơi các thiết bị của nhân viên sẽ kết nối vào để nghe ngóng tín hiệu
    public class StaffHub : Hub
    {
        // Tạm thời để trống vì hệ thống của bạn chỉ cần luồng đi một chiều: Khách -> Server -> Nhân viên
    }
}