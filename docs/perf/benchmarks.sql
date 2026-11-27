-- Rapor sorgularında index etkisi. Her sorgu iki kez çalışır: optimizer'ın seçtiği plan ve
-- WITH (INDEX(0)) ile zorlanan clustered index taraması (index yokmuş gibi).
-- Çalıştırma: sqlcmd -I -d ErpAssistant -i benchmarks.sql   (STATISTICS IO/TIME çıktısı ölçümdür)
SET NOCOUNT ON;
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

DECLARE @from datetime2 = DATEADD(month, -12, CAST(GETDATE() AS date)), @to datetime2 = CAST(GETDATE() AS date);

PRINT '== 1a. Aylık ciro (IX_Orders_OrderDate_Covering)';
SELECT YEAR(OrderDate) y, MONTH(OrderDate) m, COUNT(*) c, SUM(TotalAmount) r
FROM Orders WHERE OrderDate >= @from AND OrderDate < @to AND Status NOT IN (0, 4)
GROUP BY YEAR(OrderDate), MONTH(OrderDate);

PRINT '== 1b. Aylık ciro (zorla tam tarama)';
SELECT YEAR(OrderDate) y, MONTH(OrderDate) m, COUNT(*) c, SUM(TotalAmount) r
FROM Orders WITH (INDEX(0)) WHERE OrderDate >= @from AND OrderDate < @to AND Status NOT IN (0, 4)
GROUP BY YEAR(OrderDate), MONTH(OrderDate);

DECLARE @m1 datetime2 = DATEADD(month, -1, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1));
DECLARE @m2 datetime2 = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);

PRINT '== 2a. Geçen ayın en çok satan 10 ürünü (covering index''ler)';
SELECT TOP 10 l.ProductId, SUM(l.Quantity) q, SUM(l.LineTotal) r
FROM Orders o JOIN OrderLines l ON l.OrderId = o.Id
WHERE o.OrderDate >= @m1 AND o.OrderDate < @m2 AND o.Status NOT IN (0, 4)
GROUP BY l.ProductId ORDER BY r DESC;

PRINT '== 2b. Geçen ayın en çok satan 10 ürünü (zorla tam tarama)';
SELECT TOP 10 l.ProductId, SUM(l.Quantity) q, SUM(l.LineTotal) r
FROM Orders o WITH (INDEX(0)) JOIN OrderLines l WITH (INDEX(0)) ON l.OrderId = o.Id
WHERE o.OrderDate >= @m1 AND o.OrderDate < @m2 AND o.Status NOT IN (0, 4)
GROUP BY l.ProductId ORDER BY r DESC;

PRINT '== 3a. Temsilcinin son 3 ay siparişleri (IX_Orders_SalesRepresentativeId_OrderDate)';
SELECT COUNT(*), SUM(TotalAmount) FROM Orders
WHERE SalesRepresentativeId = 1 AND OrderDate >= DATEADD(month, -3, GETDATE());

PRINT '== 3b. Temsilcinin son 3 ay siparişleri (zorla tam tarama)';
SELECT COUNT(*), SUM(TotalAmount) FROM Orders WITH (INDEX(0))
WHERE SalesRepresentativeId = 1 AND OrderDate >= DATEADD(month, -3, GETDATE());

PRINT '== 4a. Vadesi 60 günü geçmiş açık faturalar (IX_Invoices_Status_DueDate)';
SELECT COUNT(*), SUM(TotalAmount - PaidAmount) FROM Invoices
WHERE Status IN (0, 1) AND DueDate < DATEADD(day, -60, GETDATE());

PRINT '== 4b. Vadesi 60 günü geçmiş açık faturalar (zorla tam tarama)';
SELECT COUNT(*), SUM(TotalAmount - PaidAmount) FROM Invoices WITH (INDEX(0))
WHERE Status IN (0, 1) AND DueDate < DATEADD(day, -60, GETDATE());

PRINT '== 5a. Kritik stok (indekslenmiş view, NOEXPAND)';
SELECT COUNT(*) FROM vw_StockBalance b WITH (NOEXPAND) JOIN Products p ON p.Id = b.ProductId
WHERE b.Quantity < p.ReorderLevel;

PRINT '== 5b. Kritik stok (hareketlerden anlık toplama)';
SELECT COUNT(*) FROM (
    SELECT ProductId, SUM(Quantity) q FROM StockMovements WITH (INDEX(0)) GROUP BY ProductId, WarehouseId
) b JOIN Products p ON p.Id = b.ProductId WHERE b.q < p.ReorderLevel;
