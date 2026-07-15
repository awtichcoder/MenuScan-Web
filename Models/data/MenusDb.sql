CREATE TABLE Users ( 

    UserId INT IDENTITY(1,1) PRIMARY KEY, 

    Username VARCHAR(100) NOT NULL UNIQUE, 

    PasswordHash VARCHAR(255) NOT NULL, 

    FullName NVARCHAR(255) NOT NULL, 

    Role VARCHAR(50) NOT NULL,  

    IsActive BIT DEFAULT 1 

);
CREATE TABLE Orders ( 

    OrderId INT IDENTITY(1,1) PRIMARY KEY, 

    TableNumber VARCHAR(20) NULL,  

    OrderType NVARCHAR(50) NOT NULL,  

    Status NVARCHAR(50) DEFAULT N'Completed',  

    CreatedAt DATETIME DEFAULT GETDATE() 

); 
CREATE TABLE OrderDetails ( 

    OrderDetailId INT IDENTITY(1,1) PRIMARY KEY, 

    OrderId INT NOT NULL, 

    DishId VARCHAR(50) NOT NULL,  

    DishName NVARCHAR(255) NOT NULL, 

    CategoryName NVARCHAR(100) NOT NULL, 

    Quantity INT NOT NULL, 

    BasePrice DECIMAL(18,2) NOT NULL,  

    DiscountPercent INT NOT NULL,  

    PriceAfterDiscount DECIMAL(18,2) NOT NULL, 

    TotalToppingPrice DECIMAL(18,2) NOT NULL,  

    SelectedOptionsJson NVARCHAR(MAX) NULL,  

    ItemNote NVARCHAR(500) NULL, -- <--- BỔ SUNG LƯU LẠI GHI CHÚ CỦA KHÁCH 

    FOREIGN KEY (OrderId) REFERENCES Orders(OrderId) ON DELETE CASCADE 

);

CREATE TABLE Invoices ( 

    InvoiceId INT IDENTITY(1,1) PRIMARY KEY, 

    OrderId INT NOT NULL UNIQUE, 

    CashierId INT NULL,  

    SubTotal DECIMAL(18,2) NOT NULL, -- Tiền món ăn gốc + Tiền topping 

    TotalDiscount DECIMAL(18,2) NOT NULL, -- Tiền được giảm do % 

    FinalAmount DECIMAL(18,2) NOT NULL, -- Khách móc ví trả bao nhiêu 

    PaymentMethod NVARCHAR(50) NOT NULL,  

    PaymentStatus NVARCHAR(50) DEFAULT N'Paid', 

    PaidAt DATETIME DEFAULT GETDATE(), 

    FOREIGN KEY (OrderId) REFERENCES Orders(OrderId), 

    FOREIGN KEY (CashierId) REFERENCES Users(UserId) 

);