# ERP veri modeli

```mermaid
erDiagram
    Department ||--o{ SalesRepresentative : "çalışır"
    SalesRepresentative ||--o{ Customer : "sorumlu"
    Customer ||--o{ Order : "verir"
    SalesRepresentative ||--o{ Order : "temsilci (denormalize)"
    Warehouse ||--o{ Order : "sevk eder"
    Order ||--|{ OrderLine : "kalem"
    Product ||--o{ OrderLine : ""
    ProductCategory ||--o{ Product : ""
    Order ||--o| Invoice : "faturalanır"
    Customer ||--o{ Invoice : ""
    Invoice ||--o{ Payment : "tahsilat"
    Product ||--o{ StockMovement : ""
    Warehouse ||--o{ StockMovement : ""
    Department ||--o{ Document : "sahiplik"
    Document ||--o{ DocumentChunk : "parça"

    Customer {
        int Id PK
        string Code UK
        string Name
        string TaxNumber UK
        int SalesRepresentativeId FK
        decimal CreditLimit
    }
    Order {
        int Id PK
        string OrderNumber UK
        int CustomerId FK
        int SalesRepresentativeId FK
        int WarehouseId FK
        datetime OrderDate
        byte Status
        decimal TotalAmount
    }
    OrderLine {
        int Id PK
        int OrderId FK
        int ProductId FK
        int Quantity
        decimal UnitPrice
        decimal DiscountRate
        decimal LineTotal
    }
    Invoice {
        int Id PK
        string InvoiceNumber UK
        int OrderId FK "UK"
        int CustomerId FK
        int SalesRepresentativeId FK
        datetime DueDate
        decimal TotalAmount
        decimal PaidAmount
        byte Status
    }
    Payment {
        int Id PK
        int InvoiceId FK
        decimal Amount
        datetime PaidAt
    }
    Product {
        int Id PK
        string Sku UK
        string Name
        int CategoryId FK
        decimal UnitPrice
        decimal UnitCost
        int ReorderLevel
    }
    StockMovement {
        int Id PK
        int ProductId FK
        int WarehouseId FK
        byte Type
        int Quantity "işaretli"
        datetime OccurredAt
    }
    Document {
        int Id PK
        string FileName
        int DepartmentId FK
        byte Status
        int ChunkCount
    }
    DocumentChunk {
        int Id PK
        int DocumentId FK
        int PageNumber
        guid VectorId UK
    }
```

Tabloya dahil olmayanlar:

- **`vw_StockBalance`** — `StockMovements` üzerinde `WITH SCHEMABINDING` indekslenmiş view
  (`ProductId, WarehouseId` benzersiz clustered index). Bakiye hiçbir tabloda saklanmaz; SQL Server
  view'ı her hareket insert'inde günceller.
- **`AiQueryLogs`** — asistana sorulan her soru, çağrılan araçlar ve dönen satır sayısı.
- **ASP.NET Core Identity tabloları** (Faz 3) — kullanıcı, rol ve yenileme token'ları.

## Denormalizasyon kararı

`Orders.SalesRepresentativeId` ve `Invoices.SalesRepresentativeId` müşteriden kopyalanır. Böylece:

- satır yetkisi filtresi (`WHERE SalesRepresentativeId = @rep`) join'siz ve index üzerinden çalışır,
- müşteri başka temsilciye devredildiğinde geçmiş satışlar eski temsilcide kalır (komisyon raporu
  açısından doğru davranış).

## Sentetik veri

`ErpDataSeeder` (Bogus, `tr` yereli, sabit tohum):

| Varlık | Adet |
|---|---|
| Departman | 4 |
| Satış temsilcisi | 8 |
| Depo | 3 |
| Ürün kategorisi / ürün | 12 / 500 |
| Müşteri | 200 |
| Sipariş / kalem | 20.000 / ≈54.000 |
| Fatura / tahsilat | ≈19.000 / ≈17.000 |

Sipariş tarihleri son 24 aya yayılır; hafif büyüme trendi, mevsimsellik ve hafta sonu düşüşü vardır.
Ürün ve müşteri seçimi ağırlıklıdır (az sayıda ürün satışların büyük kısmını yapar). Ödeme davranışı:
%72 vadesinde, %15 geç, %7 kısmi, %6 hiç ödemiyor — "geciken tahsilatlar" sorusu için.
