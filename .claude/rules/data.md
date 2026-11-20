---
paths:
  - "StockFlow/Data/**"
  - "StockFlow/Entities/**"
  - "StockFlow/Options/**"
  - "StockFlow.Tests/Data/**"
---

# Veri katmanı (Entity, mapping, migration, seeder)

Şema bütünlüğünü veritabanında ve EF modelinde birlikte korur; ayrıntı `docs/database-schema.md`.

## Entity ve mapping

- Entity'ler `StockFlow.Entities` namespace'inde kalıcı veri şeklidir; kullanıcı girdisi veya View modeli değildir — `CLAUDE.md` › Değişmez proje sınırları.
- Sayısal invariant'lar (`docs/product-spec.md` › 6.2): `Product.Sku` benzersiz, `Price > 0`, `StockQuantity >= 0`, `MinimumStockQuantity >= 0`, `OrderItem.Quantity > 0`, `StockMovement.Quantity > 0` (yön `StockIn`/`StockOut` türünden); `Order.TotalAmount` sunucuda hesaplanır. Veritabanında check constraint olarak da korunur.
- Mapping her entity için ayrı `IEntityTypeConfiguration` dosyasında Fluent API ile yapılır (`StockFlow/Data/Configurations/`), data annotation ile değil.
- İlişkiler `DeleteBehavior.Restrict` (`StockFlow/Data/Configurations/OrderConfiguration.cs:63`), para alanları `HasPrecision(18, 2)` (`OrderConfiguration.cs:39`), enum/aralık invariant'ları SQL check constraint (`OrderConfiguration.cs:13-19`).
- Alan uzunlukları ViewModel DataAnnotations ve Service doğrulamasıyla aynı kalmalıdır; birini değiştirirsen üçünü birlikte değiştir.

## Migration

- Migration yalnız `dotnet ef migrations add <Ad> --project StockFlow/StockFlow.csproj --startup-project StockFlow/StockFlow.csproj` ile üretilir; `*.Designer.cs` ve `ApplicationDbContextModelSnapshot.cs` elle düzenlenmez.
- Uygulama migration'ı otomatik uygulamaz (README › Migration ve çalıştırma). `dotnet ef database update|drop` ve `dotnet ef migrations remove` geliştirme veritabanını değiştirir; insan onayı ister.
- Mevcut zincir: `InitialDomainSchema`, `AddIdentitySchema` (`StockFlow/Data/Migrations/`). Model değişikliği `docs/product-spec.md` › 6, `docs/database-schema.md` ve ilgili ADR'nin gözden geçirilmesini ister (`.claude/rules/editing-discipline.md` › Dokümantasyon).

## Seeder

- `IdentityDataSeeder` idempotenttir; mevcut hesabın parolasını değiştirmez, hesap silmez (README › Admin ve Employee hesapları).
- `DemoDataSeeder` yalnız `DemoData:Enabled=true` ve Development ortamında çalışır (`StockFlow/Data/DemoDataSeeder.cs:23`, `:29`); yedi domain tablosundan biri doluysa hiçbir kaydı değiştirmeden atlar (`DemoDataSeeder.cs:107-113`); bütün grafiği tek serializable transaction'da yazar (`DemoDataSeeder.cs:60`). Demo veri `HasData` ile modele konmaz.
- Demo katalog kişi/şirket adları kurgusal, iletişim bilgileri güvenli örnek değerlerdir.

## Testler

- Veritabanına dokunan Data testleri `SqlServerDatabaseTestBase`'den türer (izole `SqlServerTestDatabase`); uygulama bağlantı ayarlarını okumaz; veritabanı Testcontainers MSSQL test konteynerindedir ([ADR-0009](../../docs/adr/0009-testcontainers-mssql-test-databases.md), ayrıntı `.claude/rules/testing.md` › Veritabanı). Seeder kapıları için veritabanısız test: `StockFlow.Tests/Data/DemoDataSeederGateTests.cs`.
