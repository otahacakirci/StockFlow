---
title: "StockFlow Mevcut Teknik Durum"
status: current
authority: descriptive
last_reviewed: "2026-08-26"
review_triggers:
  - application-code-change
  - package-change
  - build-baseline-change
  - authentication-change
  - persistence-change
---

# StockFlow Mevcut Teknik Durum

Bu belge bugünkü kod tabanının açıklayıcı anlık görüntüsüdür. Karar vermeden önce kodu ve testleri doğrudan inceleyin. Hedef sözleşme için [product-spec.md](../product-spec.md) kullanılır.

## Çözüm envanteri

- `StockFlow.slnx`, uygulama için `StockFlow/StockFlow.csproj` ve testler için `StockFlow.Tests/StockFlow.Tests.csproj` projelerini içerir.
- Proje `net10.0`, nullable reference types ve implicit usings kullanır.
- Uygulama ASP.NET Core MVC/Razor ile EF Core altyapısını aynı host içinde barındırır.
- Paket envanterinde EF Core 10 SQL Server provider/design-time paketleri ve ASP.NET Core Identity EF store paketi bulunur; geçici JWT, IdentityModel ve Swagger paketleri yoktur.
- Yerel araç manifesti `dotnet-ef` 10.0.11 sürümünü sabitler. `StockFlow.Tests` projesi xUnit 2.9.3 ve EF Core SQL Server provider 10.0.11 kullanır.

## Kalıcılık temeli

- `ApplicationDbContext`, `IdentityDbContext<ApplicationUser>` tabanından türetilir; yedi domain tablosunu ve yedi standart Identity tablosunu tek EF Core modelinde yönetir.
- Kalıcı entity tipleri geçici API modellerinden ayrı `StockFlow.Entities` namespace'inde tutulur.
- Fluent API mapping'leri alan uzunluklarını, `decimal(18,2)` hassasiyetini, enum değerlerini, foreign key'leri, `Restrict` silme davranışını, indeksleri ve check constraint'leri tanımlar.
- `InitialDomainSchema` ve ikinci `AddIdentitySchema` migration'ı üretilmiştir. İkinci migration Identity tablolarını ve nullable `Orders.CreatedByUserId -> AspNetUsers.Id` Restrict foreign key'ini ekler.
- `Program.cs`, `ConnectionStrings:DefaultConnection` yoksa güvenli ve açıklayıcı hata üreterek fail-fast davranır; gerçek bağlantı dizesi kaynak kontrollü ayarlarda tutulmaz.
- Geliştirme bağlantısı User Secrets üzerinden `(localdb)\MSSQLLocalDB` hedefindeki `StockFlow` veritabanına yönelir; Windows tümleşik kimlik doğrulaması kullanır ve parola içermez.
- İki migration da LocalDB geliştirme veritabanına uygulanmıştır. 24 Ağustos 2026 doğrulamasında EF bekleyen model değişikliği bildirmemiştir.

## Çalışan uygulama davranışı

