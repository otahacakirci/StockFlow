# İnceleme kontrol listesi — StockFlow

Yalnız dosyaları değişen bölümleri uygula. Her madde kaynağı olan kurala atıf yapar; kaynağı olmayan madde buraya ait değildir.

## A. Web katmanı (`StockFlow/Controllers/**`, `Views/**`, `ViewModels/**`, `ModelBinding/**`, `wwwroot/{js,css}/**`)

| # | Madde | Kural |
| --- | --- | --- |
| A1 | Controller `ApplicationDbContext` kullanmıyor; yalnız Service arayüzleri | `.claude/rules/web-layer.md` › Controller |
| A2 | Sınıf/action `[Authorize(Roles = …)]` README rol matrisiyle birebir uyumlu (Admin'e özel action'lar ek Admin özniteliği taşıyor) | `.claude/rules/web-layer.md` › Controller |
| A3 | Her POST `[ValidateAntiForgeryToken]`; mutasyon GET ile yapılmıyor | `.claude/rules/security.md` › HTTP sınırı |
| A4 | `ServiceResult` eşlemesi: Validation → form, NotFound → 404, BusinessRule → 409, diğer → loglu güvenli 500 | `.claude/rules/web-layer.md` › Controller |
| A5 | `returnUrl` yalnız `Url.IsLocalUrl` sonrası kullanılıyor | `.claude/rules/security.md` › HTTP sınırı |
| A6 | Entity giriş/çıkış modeli değil; POST modeli sunucu hesaplı alan (`UnitPrice`, `TotalAmount`, `StockQuantity`) taşımıyor | `.claude/rules/web-layer.md` › ViewModel |
| A7 | UI rol görünürlüğü action yetkisiyle tutarlı; Türkçe metin, `tr-TR` para, açık UTC tarih | `.claude/rules/web-layer.md` › Razor ve istemci |
| A8 | `wwwroot/lib/**` elle değiştirilmemiş | `.claude/rules/editing-discipline.md` › Dosyalar |

## B. Service katmanı (`StockFlow/Services/**`)

| # | Madde | Kural |
| --- | --- | --- |
| B1 | Beklenen hatalar `ServiceResult` + kararlı `<domain>.<kod>` sabitiyle; mevcut public sabit kaldırılmamış | `.claude/rules/services.md` › Sözleşme |
| B2 | Okuma sorguları `AsNoTracking` + projection; sayfalama `ListPagingPolicy`; whitelist sıralama | `.claude/rules/services.md` › Sorgular |
| B3 | Transaction dışı kayıt `TrackedPersistence` üzerinden | `.claude/rules/services.md` › Yazma |
| B4 | Rol kontrolü Service'e girmemiş; spesifikasyonda olmayan benzersizlik kuralı yok | `.claude/rules/services.md` › Sözleşme, Yazma |
| B5 | Yeni Service `Program.cs`'te kayıtlı (onaylı değişiklik olarak) | `.claude/rules/services.md` › Sözleşme |
| B6 | İletişim/kişisel veri loglanmıyor | `.claude/rules/security.md` › Hata ve log |

## C. Sipariş ve stok (`StockFlow/Services/Orders/**`, `StockMovements/**`, ilgili Controller/ViewModel/Entity)

| # | Madde | Kural |
| --- | --- | --- |
| C1 | Draft yolu stok/StockMovement değiştirmiyor | `.claude/rules/orders-stock.md` › Değişmezler |
| C2 | Confirm tek transaction + tek `SaveChangesAsync`; ihlalde rollback | `.claude/rules/orders-stock.md` › Değişmezler |
| C3 | Uygulama kodunda stok mutasyonu ve StockMovement oluşturma yalnız `OrderService` confirm akışında (demo katalog hariç) | `.claude/rules/orders-stock.md` › Değişmezler |
| C4 | Confirmed/Cancelled terminal; fiyat snapshot'ı korunuyor | `.claude/rules/orders-stock.md` › Değişmezler |
| C5 | Davranış değiştiyse `docs/product-spec.md` › 7 güncellemesi kullanıcı yetkisiyle mevcut | `.claude/rules/orders-stock.md` › Test beklentisi |

## D. Veri katmanı (`StockFlow/Data/**`, `Entities/**`, `Options/**`)

| # | Madde | Kural |
| --- | --- | --- |
| D1 | Fluent mapping: `Restrict`, `HasPrecision(18, 2)`, check constraint; uzunluklar ViewModel/Service ile aynı | `.claude/rules/data.md` › Entity ve mapping |
| D2 | Migration `dotnet ef` ile üretilmiş; Designer/Snapshot elle düzenlenmemiş | `.claude/rules/data.md` › Migration |
| D3 | Seeder kapıları (Development + bayrak + boş domain) ve idempotency korunmuş | `.claude/rules/data.md` › Seeder |
| D4 | Şema değiştiyse `docs/database-schema.md` güncel | `.claude/rules/data.md` › Migration |

## E. Testler (`StockFlow.Tests/**`)

| # | Madde | Kural |
| --- | --- | --- |
| E1 | DB testleri izole `SqlServerTestDatabase` altyapısıyla (`SqlServerDatabaseTestBase` veya `MvcIntegrationTestBase`); InMemory/geliştirme DB'si yok | `.claude/rules/testing.md` › Veritabanı |
| E2 | Adlandırma `<Metot>_<Koşul>_<Beklenti>`, yerleşim kaynak ağacını yansıtıyor | `.claude/rules/testing.md` › Yerleşim ve adlandırma |
| E3 | Her assertion konu hiçbir şey yapmasa kırılır; beklenen değer spesifikasyondan | `.claude/rules/testing.md` › Çalıştırma ve raporlama |

## Z. Her değişiklik

| # | Madde | Kural |
| --- | --- | --- |
| Z1 | Değişiklik tasarım/istek kapsamıyla uyumlu; ilgisiz refactor veya biçimlendirme yok | `.claude/rules/editing-discipline.md` › Kapsam |
| Z2 | Davranış değişiklikleri için testler eklenmiş/güncellenmiş, red → green kanıtıyla | `.claude/rules/testing.md` › Çalıştırma ve raporlama |
| Z3 | Üretilmiş, vendor veya secret dosyası elle düzenlenmemiş; onaysız silme yok; secret/bağlantı dizesi yok | `CLAUDE.md` › Asla yapma, `.claude/rules/security.md` |
| Z4 | Build + test geçiyor; `dotnet format --verify-no-changes` temiz | `CLAUDE.md` › Komutlar |
| Z5 | Dokümantasyon tablosu uygulanmış (README, `docs/database-schema.md`, ADR, ilgili kural) | `.claude/rules/editing-discipline.md` › Dokümantasyon |
| Z6 | Kalıcı kapsam dışı bileşen eklenmemiş; faz kapılı bileşen yalnız fazı `Açık` ise eklenmiş (paket, katman, framework) | `CLAUDE.md` › Değişmez proje sınırları, `docs/product-spec.md` › 10.1 |
