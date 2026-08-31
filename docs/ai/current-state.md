---
title: "StockFlow Mevcut Teknik Durum"
status: current
authority: descriptive
last_reviewed: "2026-08-31"
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
- `ICategoryService`/`CategoryService` ve güvenli giriş, sorgu ve çıkış ViewModel'leri eklenmiştir. Service; ad doğrulama/trim, ad araması, whitelist sıralama, 20 varsayılan ve 100 üst sınırlı normalize sayfalama, `AsNoTracking` projection, bağlı ürün sayısı, detay, oluşturma ve düzenleme akışlarını yönetir.
- Category fiziksel silme işlemi yalnız bağlı `Product` yoksa yapılır. Eksik kayıt ve bağlı ürün ihlali sırasıyla `NotFound` ve `BusinessRule` sonucu üretir; ad benzersizliği ürün sözleşmesinde veya veritabanında bulunmadığı için ek bir duplicate-name kuralı uygulanmaz.
- `IProductService`/`ProductService` ve ayrı create/update giriş modelleri eklenmiştir. Service; ad/SKU araması, kategori ve düşük stok filtresi, whitelist sıralama, normalize sayfalama, kategori projection'ı, detay, doğrulamalı create/update ve geçmiş korumalı delete akışlarını yönetir.
- Product create nonnegative başlangıç stoğunu ilk durum olarak kabul eder ve hareket üretmez. Update modeli `StockQuantity` taşımaz; Service mevcut stoğu ve geçmiş `OrderItem.UnitPrice` snapshot'larını korur. SKU benzersizliği uygulama sorgusu ve mevcut `UX_Products_Sku` constraint'iyle iki katmanda korunur.
- Product fiziksel silme işlemi herhangi bir `OrderItem` veya `StockMovement` varsa `BusinessRule` ile reddedilir. Geçmişsiz ürün için ek sıfır stok şartı uygulanmaz.
- `ICustomerService`/`CustomerService` ve güvenli giriş, sorgu, seçim ve çıkış ViewModel'leri eklenmiştir. Service; ad/e-posta/telefon araması, whitelist ad sıralaması, normalize sayfalama, `AsNoTracking` projection, sipariş sayılı detay, doğrulamalı create/update ve yalnız `Id`/`Name` taşıyan sipariş formu seçim verisini yönetir.
- Customer girişinde ad zorunlu; e-posta, telefon ve adres opsiyoneldir. Bütün değerler trimlenir, boş opsiyonel alanlar `null` yapılır; ViewModel ve Service doğrulaması mapping ile aynı 150/256/32/500 karakter sınırlarını, e-posta ve telefon biçimlerini korur. E-posta veya telefon için ürün sözleşmesinde ve veritabanında bulunmayan benzersizlik kuralı eklenmez.
- Customer fiziksel silme işlemi herhangi bir Draft, Confirmed veya Cancelled `Order` varsa `BusinessRule` ile reddedilir. Eksik kayıt `NotFound`, geçersiz giriş `Validation` üretir; beklenmeyen kalıcılaştırma hataları kişisel veri loglanmadan yapılandırılmış bağlamla kaydedilir, tracker temizlenir ve hata yeniden fırlatılır.
- `ISupplierService`/`SupplierService` ve güvenli giriş, sorgu, seçim ve çıkış ViewModel'leri eklenmiştir. Service; şirket adı/e-posta/telefon araması, whitelist şirket adı sıralaması, normalize sayfalama, `AsNoTracking` projection, sipariş sayılı detay, doğrulamalı create/update ve yalnız `Id`/`CompanyName` taşıyan Purchase sipariş formu seçim verisini yönetir.
- Supplier girişinde şirket adı zorunlu; e-posta, telefon ve adres opsiyoneldir. Bütün değerler trimlenir, boş opsiyonel alanlar `null` yapılır; ViewModel ve Service doğrulaması mapping ile aynı 200/256/32/500 karakter sınırlarını, e-posta ve telefon biçimlerini korur. Şirket adı, e-posta veya telefon için ürün sözleşmesinde ve veritabanında bulunmayan benzersizlik kuralı eklenmez.
- Supplier fiziksel silme işlemi herhangi bir Draft, Confirmed veya Cancelled Purchase `Order` varsa `BusinessRule` ile reddedilir. Admin rol sınırı HTTP/endpoint katmanında uygulanmak üzere Service'e taşınmaz. Eksik kayıt `NotFound`, geçersiz giriş `Validation` üretir; beklenmeyen kalıcılaştırma hataları iletişim verisi loglanmadan kaydedilir, tracker temizlenir ve hata yeniden fırlatılır.
- `IOrderService`/`OrderService` ve güvenli Draft giriş modelleri eklenmiştir. Service; Sale/Purchase taraf doğrulamasını, zorunlu audit kullanıcısını, 32 karakterlik sunucu sipariş numarasını, yeni kalemlerde fiyat snapshot'ını, mevcut Draft satırlarında snapshot korumasını ve toplam hesabını yönetir.
- `OrderService` dış sözleşmesini ve davranışını değiştirmeden kısa orchestration metotları kullanır; Draft doğrulama/satır eşitleme ve transaction dışı kalıcılaştırma hata temizliği özel yardımcı metotlarda ayrıştırılmıştır. Bağımlılıksız `internal` `OrderStockConfirmationPlanner`, Sale/Purchase yeni stok değerlerini entity mutation'dan önce hesaplar; planın tracked entity'lere ve hareketlere uygulanması ile transaction sınırı `OrderService` içinde kalır.
- Draft oluşturma/düzenleme stok değiştirmez. Confirm; bütün stok kontrollerinden sonra stokları, `StockMovement` kayıtlarını ve terminal `Confirmed` durumunu açık SQL transaction ve tek `SaveChangesAsync` sınırında yazar. Cancel stok hareketi üretmeden terminaldir; yalnız hareket geçmişi olmayan Draft sipariş fiziksel silinir.
- `IOrderQueryService`/`OrderQueryService`, mevcut `IOrderService` mutasyon sözleşmesini değiştirmeden siparişlerin salt-okunur ekran sorgularını ayrı bir Service sorumluluğunda toplar. Tür/durum filtreli liste; whitelist tarih sıralaması, 20 varsayılan/100 üst sınırlı normalize sayfalama, kararlı kimlik tie-breaker'ı, taraf adı ve kalem sayısı projection'ı üretir.
- Sipariş detay ve Draft edit sorguları Customer/Supplier tarafını ve OrderItem/Product kimlik, ad, SKU, miktar ve fiyat snapshot verilerini güvenli ViewModel'lerle taşır. Bütün sorgular `AsNoTracking` projection kullanır ve kalıcı veri değiştirmez; eksik sipariş `NotFound`, terminal sipariş için edit sorgusu `BusinessRule` üretir. Customer/Supplier seçim listeleri ilgili mevcut Service'lerde kalır.
- `IStockMovementQueryService`/`StockMovementQueryService`, stok hareketlerini Product/Order kimliği, StockIn/StockOut türü ve inclusive UTC takvim günü aralığıyla filtreler; whitelist tarih sıralaması ve 20 varsayılan/100 üst sınırlı normalize sayfalama uygular. Ters tarih aralığı `Validation`, eksik detay `NotFound` üretir.
- StockMovement liste ve detay sonuçları Product adı/SKU'su, OrderNumber, pozitif miktar, açıklama ve sıfır UTC offset'li hareket tarihini güvenli ViewModel'lere project eder. Sorgular `AsNoTracking` kullanır ve kalıcı veri değiştirmez; StockMovement oluşturma ile Product stok mutation'ı yalnız mevcut `OrderService` confirm transaction'ında kalır.
- Beklenen Service hataları `Validation`, `NotFound` ve `BusinessRule` kategorili tipli sonuçlarla ayrılır. Beklenmeyen persistence hataları yapılandırılmış loglanıp ilgili change tracker temizlendikten sonra yeniden fırlatılır; sipariş onayı ayrıca açık transaction'ı geri alır.
- Yönetim Controller ve Razor ekranları henüz yoktur. Login formu ayrı, doğrulamalı bir ViewModel kullanır.
- Kaynak kontrollü ayarlarda bağlantı dizesi, başlangıç e-postası veya parola yoktur. LocalDB ve Identity seed değerleri güvenli yapılandırmada kalır.
- Seksen üç xUnit testi bulunur. Yedi ilişkisel `StockMovementQueryService` testi ilişki/tür/tarih filtrelerini, inclusive UTC gün sınırlarını, kararlı sıralama ve normalize sayfalamayı, detay/hata sonuçlarını ve salt-okunur davranışı kapsar. Sekiz `OrderQueryService`, on üç `SupplierService`, on üç `CustomerService`, on iki `ProductService`, sekiz `CategoryService`, on `OrderService`, dört saf planner ve sekiz altyapı/Identity testi diğer mevcut davranışları doğrular.
- Veritabanına dokunan her test, yalnız `(localdb)\MSSQLLocalDB` üzerinde benzersiz `StockFlow_Tests_<guid>` veritabanı oluşturur, migration uygular ve test sonunda veritabanını siler. Test altyapısı uygulama yapılandırmasını veya dış bağlantı dizesini kabul etmez.
- `.gitignore`, `.gitattributes` ve staged içeriği denetleyen repository hygiene betiği GitHub öncesi güvenlik tabanını oluşturur.

