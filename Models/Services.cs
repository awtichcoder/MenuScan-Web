//   cấu hình MongoDB
public class MongoDbSettings
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
}

//  cấu hình Cloudinary
public class CloudinarySettings
{
    public string CloudName { get; set; } = null!;
    public string ApiKey { get; set; } = null!;
    public string ApiSecret { get; set; } = null!;
}

//  cấu hình VnPay
public class VnPaySettings
{
    public string TmnCode { get; set; } = null!;
    public string HashSecret { get; set; } = null!;
    public string BaseUrl { get; set; } = null!;
}