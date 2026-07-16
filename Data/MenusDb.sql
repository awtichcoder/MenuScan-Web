-- =======================================================
-- 1. XÓA CÁC BẢNG CŨ ĐỂ LÀM SẠCH DATABASE
-- LƯU Ý: Phải xóa bảng con (chứa Khóa Ngoại) trước, bảng cha sau
-- =======================================================
DROP TABLE IF EXISTS Invoices;
DROP TABLE IF EXISTS OrderDetails;
DROP TABLE IF EXISTS Orders;
DROP TABLE IF EXISTS Users;

-- =======================================================
-- 2. TẠO LẠI CÁC BẢNG MỚI CHUẨN CHỈNH
-- =======================================================

CREATE TABLE Users ( 
    UserId INT IDENTITY(1,1) PRIMARY KEY, 
    Username VARCHAR(100) NOT NULL UNIQUE, 
    PasswordHash VARCHAR(255) NOT NULL, 
    FullName NVARCHAR(255) NOT NULL, 
    Role VARCHAR(50) NOT NULL,  
    IsActive BIT DEFAULT 1 
);

CREATE TABLE Orders ( 
    -- Đã sửa thành NVARCHAR(50) và bỏ IDENTITY(1,1)
    OrderId NVARCHAR(50) PRIMARY KEY, 
    TableNumber VARCHAR(20) NULL,  
    OrderType NVARCHAR(50) NOT NULL,  
    Status NVARCHAR(50) DEFAULT N'Completed',  
    CreatedAt DATETIME DEFAULT GETDATE() 
); 

CREATE TABLE OrderDetails ( 
    OrderDetailId INT IDENTITY(1,1) PRIMARY KEY, 
    OrderId NVARCHAR(50) NOT NULL, 
    DishId VARCHAR(50) NOT NULL,  
    DishName NVARCHAR(255) NOT NULL, 
    CategoryName NVARCHAR(100) NOT NULL, 
    Quantity INT NOT NULL, 
    BasePrice DECIMAL(18,2) NOT NULL,  
    DiscountPercent INT NOT NULL,  
    PriceAfterDiscount DECIMAL(18,2) NOT NULL, 
    TotalToppingPrice DECIMAL(18,2) NOT NULL,  
    SelectedOptionsJson NVARCHAR(MAX) NULL,  
    ItemNote NVARCHAR(500) NULL,
    FOREIGN KEY (OrderId) REFERENCES Orders(OrderId) ON DELETE CASCADE 
);

CREATE TABLE Invoices ( 
    InvoiceId INT IDENTITY(1,1) PRIMARY KEY, 
    OrderId NVARCHAR(50) NOT NULL UNIQUE, 
    CashierId INT NULL,  
    SubTotal DECIMAL(18,2) NOT NULL, 
    TotalDiscount DECIMAL(18,2) NOT NULL, 
    FinalAmount DECIMAL(18,2) NOT NULL, 
    PaymentMethod NVARCHAR(50) NOT NULL,  
    PaymentStatus NVARCHAR(50) DEFAULT N'Paid', 
    PaidAt DATETIME DEFAULT GETDATE(), 
    
    FOREIGN KEY (CashierId) REFERENCES Users(UserId)

    
    -- FOREIGN KEY (OrderId) REFERENCES Orders(OrderId)
);