---
paths:
  - "StockFlow/Services/Orders/**"
  - "StockFlow/Services/StockMovements/**"
  - "StockFlow/Controllers/OrdersController.cs"
  - "StockFlow/Controllers/StockMovementsController.cs"
  - "StockFlow/ViewModels/Orders/**"
  - "StockFlow/Entities/Order*.cs"
  - "StockFlow/Entities/StockMovement.cs"
  - "StockFlow.Tests/Services/Order*.cs"
  - "StockFlow.Tests/Services/StockMovementQueryServiceTests.cs"
---

# Sipariş ve stok

StockFlow'un çekirdek değeri: stok yalnız tek atomik Confirm ile değişir (README giriş paragrafı, `docs/product-spec.md` › 7. Sipariş ve Stok Sözleşmeleri). Ayrıntılı durum makinesi ve atomik onay sözleşmesi: `docs/product-spec.md` › 7 ve 7.1.

```text
Draft ──Confirm (bütün doğrulamalar + atomik kayıt)──> Confirmed  (terminal)
Draft ──Cancel (stok hareketi yok)─────────────────────> Cancelled (terminal)
```

## Değişmezler

- Draft oluşturma/düzenleme stok veya `StockMovement` değiştirmez — `CLAUDE.md` › Değişmez proje sınırları.
- Confirm; bütün stok kontrolleri geçtikten sonra Product stoklarını, `StockMovement` kayıtlarını ve `Confirmed` durumunu tek açık transaction ve tek `SaveChangesAsync` ile yazar (`StockFlow/Services/Orders/OrderService.cs:151`, `:196-197`). Beklenen ihlalde rollback ile `ServiceResult` hatası döner (`OrderService.cs:919`).
- Stok yeni değerleri entity mutation'dan önce bağımlılıksız `OrderStockConfirmationPlanner` ile hesaplanır; planın uygulanması ve transaction sınırı `OrderService` içinde kalır.
- Uygulama kodunda `StockMovement` oluşturma ve Product stok mutasyonu yalnız `OrderService` confirm akışındadır (tek istisna Development demo kataloğudur: `StockFlow/Data/DemoDataCatalog.cs`, tutarlı başlangıç verisi üretir ve kullanıcı akışı değildir); `StockMovementQueryService` salt-okunurdur ve `StockMovementsController` POST action içermez.
- `Confirmed` ve `Cancelled` terminaldir: yeniden açılmaz, düzenlenmez, fiziksel silinmez. Cancel stok hareketi üretmez. Yalnız hareket geçmişi olmayan Draft fiziksel silinir.
- Purchase → StockIn, Sale → StockOut. Sale onayı herhangi bir kalemde stok yetersizse, Purchase onayı stok sayacı taşarsa reddedilir; planner ilk ihlalde durur (`StockFlow/Services/Orders/OrderStockConfirmationPlanner.cs:18`, `:42`, `:67`).
- Fiyat snapshot'ı: yeni kalem güncel Product fiyatını alır, mevcut Draft kaleminin `UnitPrice` değerine dokunulmaz (`StockFlow/Services/Orders/OrderService.cs:330-356`); `UnitPrice`/`TotalAmount` istemciden kabul edilmez.
- Sipariş tarafı: Sale yalnız Customer, Purchase yalnız Supplier (`OrderService.cs:363-368`, DB'de `CK_Orders_PartyMatchesType`); oluşturan kullanıcı zorunludur (`OrderService.cs:47`).

## HTTP eşlemesi

- Confirm/Cancel/Delete yalnız Admin; BusinessRule ve terminal durum yarışı 409 üretir (`StockFlow/Controllers/OrdersController.cs:334`, `:375`, `:491`).

## Test beklentisi

- Stok veya durum davranışı değişiyorsa `StockFlow.Tests/Services/OrderServiceTests.cs` içinde gerçek SQL Server test veritabanında (Testcontainers MSSQL, `.claude/rules/testing.md` › Veritabanı) atomiklik/rollback testi ve gerekirse `OrderStockConfirmationPlannerTests.cs` içinde saf test ekle.
- Davranış değişikliği `docs/product-spec.md` › 7 ve bu kuralın güncellenmesini gerektirir (`.claude/rules/editing-discipline.md` › Dokümantasyon) — ürün kapsamı kullanıcı yetkisi olmadan değişmez.