## Doğrulanmış build taban çizgisi

31 Ağustos 2026 tarihinde solution derlemesi (`dotnet build StockFlow.slnx --no-restore`) sonucu:

- 0 hata
- 0 uyarı

## Hedefle önemli farklar

| Alan | Mevcut | Hedef |
| --- | --- | --- |
| Kimlik | Cookie Identity, özel MVC login/logout, global auth fallback ve idempotent Admin/Employee seed hazır | İş ekranlarında rol matrisi ve role göre navigasyon |
| Veri | Domain entity/mapping/migration, Category/Product/Customer/Supplier yönetimi, sipariş mutation/query ve StockMovement query Service akışları hazır | Controller akışları ve kalan DashboardService |
| Uygulama akışı | Category/Product/Customer/Supplier yönetimi, sipariş mutation/query ve StockMovement görüntüleme davranışları Service/ViewModel sınırında hazır; varsayılan MVC ekranları Service'leri çağırmıyor | İnce Controller ve yönetim ekranlarının Service'lere bağlanması |
| Arayüz | Varsayılan Razor sayfaları | MVC yönetim ekranları; API yalnızca bonus salt-okunur kapsam |
| Domain | Category/Product/Customer/Supplier yönetimi, sipariş yaşam döngüsü, sipariş ve stok hareketi görüntüleme sorguları ile CreatedBy audit bağı Service akışında kullanılıyor | Dashboard sorguları |