- `Program.cs`; MVC/Razor, `ApplicationDbContext`, cookie tabanlı Identity, global authenticated fallback policy ve başlangıç seeder'ını kaydeder; Swagger, JWT ve API controller routing kaydı yoktur.
- Geçici `/api/IdentityVerification/login` ve `/api/Products` uçları, statik kullanıcı/ürün koleksiyonları ve düz metin demo parolaları kaldırılmıştır.
- Özel MVC `AccountController` login/logout akışı vardır. Login ve hata uçlarıyla statik dosyalar anonim; diğer MVC endpoint'leri varsayılan olarak kimlik doğrulaması gerektirir.
- Identity cookie'si HttpOnly, Secure, SameSite=Lax, sekiz saatlik kayan süre ve `.StockFlow.Auth` adıyla yapılandırılmıştır. `UseAuthentication`, routing ile authorization arasında çalışır.
- `Admin` ve `Employee` rol adları tek sabit kaynaktadır. Başlangıç kullanıcıları User Secrets/ortam yapılandırmasından idempotent seed edilir; eksik/geçersiz ayar uygulamayı hassas değer göstermeden durdurur.
- `HomeController` ve varsayılan Razor şablon sayfaları dışında hedef yönetim ekranları yoktur.
- `IOrderService`/`OrderService` ve güvenli Draft giriş modelleri eklenmiştir. Service; Sale/Purchase taraf doğrulamasını, zorunlu audit kullanıcısını, 32 karakterlik sunucu sipariş numarasını, yeni kalemlerde fiyat snapshot'ını, mevcut Draft satırlarında snapshot korumasını ve toplam hesabını yönetir.
- `OrderService` dış sözleşmesini ve davranışını değiştirmeden kısa orchestration metotları kullanır; Draft doğrulama/satır eşitleme ve transaction dışı kalıcılaştırma hata temizliği özel yardımcı metotlarda ayrıştırılmıştır. Bağımlılıksız `internal` `OrderStockConfirmationPlanner`, Sale/Purchase yeni stok değerlerini entity mutation'dan önce hesaplar; planın tracked entity'lere ve hareketlere uygulanması ile transaction sınırı `OrderService` içinde kalır.
- Draft oluşturma/düzenleme stok değiştirmez. Confirm; bütün stok kontrollerinden sonra stokları, `StockMovement` kayıtlarını ve terminal `Confirmed` durumunu açık SQL transaction ve tek `SaveChangesAsync` sınırında yazar. Cancel stok hareketi üretmeden terminaldir; yalnız hareket geçmişi olmayan Draft sipariş fiziksel silinir.
- Beklenen Service hataları `Validation`, `NotFound` ve `BusinessRule` kategorili tipli sonuçlarla ayrılır; beklenmeyen persistence hataları yapılandırılmış loglanır, transaction geri alınır ve yeniden fırlatılır.
- Yönetim Controller ve Razor ekranları henüz yoktur. Login formu ayrı, doğrulamalı bir ViewModel kullanır.
- Kaynak kontrollü ayarlarda bağlantı dizesi, başlangıç e-postası veya parola yoktur. LocalDB ve Identity seed değerleri güvenli yapılandırmada kalır.
- Yirmi iki xUnit testi bulunur. On ilişkisel `OrderService` testi Draft create/update, korunan-yeni-kaldırılan satırlarla snapshot/total, Purchase/Sale confirm, yetersiz stok atomikliği, SaveChanges sonrası rollback, cancel, Draft delete, iki terminal durum ve kategorili hata sonuçlarını kapsar. Dört LocalDB'siz planner testi Sale/Purchase başarı kararlarını, yetersiz stok bağlamını ve Purchase taşmasını doğrular.
- Veritabanına dokunan her test, yalnız `(localdb)\MSSQLLocalDB` üzerinde benzersiz `StockFlow_Tests_<guid>` veritabanı oluşturur, migration uygular ve test sonunda veritabanını siler. Test altyapısı uygulama yapılandırmasını veya dış bağlantı dizesini kabul etmez.
- `.gitignore`, `.gitattributes` ve staged içeriği denetleyen repository hygiene betiği GitHub öncesi güvenlik tabanını oluşturur.

## Doğrulanmış build taban çizgisi

26 Ağustos 2026 tarihinde solution derlemesi (`dotnet build StockFlow.slnx --no-restore`) sonucu:

- 0 hata
- 0 uyarı

## Hedefle önemli farklar

| Alan | Mevcut | Hedef |
| --- | --- | --- |
| Kimlik | Cookie Identity, özel MVC login/logout, global auth fallback ve idempotent Admin/Employee seed hazır | İş ekranlarında rol matrisi ve role göre navigasyon |
| Veri | Domain entity/mapping/migration ve sipariş/stok Service kalıcılık akışı hazır | Diğer yönetim Service'leri ve Controller akışları |
| Uygulama akışı | Sipariş/stok iş kuralları Service ve ViewModel sınırında hazır; varsayılan MVC ekranları Service'i çağırmıyor | İnce Controller ve yönetim ekranlarının Service'lere bağlanması |
| Arayüz | Varsayılan Razor sayfaları | MVC yönetim ekranları; API yalnızca bonus salt-okunur kapsam |
| Domain | Sipariş yaşam döngüsü, stok hareketleri ve CreatedBy audit bağı Service akışında kullanılıyor | Kalan Category/Product/Customer/Supplier yönetim akışları |
| Test | SQL Server hedefli sekiz altyapı/Identity ve on sipariş/stok Service testi var | LocalDB kullanılabilir ortamda yeni ilişkisel paketin tam başarılı koşusunun alınması |

## Bilinen riskler ve boşluklar

- İş ekranları bulunmadığı için Admin/Employee rol matrisi henüz controller/action ve navigasyon seviyesinde uygulanmamıştır.
- Uygulama çalışmadan önce dört `IdentitySeed` secret değeri ve migration uygulanmış veritabanı zorunludur; uygulama otomatik migration çalıştırmaz.
- Sipariş/stok Service katmanı hazır olsa da Controller, rol matrisi ve yönetim ekranlarına henüz bağlanmamıştır.
- 26 Ağustos 2026 doğrulamasında `MSSQLLocalDB` otomatik örneği başlatılamadığı için ilişkisel testlerin çalışma zamanı sonucu alınamamıştır; test keşfi yirmi iki testi başarıyla listeler, LocalDB gerektirmeyen dokuz test geçer ve çözüm 0 hata/uyarıyla derlenir.
- İlişkisel test altyapısı Windows ve kurulu SQL Server LocalDB gerektirir; CI ve çapraz platform test hedefi henüz yoktur.
- LocalDB geliştirme için uygundur fakat production veya çok kullanıcılı deployment hedefi değildir.

Hassas değerleri belgelere veya yanıtlara kopyalamayın. Repository hygiene denetimi GitHub'a gönderim öncesinde çalıştırılmalıdır.
