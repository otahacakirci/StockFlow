---
paths:
  - "StockFlow/Controllers/**"
  - "StockFlow/Views/**"
  - "StockFlow/ViewModels/**"
  - "StockFlow/ModelBinding/**"
  - "StockFlow/wwwroot/js/**"
  - "StockFlow/wwwroot/css/**"
---

# Web katmanı (Controller, ViewModel, Razor)

Controller'ları ince, HTTP sözleşmesini güvenli tutar; iş kuralı Service'te kalır (`docs/product-spec.md` › 3.2 Mimari sorumluluklar).

## Controller

- Controller `ApplicationDbContext` kullanmaz; yalnız Service arayüzlerine bağımlıdır (`StockFlow/Controllers/CategoriesController.cs:13-15`) — `CLAUDE.md` › Değişmez proje sınırları.
- Roller README › Roller matrisine göre controller/action seviyesinde zorlanır. Kalıplar: sınıfta Admin+Employee ve Admin'e özel action'larda ek `[Authorize(Roles = AppRoles.Admin)]` (`CategoriesController.cs:12`, `:48`); Employee'nin de yazabildiği akışlarda (Customer create/edit, Order Draft create/edit) ek Admin özniteliği yoktur; tamamen Admin'e özel alanlarda sınıf seviyesinde Admin (`SuppliersController.cs:12`). Yeni action'ın rolünü matristen al, en yakın controller'ı varsayma.
- GET yalnız form/onay sayfası üretir; mutasyon antiforgery korumalı POST ile yapılır (`StockFlow/Controllers/OrdersController.cs`).
- `ServiceResult` eşlemesi: `Validation` → form alanı hatası ve aynı View; `NotFound` → güvenli 404 görünümü; `BusinessRule` → güncel onay görünümüyle 409; beklenmeyen tipli hata → loglanır ve güvenli 500 (`CategoriesController.cs:75-81`, `:190`, `:230-247`).
- Gerçek exception'ları yakalama; merkezi hata middleware'ine yayılır (hiçbir `StockFlow/Controllers/*.cs` dosyası `catch` içermez; `StockFlow/Program.cs:108`).
- Başarı mesajı `TempData["SuccessMessage"]` ile, ardından Post/Redirect/Get (`CategoriesController.cs:71-72`).
- Service çağıran async action'lar `CancellationToken` alır ve Service'e iletir (`CategoriesController.cs:22-24`); Identity login/logout action'ları (`AccountController.cs:32`, `:66`) bunun dışındadır.

## ViewModel

- Entity nesnesi kullanıcı girdisi veya View modeli olarak kullanılmaz; giriş için `*InputModel`, liste sorgusu için `*ListQueryModel`, çıkış için `*ViewModel` (`StockFlow/ViewModels/Categories/`).
- Sunucunun hesapladığı alanlar POST modelinde yer almaz: Order formu `UnitPrice`/`TotalAmount` (`StockFlow/ViewModels/Orders/OrderDraftInputModel.cs`, `OrderItemInputModel.cs`), Product edit modeli `StockQuantity` taşımaz (`StockFlow/ViewModels/Products/ProductUpdateInputModel.cs`; yalnız create modeli başlangıç stoğu alır, `ProductCreateInputModel.cs:31`).
- DataAnnotations mesajları Türkçedir ve uzunluk sınırları Fluent mapping ile aynıdır (ör. Customer 150/256/32/500).

## Razor ve istemci

- UI görünürlüğü yetkilendirmenin tek katmanı değildir; aynı karar action'da da uygulanır.
- Para `tr-TR` Türk lirası, tarih açık UTC gösterilir; ondalık giriş `TurkishDecimalModelBinder` ile katı Türkçe biçimdedir (`StockFlow/ModelBinding/TurkishDecimalModelBinder.cs`).
- Liste ekranları filtre/sıralama/sayfa durumunu sayfalama bağlantılarında korur; sayfa boyutu 10/20/50/100.
- `StockFlow/wwwroot/lib/**` üçüncü taraf dağıtımıdır; elle düzenleme.

## Testler

- Controller testleri veritabanısızdır ve Service'leri test double ile değiştirir (`StockFlow.Tests/Controllers/CategoriesControllerTests.cs`); uçtan uca davranış `StockFlow.Tests/Integration/MvcAcceptanceTests.cs` içindedir.
