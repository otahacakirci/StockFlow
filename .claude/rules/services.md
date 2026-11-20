---
paths:
  - "StockFlow/Services/**"
  - "StockFlow.Tests/Services/**"
---

# Service katmanı

İş kurallarının, doğrulamanın ve kalıcılaştırma akışının tek sahibi Service'lerdir (`docs/product-spec.md` › 3.2 Mimari sorumluluklar).

## Sözleşme

- Yönetim domain'leri kendi klasöründe `I<Ad>Service`, `internal sealed class <Ad>Service` ve `<Ad>ServiceErrorCodes` üçlüsüyle durur (`StockFlow/Services/Categories/`). İstisnalar: `Dashboard/` hata kodu dosyası taşımaz; `Orders/` mutasyon (`OrderService`) ve sorgu (`OrderQueryService`) Service'lerini, planner'ı ve ortak `OrderServiceErrorCodes` dosyasını birlikte barındırır. Yeni Service `StockFlow/Program.cs:64-72` içinde `AddScoped` ile kaydedilir — `Program.cs` insan onayı ister; implementer bunu bağımlılık olarak raporlar.
- Beklenen sonuçlar exception değil `ServiceResult` / `ServiceResult<T>` döner (`StockFlow/Services/Common/ServiceResult.cs:6`, `:39`); kategori yalnız `Validation`, `NotFound`, `BusinessRule` (`StockFlow/Services/Common/ServiceErrorCategory.cs:6-10`).
- Hata kodları kararlı `"<domain>.<snake_case>"` sabitleridir (`StockFlow/Services/Categories/CategoryServiceErrorCodes.cs:8-12`); Controller ve testler bu sabitlerle eşleşir, metin karşılaştırması yapılmaz. Başka domain'e ait hata kodu kopyalanmaz, sahibi olan katalogdan alias'lanır (`StockFlow/Services/Orders/OrderServiceErrorCodes.cs:18-26`); mevcut public sabitleri kaldırma.
- Bugünkü sözleşmede Service'ler `StockFlow.ViewModels.*` giriş/çıkış modellerini kullanır (`StockFlow/Services/Categories/CategoryService.cs:5`). Bu bağımlılığı kaldırmak `docs/product-spec.md` › 10.1 Faz 2 kapsamıdır; Faz 2 kapısı açılmadan değiştirme.
- Rol/yetki kontrolü Service'e taşınmaz; HTTP katmanında kalır (Service'ler `AppRoles` kullanmaz).

## Sorgular

- Okuma sorguları `AsNoTracking()` ve ViewModel projection kullanır; entity grafiği View'a taşınmaz (`StockFlow/Services/Categories/CategoryService.cs:23`, `:62`).
- Sayfalama ortak `ListPagingPolicy` üzerinden: 20 varsayılan, 100 üst sınır, taşan sayfa son sayfaya çekilir (`CategoryService.cs:32`, `:203`).
- Sıralama whitelist enum ile yapılır; kullanıcıdan gelen alan adıyla dinamik sıralama yok.
- Girdi trimlenir, boş opsiyonel alan `null` olur; iletişim alanları için ortak `ContactInformationPolicy` (`StockFlow/Services/Common/ContactInformationPolicy.cs`).

## Yazma

- Transaction dışı kayıt `TrackedPersistence.SaveChangesAsync` ile yapılır: beklenmeyen hatada domain'e özgü log, tracker temizliği ve yeniden fırlatma (`StockFlow/Services/Common/TrackedPersistence.cs:10-31`).
- Fiziksel silme koşulları (`docs/product-spec.md` › 5.2–5.4: FR-PROD-06, FR-PARTY-03, FR-ORD-07/08; › 6 veri modeli tablosu) gerçek silme anında yarış koşuluna karşı yeniden doğrulanır (ör. `StockFlow/Services/Customers/CustomerService.cs:172-183`):

  | Kayıt | Silinebilir |
  | --- | --- |
  | Category | Bağlı Product yoksa |
  | Product | OrderItem ve StockMovement geçmişi yoksa |
  | Customer | Bağlı Order yoksa (yalnız Admin — HTTP katmanı) |
  | Supplier | Bağlı Order yoksa |
  | Order | Yalnız Draft ise (yalnız Admin — HTTP katmanı) |

- Ürün sözleşmesinde veya veritabanında olmayan benzersizlik kuralı ekleme (ör. Category adı, Customer e-postası); bugün benzersiz olan yalnız `Product.Sku` (`docs/product-spec.md` › 6.2).
- Dashboard'da düşük stok `StockQuantity <= MinimumStockQuantity`, toplam satış yalnız Confirmed Sale siparişleridir; sorgular `DashboardService` içinde kalır (`StockFlow/Services/Dashboard/DashboardService.cs`).
- Kişisel/iletişim verisini loglama.

## Testler

- Service testleri `SqlServerDatabaseTestBase` üzerinden gerçek LocalDB kullanır (`StockFlow.Tests/Services/CategoryServiceTests.cs:13`); bağımlılıksız planner/policy sınıfları veritabanısız test edilir (`StockFlow.Tests/Services/OrderStockConfirmationPlannerTests.cs`).
