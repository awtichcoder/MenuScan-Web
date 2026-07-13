using MongoDB.Driver;
using CloudinaryDotNet;

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



//  SETUP CLOUDINARY

// Đăng ký Class CloudinarySettings để xài Options Pattern
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));

// Đăng ký đối tượng Cloudinary để sau này inject vào Controller/Service là xài luôn
builder.Services.AddScoped(sp =>
{
    var config = builder.Configuration.GetSection("CloudinarySettings").Get<CloudinarySettings>();
    var account = new Account(config.CloudName, config.ApiKey, config.ApiSecret);
    return new Cloudinary(account);
});



// SETUP VNPAY
// Đăng ký Class VnPaySettings để khi cần lấy dữ liệu TmnCode hay HashSecret thì gọi ra
builder.Services.Configure<VnPaySettings>(builder.Configuration.GetSection("VnPay"));
// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
// admin 
app.MapControllerRoute(
    name: "MyAreas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
// cus
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
