---
paths:
  - "StockFlow.Tests/**"
  - "StockFlow/**/*.cs"
---

# Test

Davranışı en dar ilgili testle kanıtlamayı ve test sonuçlarını dürüst raporlamayı sağlar (`CLAUDE.md` › Definition of Done, `docs/product-spec.md` › 8.3 Test ve teslim kalitesi).

## Yerleşim ve adlandırma

- Test projesi kaynak ağacını yansıtır: `StockFlow.Tests/{Controllers,Services,Data,ViewModels,ModelBinding,Integration,Infrastructure}`.
- Sınıf adı `<Konu>Tests`, `public sealed class` (`StockFlow.Tests/Services/OrderServiceTests.cs:14`); metot adı `<Metot>_<Koşul>_<Beklenti>` veya `<Metot>_<Beklenti>` (`StockFlow.Tests/Controllers/CategoriesControllerTests.cs:52`, `OrderServiceTests.cs:21`).
- xUnit `[Fact]`/`[Theory]`; `Xunit` global using'dir (`StockFlow.Tests/StockFlow.Tests.csproj`).

## Veritabanı

- Veritabanı gerektiren test `SqlServerTestDatabase` altyapısını kullanır: Service/Data testleri `SqlServerDatabaseTestBase`'den, MVC uçtan uca testleri `MvcIntegrationTestBase`'den türer (`StockFlow.Tests/Integration/StockFlowWebApplicationFactory.cs:34`). Her test `(localdb)\MSSQLLocalDB` üzerinde benzersiz `StockFlow_Tests_<guid>` oluşturur, migration uygular ve sonunda siler (`StockFlow.Tests/Infrastructure/SqlServerTestDatabase.cs:11-12`, ADR-0002).
- EF Core InMemory, SQLite veya geliştirme veritabanına geri dönüş yok; test altyapısı uygulama bağlantı ayarlarını veya User Secrets'ı okumaz.
- Saf doğrulama/planner/policy testleri veritabanı oluşturmaz (`StockFlow.Tests/Services/OrderStockConfirmationPlannerTests.cs`).
- MVC uçtan uca testleri `StockFlowWebApplicationFactory` ile gerçek pipeline ve Identity cookie kullanır (`StockFlow.Tests/Integration/StockFlowWebApplicationFactory.cs:9`).

## Çalıştırma ve raporlama

- Önce değiştirilen davranışa en yakın testi çalıştır: `dotnet test StockFlow.Tests/StockFlow.Tests.csproj --no-restore --filter "FullyQualifiedName~<Ad>Tests"`; sonra done gate.
- Davranış değişikliğinde testi önce yaz, doğru nedenle kırmızı olduğunu göster, sonra yeşile getir; raporda iki çıktıyı da ver.
- LocalDB açılamazsa (`SqlServerTestDatabase.cs:122` mesajı) testleri geçti sayma; ortam hatası olarak raporla ve DB'siz testlerin sonucunu ayrı belirt (`CLAUDE.md` › Bilinen ortam gerçekleri).
- Beklenen değeri ilk çalıştırmanın çıktısından değil, spesifikasyondan veya kod kuralından türet.
