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

- Veritabanı gerektiren test `SqlServerTestDatabase` altyapısını kullanır: Service/Data testleri `SqlServerDatabaseTestBase`'den, MVC uçtan uca testleri `MvcIntegrationTestBase`'den türer (`StockFlow.Tests/Integration/StockFlowWebApplicationFactory.cs:34`). Her test, koşu başına bir kez başlatılan Testcontainers MSSQL konteynerinde (`StockFlow.Tests/Infrastructure/MsSqlTestServer.cs:8`, `:11-13`) benzersiz `StockFlow_Tests_<guid>` oluşturur, migration uygular ve sonunda siler (`StockFlow.Tests/Infrastructure/SqlServerTestDatabase.cs:13`, `:121-178`, `:272-277`; [ADR-0009](../../docs/adr/0009-testcontainers-mssql-test-databases.md), ADR-0002'nin yerine geçer).
- Hedef doğrulaması yalnız bu koşunun konteyner uç noktasını ve `StockFlow_Tests_` + GUID adını kabul eder (`SqlServerTestDatabase.cs:51-66`); bağlantı yalnız koşu başına üretilen geçici `sa` kimliğini kullanır ve pooling kapalıdır (`SqlServerTestDatabase.cs:226-239`, `MsSqlTestServer.cs:71-75`). Bu doğrulamaları gevşetme; parolayı veya connection string'i mesaja/loga yazma.
- Veritabanı oluşturma statik semafor altında sıraya alınır, migration paralel kalır (`SqlServerTestDatabase.cs:143-155`); xUnit sınıflar arası paralelliğini kapatan koleksiyon fixture'ı ekleme.
- Veritabanına bağlanmaması gereken testler `SqlServerTestDatabase.AddUninitializedDbContext` kullanır; Docker başlatmaz (`SqlServerTestDatabase.cs:68-99`, kullanım `StockFlow.Tests/Data/IdentityDataSeederConfigurationTests.cs`).
- EF Core InMemory, SQLite, LocalDB veya geliştirme veritabanına geri dönüş yok; Docker yoksa test atlanmaz. Test altyapısı uygulama bağlantı ayarlarını veya User Secrets'ı okumaz. LocalDB yalnız uygulamanın geliştirme veritabanıdır.
- Saf doğrulama/planner/policy testleri veritabanı oluşturmaz (`StockFlow.Tests/Services/OrderStockConfirmationPlannerTests.cs`).
- MVC uçtan uca testleri `StockFlowWebApplicationFactory` ile gerçek pipeline ve Identity cookie kullanır (`StockFlow.Tests/Integration/StockFlowWebApplicationFactory.cs:9`).

## Çalıştırma ve raporlama

- Önce değiştirilen davranışa en yakın testi çalıştır: `dotnet test StockFlow.Tests/StockFlow.Tests.csproj --no-restore --filter "FullyQualifiedName~<Ad>Tests"`; sonra done gate.
- Davranış değişikliğinde testi önce yaz, doğru nedenle kırmızı olduğunu göster, sonra yeşile getir; raporda iki çıktıyı da ver.
- Docker yoksa veya test konteyneri başlamazsa ("SQL Server test konteyneri başlatılamadı…", `MsSqlTestServer.cs:22-29`) ya da test veritabanı oluşturulamazsa ("SQL Server test veritabanı başlatılamadı…", `SqlServerTestDatabase.cs:169-174`) testleri geçti sayma; ortam hatası olarak raporla ve DB'siz testlerin sonucunu ayrı belirt (`CLAUDE.md` › Bilinen ortam gerçekleri). Başlatma hatası koşu boyunca önbelleğe alınır (`MsSqlTestServer.cs:11-13`); sonraki DB testleri aynı hatayla hızlıca düşer.
- Beklenen değeri ilk çalıştırmanın çıktısından değil, spesifikasyondan veya kod kuralından türet.
