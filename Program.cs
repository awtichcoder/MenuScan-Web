using MongoDB.Driver;
using CloudinaryDotNet;
using MenuQr.Data;
using Microsoft.EntityFrameworkCore; // Nhớ thêm using này để dùng được UseSqlServer

var builder = WebApplication.CreateBuilder(args);

// 1. SETUP MONGODB ATLAS
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDBSettings"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var connectionString = builder.Configuration.GetValue<string>("MongoDBSettings:ConnectionString");
    return new MongoClient(connectionString);
});

builder.Services.AddScoped<IMongoDatabase>(sp => 
{
    var client = sp.GetRequiredService<IMongoClient>();
    var databaseName = builder.Configuration.GetValue<string>("MongoDBSettings:DatabaseName");
    return client.GetDatabase(databaseName);
});


// 2. SETUP CLOUDINARY
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));

builder.Services.AddScoped(sp =>
{
    var config = builder.Configuration.GetSection("CloudinarySettings").Get<CloudinarySettings>();
    var account = new Account(config.CloudName, config.ApiKey, config.ApiSecret);
    return new Cloudinary(account);
});


// 3. SETUP VNPAY
builder.Services.Configure<VnPaySettings>(builder.Configuration.GetSection("VnPay"));

builder.Services.AddControllersWithViews();


// =======================================================
// SETUP SQL SERVER (PHẢI NẰM TRƯỚC BUILDER.BUILD)
// =======================================================
var sqlConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(sqlConnectionString));


// =======================================================
// LỆNH BUILD BẮT BUỘC NẰM Ở ĐÂY
// =======================================================
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