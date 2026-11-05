-- Run from repository root using sqlcmd -b -i tests/schema-dry-run.sql.
IF DB_NAME()<>'QLKhachSanDB' OR ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),0)<>1
    THROW 50100, 'Schema dry run is restricted to LocalDB QLKhachSanDB.', 1;
BEGIN TRANSACTION;
:r database/20261104_safe_hotel_fixes.sql
ROLLBACK TRANSACTION;
PRINT 'PASS: schema upgrade validated inside rollback-only outer transaction';
