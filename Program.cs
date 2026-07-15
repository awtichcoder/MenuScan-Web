using MongoDB.Driver;
using CloudinaryDotNet;
using Microsoft.EntityFrameworkCore;
using MenuQr.Data;

var builder = WebApplication.CreateBuilder(args);


// 1. SETUP MONGODB ATLAS


// Đăng ký Class MongoDbSettings đọc dữ liệu từ appsettings.json
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDBSettings"));

// Đăng ký IMongoClient dưới dạng Singleton (toàn ứng dụng dùng chung 1 kết nối duy nhất)
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var connectionString = builder.Configuration.GetValue<string>("MongoDBSettings:ConnectionString");
    return new MongoClient(connectionString);
});

// Đăng ký IMongoDatabase để các Controller (như DishController) có thể nhận được qua Constructor
builder.Services.AddScoped<IMongoDatabase>(sp => 
{
    var client = sp.GetRequiredService<IMongoClient>();
    var databaseName = builder.Configuration.GetValue<string>("MongoDBSettings:DatabaseName");
    return client.GetDatabase(databaseName);
});



// 1B. SETUP SQL SERVER (EF Core) - MenuDb


// Đăng ký AppDbContext dùng SQL Server qua connection string "DefaultConnection"
// Schema tương ứng đã có ở Models/data/MenusDb.sql (Users, Orders, OrderDetails, Invoices)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));



// 2. SETUP CLOUDINARY


// Đăng ký Class CloudinarySettings để xài Options Pattern
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));

// Đăng ký đối tượng Cloudinary để sau này inject vào Controller/Service là xài luôn
builder.Services.AddScoped(sp =>
{
    var config = builder.Configuration.GetSection("CloudinarySettings").Get<CloudinarySettings>();
    var account = new Account(config.CloudName, config.ApiKey, config.ApiSecret);
    return new Cloudinary(account);
});



// 3. SETUP VNPAY


// Đăng ký Class VnPaySettings để khi cần lấy dữ liệu TmnCode hay HashSecret thì gọi ra
builder.Services.Configure<VnPaySettings>(builder.Configuration.GetSection("VnPay"));

// UC25 - Job nền tự bật/tắt khuyến mãi theo thời gian (BR10.6)
builder.Services.AddHostedService<MenuQr.Areas.Admin.Services.PromotionStatusJob>();

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();


// 4. CONFIGURE THE HTTP REQUEST PIPELINE


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

// Map Route cho khu vực Admin 
app.MapControllerRoute(
    name: "MyAreas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Map Route mặc định cho Customer
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();