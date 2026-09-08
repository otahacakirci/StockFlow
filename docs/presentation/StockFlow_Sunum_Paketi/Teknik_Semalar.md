# StockFlow teknik şemaları

Şemalar iş modelini özetler. Düzenlenebilir şekiller sunumun 4, 7, 8 ve 9. slaytlarında, PNG karşılıkları Slayt_Gorselleri klasöründedir.

## Mimari

```mermaid
flowchart LR
 V["Razor / Bootstrap"] -->|ViewModel| C[Controller]
 C --> S[Service]
 S --> E["EF Core ApplicationDbContext"]
 E --> DB[(SQL Server)]
 I["Cookie Identity"] -.-> C
```

Tek uygulama host'u. Controller DbContext kullanmaz. Entity form girdisi değildir.

## Durum makinesi

```mermaid
stateDiagram-v2
 [*] --> Draft
 Draft --> Confirmed: Admin onayı ve atomik kayıt
 Draft --> Cancelled: Admin iptali
 Confirmed --> [*]
 Cancelled --> [*]
```

Draft stok ve rezervasyon üretmez. Cancelled stok etkisi üretmez. Terminal durumlardan geri dönüş yoktur.

## İş ilişkileri

```mermaid
erDiagram
 Category ||--o{ Product : gruplar
 Customer o|--o{ Order : "Sale tarafı"
 Supplier o|--o{ Order : "Purchase tarafı"
 Order ||--|{ OrderItem : kalemleri
 Product ||--o{ OrderItem : kullanilir
 Order ||--o{ StockMovement : uretir
 Product ||--o{ StockMovement : hareketleri
 ApplicationUser o|--o{ Order : olusturur
```

Order'ın en az bir kalem taşıması Service kuralıdır. Sale'de yalnız Customer, Purchase'da yalnız Supplier doludur. CreatedByUserId şemada nullable, yeni sipariş servisinde mevcut kullanıcı zorunludur. Identity destek tabloları gösterilmemiştir. OrderItem.UnitPrice geçmiş fiyatı taşır. OrderId + ProductId çifti benzersizdir.

## Atomik onay

```mermaid
flowchart TD
 A["BeginTransactionAsync"] --> B["Order + Items + Product oku"]
 B --> C["Draft, taraf, kalem ve fiyat kuralları"]
 C --> D["CreatePlan: bütün stokları hesapla"]
 D -->|Onay| E["Stok + StockMovement + Confirmed hazırla"]
 E --> F[SaveChangesAsync]
 F --> G[CommitAsync]
 C -->|İhlal| R["Rollback ve tracker temizliği"]
 D -->|Ret| R
 F -->|Hata| R
```

Sale StockOut, Purchase StockIn üretir. Başarısız onay ilgili siparişi Draft bırakır ve kısmi değişiklik bırakmaz. Bu şema transaction sınırını gösterir. Eşzamanlı onayların tüm yarışları için ek strateji gerekir.
