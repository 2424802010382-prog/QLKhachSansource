-- Run manually against QLKhachSanDB (LocalDB) OR db_ad0565_luxuryhotel (hosting).
-- Select the target database first. No USE, DROP, DELETE, password or connection change.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
IF OBJECT_ID(N'dbo.RoomMaintenances', N'U') IS NULL
    THROW 50001, 'RoomMaintenances is missing. Restore the existing schema; do not recreate or erase it.', 1;
IF OBJECT_ID(N'dbo.DonDatMon', N'U') IS NULL
    THROW 50002, 'DonDatMon is missing. Restore the existing restaurant schema first.', 1;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.RoomMaintenances','EndedAt') IS NULL
    ALTER TABLE dbo.RoomMaintenances ADD EndedAt datetime NULL;
IF COL_LENGTH('dbo.DonDatMon','PaidAt') IS NULL
    ALTER TABLE dbo.DonDatMon ADD PaidAt datetime2 NULL;
IF COL_LENGTH('dbo.DonDatMon','PaymentReference') IS NULL
    ALTER TABLE dbo.DonDatMon ADD PaymentReference nvarchar(200) NULL;
IF COL_LENGTH('dbo.DonDatMon','PaymentEvidence') IS NULL
    ALTER TABLE dbo.DonDatMon ADD PaymentEvidence nvarchar(500) NULL;
IF COL_LENGTH('dbo.DonDatMon','PayWithBooking') IS NULL
    ALTER TABLE dbo.DonDatMon ADD PayWithBooking bit NOT NULL CONSTRAINT DF_DonDatMon_PayWithBooking DEFAULT(0);
IF COL_LENGTH('dbo.DonDatMon','BookingServiceId') IS NULL
    ALTER TABLE dbo.DonDatMon ADD BookingServiceId int NULL;
-- Existing columns must also be compatible; never silently convert old data.
IF EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID('dbo.DonDatMon') AND
    ((c.name='PayWithBooking' AND (TYPE_NAME(c.user_type_id)<>'bit' OR c.is_nullable=1)) OR
     (c.name='BookingServiceId' AND (TYPE_NAME(c.user_type_id)<>'int' OR c.is_nullable=0)) OR
     (c.name='PaidAt' AND (TYPE_NAME(c.user_type_id) NOT IN ('datetime','datetime2') OR c.is_nullable=0)) OR
     (c.name='PaymentReference' AND (TYPE_NAME(c.user_type_id)<>'nvarchar' OR c.is_nullable=0 OR (c.max_length<>-1 AND c.max_length<400))) OR
     (c.name='PaymentEvidence' AND (TYPE_NAME(c.user_type_id)<>'nvarchar' OR c.is_nullable=0 OR (c.max_length<>-1 AND c.max_length<1000)))))
    THROW 50004, 'Existing DonDatMon columns have incompatible types/nullability/lengths; review the schema explicitly. No data was converted.', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.RoomMaintenances') AND name='EndedAt'
    AND (TYPE_NAME(user_type_id) NOT IN ('datetime','datetime2') OR is_nullable=0))
    THROW 50005, 'EndedAt must be a nullable datetime/datetime2 column. Review existing schema without overwriting history.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints d JOIN sys.columns c
    ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id
    WHERE c.object_id=OBJECT_ID('dbo.DonDatMon') AND c.name='PayWithBooking')
    ALTER TABLE dbo.DonDatMon ADD CONSTRAINT DF_DonDatMon_PayWithBooking DEFAULT(0) FOR PayWithBooking;
IF EXISTS (SELECT 1 FROM sys.default_constraints d JOIN sys.columns c
    ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id
    WHERE c.object_id=OBJECT_ID('dbo.DonDatMon') AND c.name='PayWithBooking'
      AND REPLACE(REPLACE(REPLACE(d.definition,'(',''),')',''),' ','')<>'0')
    THROW 50006, 'PayWithBooking must default to zero for ordinary restaurant orders. Review nonstandard defaults manually.', 1;
-- Do not invent historical payment timestamps: legacy paid rows retain PaidAt=NULL.
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Bookings') AND name='IX_Bookings_RoomDates')
    CREATE INDEX IX_Bookings_RoomDates ON dbo.Bookings(RoomId,CheckInDate,CheckOutDate) INCLUDE(RoomCount,Status);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.RoomMaintenances') AND name='IX_RoomMaintenances_RoomDates')
    CREATE INDEX IX_RoomMaintenances_RoomDates ON dbo.RoomMaintenances(PhysicalRoomId,StartDate,EndDate);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.PhysicalRooms') AND name='UX_PhysicalRooms_RoomNumber')
BEGIN
    IF EXISTS(SELECT RoomNumber FROM dbo.PhysicalRooms GROUP BY RoomNumber HAVING COUNT(*)>1)
        THROW 50003, 'Duplicate room numbers found; resolve the data explicitly before adding the unique index.', 1;
    CREATE UNIQUE INDEX UX_PhysicalRooms_RoomNumber ON dbo.PhysicalRooms(RoomNumber);
END;
-- Dynamic SQL allows the new column to be compiled after ALTER on first execution.
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.DonDatMon') AND name='UX_DonDatMon_PaymentReference')
    EXEC(N'CREATE UNIQUE INDEX UX_DonDatMon_PaymentReference ON dbo.DonDatMon(PaymentReference) WHERE PaymentReference IS NOT NULL');
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.DonDatMon') AND name='FK_DonDatMon_BookingService')
    EXEC(N'ALTER TABLE dbo.DonDatMon WITH CHECK ADD CONSTRAINT FK_DonDatMon_BookingService FOREIGN KEY(BookingServiceId) REFERENCES dbo.DatPhongDichVu(MaDatPhongDichVu)');
COMMIT TRANSACTION;
SELECT DB_NAME() AS TargetDatabase, 'Safe additive migration complete' AS Result;
