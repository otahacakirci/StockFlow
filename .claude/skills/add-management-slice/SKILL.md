---
name: add-management-slice
description: StockFlow'a yeni bir rol duyarlı yönetim dikey dilimi (Service + ViewModel'ler + Controller + Razor View'lar + testler) ekleme tarifi; Category, Product, Customer ve Supplier dilimleri bu sırayla yapıldı. Yeni bir varlık için liste/detay/create/edit/delete ekranı eklerken veya mevcut bir dilime aynı kalıpla yeni action eklerken kullan.
---

# Yönetim dilimi ekle — StockFlow

Kaynak tarif: `git show --stat 8146575` (feat: add supplier management service) ve `git show --stat d1d898c` (feat(suppliers): add admin-only MVC supplier management). Örnek dilim: Supplier. Önce varlığın `docs/product-spec.md` kapsamında olduğunu doğrula; değilse bu bir kapsam değişikliğidir ve dur.

Dilim iki pakettir ve sırayla yapılır: **Service paketi** (`service-implementer`) → **Web paketi** (`web-implementer`). Entity/mapping/migration gerekiyorsa önce **Data paketi** (`data-implementer`, `.claude/rules/data.md`).

## Service paketi

1. `StockFlow/Services/<Çoğul>/` altında üç dosya: `I<Ad>Service.cs`, `<Ad>Service.cs` (`internal sealed`), `<Ad>ServiceErrorCodes.cs` (`"<ad>.<kod>"` sabitleri). Örnek: `StockFlow/Services/Suppliers/`.
2. Service sözleşmesindeki ViewModel'ler `StockFlow/ViewModels/<Çoğul>/` altında: `<Ad>InputModel`, `<Ad>ListQueryModel`, `<Ad>ListViewModel`, `<Ad>ViewModel`, `<Ad>SortOrder`, gerekiyorsa `<Ad>SelectionOptionViewModel` (Supplier commit'indeki dosya kümesi).
3. Kurallar: `.claude/rules/services.md` — `ServiceResult`, `AsNoTracking` projection, `ListPagingPolicy`, whitelist sıralama, `TrackedPersistence`, geçmişi olan kaydın silinmesini yarış koşuluna karşı yeniden doğrulama. İletişim alanları varsa `ContactInformationPolicy`.
4. Testi önce yaz: `StockFlow.Tests/Services/<Ad>ServiceTests.cs`, `SqlServerDatabaseTestBase`'den türer (örnek `StockFlow.Tests/Services/SupplierServiceTests.cs`). Çalıştır: `dotnet test StockFlow.Tests/StockFlow.Tests.csproj --no-restore --filter "FullyQualifiedName~<Ad>ServiceTests"`.
5. **Onaylı adım:** `StockFlow/Program.cs` içine `builder.Services.AddScoped<I<Ad>Service, <Ad>Service>();` (`Program.cs:64-72` sırası). Implementer bunu yapamaz; tam satırı raporunda bağımlılık olarak verir, ana oturum insan onayıyla ekler.

## Web paketi

1. `StockFlow/Controllers/<Çoğul>Controller.cs`: primary constructor ile Service + `ILogger`, sınıf seviyesinde rol (`[Authorize(Roles = …)]`, rol matrisi README › Roller), Index/Details/Create/Edit/Delete; POST'larda `[ValidateAntiForgeryToken]`; ServiceResult → form/404/409/500 eşlemesi; `returnUrl` için `Url.IsLocalUrl`. Örnek: `StockFlow/Controllers/SuppliersController.cs`, `.claude/rules/web-layer.md`.
2. Sayfa modeli gerekiyorsa `StockFlow/ViewModels/<Çoğul>/<Ad>EditPageViewModel.cs`.
3. View'lar `StockFlow/Views/<Çoğul>/`: `Index`, `Details`, `Create`, `Edit`, `Delete`, `NotFound`, ortak `_FormFields` partial'ı. Liste: arama, enum sıralama, 10/20/50/100 sayfa boyutu, filtre korumalı sayfalama. Silme kontrolü yalnız yetkili rol ve silinebilir kayıt için gösterilir.
4. Gezinme bağlantısı `StockFlow/Views/Shared/_Layout.cshtml` içinde, rol görünürlüğüyle.
5. Testler: `StockFlow.Tests/Controllers/<Çoğul>ControllerTests.cs` (DB'siz, Service test double'ı; örnek `SuppliersControllerTests.cs`), `StockFlow.Tests/ViewModels/<Çoğul>/<Ad>InputModelTests.cs` (DataAnnotations). Rol/antiforgery/sayfalama uçtan uca gerekiyorsa `StockFlow.Tests/Integration/MvcAcceptanceTests.cs`.

## Kapanış (ana oturum)

- Done gate: `dotnet build StockFlow.slnx --no-restore`, `dotnet test StockFlow.slnx --no-restore`, `dotnet format StockFlow.slnx --verify-no-changes --no-restore`.
- Rol matrisi değiştiyse README › Roller; yeni bir kural/konvansiyon ortaya çıktıysa `/curate-knowledge` (`.claude/rules/editing-discipline.md` › Dokümantasyon).
- Commit biçimi örnekleri: `feat: add <ad> management service`, `feat(<çoğul>): add role-aware MVC <ad> management`.