## Bilinen riskler ve boşluklar

- İş ekranları bulunmadığı için Admin/Employee rol matrisi henüz controller/action ve navigasyon seviyesinde uygulanmamıştır.
- Uygulama çalışmadan önce dört `IdentitySeed` secret değeri ve migration uygulanmış veritabanı zorunludur; uygulama otomatik migration çalıştırmaz.
- Category, Product, Customer, Supplier, sipariş mutation/query ve StockMovement query Service katmanları hazır olsa da Controller, rol matrisi ve yönetim ekranlarına henüz bağlanmamıştır.
- 31 Ağustos 2026 doğrulamasında hedefli yedi `StockMovementQueryService` testi, StockMovement sorguları ile OrderService/planner kapsamındaki yirmi bir test ve seksen üç testlik tam paket kullanıcıya bağlı `MSSQLLocalDB` örneğinde geçti; başarısız veya atlanan test yoktur. Çözüm ayrıca 0 hata/uyarıyla derlenir.
- İlişkisel test altyapısı Windows ve kurulu SQL Server LocalDB gerektirir; CI ve çapraz platform test hedefi henüz yoktur.
- LocalDB geliştirme için uygundur fakat production veya çok kullanıcılı deployment hedefi değildir.

Hassas değerleri belgelere veya yanıtlara kopyalamayın. Repository hygiene denetimi GitHub'a gönderim öncesinde çalıştırılmalıdır.
