-- ============================================================================
-- SCRIPT KHỞI TẠO BẢNG HOÀN TIỀN CHO DỰ ÁN QLKhachSan - LUXURY HOTEL
-- CHẠY AN TOÀN TRÊN SQL SERVER (SITE4NOW / SMARTERASP.NET / LOCALDB)
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RefundRequests]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RefundRequests](
        [RefundRequestId] INT IDENTITY(1,1) NOT NULL,
        [BookingId] INT NOT NULL,
        [UserId] INT NOT NULL,
        [CustomerName] NVARCHAR(100) NOT NULL,
        [CustomerPhone] NVARCHAR(20) NOT NULL,
        [CustomerEmail] NVARCHAR(100) NOT NULL,
        [PaidAmount] DECIMAL(18, 2) NOT NULL,
        [RefundRate] DECIMAL(5, 2) NOT NULL,
        [RefundAmount] DECIMAL(18, 2) NOT NULL,
        [BankName] NVARCHAR(100) NOT NULL,
        [AccountNumber] NVARCHAR(50) NOT NULL,
        [AccountHolder] NVARCHAR(100) NOT NULL,
        [Reason] NVARCHAR(MAX) NOT NULL,
        [Status] NVARCHAR(50) NOT NULL, -- ChoDuyet, NhanVienDaDuyet, DaHoanTien, TuChoi
        [RequestedAt] DATETIME NOT NULL,
        [StaffUserId] INT NULL,
        [StaffVerifiedAt] DATETIME NULL,
        [AdminUserId] INT NULL,
        [AdminApprovedAt] DATETIME NULL,
        [RejectedReason] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_RefundRequests] PRIMARY KEY CLUSTERED ([RefundRequestId] ASC),
        CONSTRAINT [FK_RefundRequests_Bookings] FOREIGN KEY ([BookingId]) REFERENCES [dbo].[Bookings] ([BookingId]),
        CONSTRAINT [FK_RefundRequests_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId]),
        CONSTRAINT [UQ_RefundRequests_BookingId] UNIQUE ([BookingId])
    );
    PRINT 'Da tao bang RefundRequests thanh cong.';
END
ELSE
BEGIN
    PRINT 'Bang RefundRequests da ton tai.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RefundTransactions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RefundTransactions](
        [RefundTransactionId] INT IDENTITY(1,1) NOT NULL,
        [RefundRequestId] INT NOT NULL,
        [BookingId] INT NOT NULL,
        [TransactionCode] NVARCHAR(100) NOT NULL,
        [ReceiptImageUrl] NVARCHAR(500) NULL,
        [Amount] DECIMAL(18, 2) NOT NULL,
        [ProcessedAt] DATETIME NOT NULL,
        [ProcessedByUserId] INT NOT NULL,
        [Notes] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_RefundTransactions] PRIMARY KEY CLUSTERED ([RefundTransactionId] ASC),
        CONSTRAINT [FK_RefundTransactions_RefundRequests] FOREIGN KEY ([RefundRequestId]) REFERENCES [dbo].[RefundRequests] ([RefundRequestId]),
        CONSTRAINT [FK_RefundTransactions_Bookings] FOREIGN KEY ([BookingId]) REFERENCES [dbo].[Bookings] ([BookingId]),
        CONSTRAINT [FK_RefundTransactions_Users] FOREIGN KEY ([ProcessedByUserId]) REFERENCES [dbo].[Users] ([UserId])
    );
    PRINT 'Da tao bang RefundTransactions thanh cong.';
END
ELSE
BEGIN
    PRINT 'Bang RefundTransactions da ton tai.';
END
GO
