---
title: "StockFlow Mevcut Teknik Durum"
status: current
authority: descriptive
last_reviewed: "2026-08-24"
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

- `StockFlow.slnx` tek bir `StockFlow/StockFlow.csproj` projesi içerir.
- Proje `net10.0`, nullable reference types ve implicit usings kullanır.
- Uygulama ASP.NET Core MVC/Razor ile EF Core altyapısını aynı host içinde barındırır.
- Paket envanterinde EF Core 10 SQL Server provider ve design-time paketi bulunur; geçici JWT, IdentityModel ve Swagger paketleri kaldırılmıştır.
- Yerel araç manifesti `dotnet-ef` 10.0.11 sürümünü sabitler; ASP.NET Core Identity ve xUnit henüz eklenmemiştir.

## Kalıcılık temeli

- `ApplicationDbContext`; Category, Product, Customer, Supplier, Order, OrderItem ve StockMovement için yedi `DbSet` ve yedi fiziksel domain tablosunu yönetir. Sekizinci çekirdek entity olan ApplicationUser, kapsam kararı gereği Identity aşamasına bırakılmıştır.
- Kalıcı entity tipleri geçici API modellerinden ayrı `StockFlow.Entities` namespace'inde tutulur.
- Fluent API mapping'leri alan uzunluklarını, `decimal(18,2)` hassasiyetini, enum değerlerini, foreign key'leri, `Restrict` silme davranışını, indeksleri ve check constraint'leri tanımlar.
- `InitialDomainSchema` migration'ı ve model snapshot'ı üretilmiştir. Şema ve ilişkiler [veritabanı şeması belgesinde](../database-schema.md) açıklanır.
- `Program.cs`, `ConnectionStrings:DefaultConnection` yoksa güvenli ve açıklayıcı hata üreterek fail-fast davranır; gerçek bağlantı dizesi kaynak kontrollü ayarlarda tutulmaz.
- Geliştirme bağlantısı User Secrets üzerinden `(localdb)\MSSQLLocalDB` hedefindeki `StockFlow` veritabanına yönelir; Windows tümleşik kimlik doğrulaması kullanır ve parola içermez.
- `20260818105705_InitialDomainSchema` migration'ı LocalDB'ye uygulanmıştır. Canlı veritabanında yedi domain tablosu ve `__EFMigrationsHistory` bulunur; 19 Ağustos 2026 doğrulamasında model drift'i veya `DBCC CHECKDB` hatası görülmemiştir.

## Çalışan uygulama davranışı

- `Program.cs` yalnızca MVC/Razor ve `ApplicationDbContext` kayıtlarını yapar; Swagger, JWT ve API controller routing kaydı yoktur.
- Geçici `/api/IdentityVerification/login` ve `/api/Products` uçları, statik kullanıcı/ürün koleksiyonları ve düz metin demo parolaları kaldırılmıştır.
- Cookie tabanlı ASP.NET Core Identity henüz uygulanmadığı için uygulama şu anda anonim MVC + EF temelidir.
- `HomeController` ve varsayılan Razor şablon sayfaları dışında hedef yönetim ekranları yoktur.
- Service katmanı ve güvenli ViewModel akışı henüz yoktur; test projesi de bulunmaz.
- Kaynak kontrollü ayarlarda bağlantı dizesi veya kimlik doğrulama anahtarı yoktur. LocalDB bağlantısı User Secrets içinde kalır.
- `.gitignore`, `.gitattributes` ve staged içeriği denetleyen repository hygiene betiği GitHub öncesi güvenlik tabanını oluşturur.

## Doğrulanmış build taban çizgisi

24 Ağustos 2026 tarihinde temiz/rebuild derlemesi (`dotnet build StockFlow.slnx --no-restore -t:Rebuild`) sonucu:

- 0 hata
- 0 uyarı

## Hedefle önemli farklar

| Alan | Mevcut | Hedef |
| --- | --- | --- |
| Kimlik | Kimlik doğrulama henüz yok | Cookie tabanlı ASP.NET Core Identity, Admin/Employee seed |
| Veri | Domain entity/mapping/migration hazır ve LocalDB'ye uygulanmış; uygulama akışı henüz veritabanını kullanmıyor | EF Core 10 + SQL Server + Service üzerinden kalıcı veri akışı |
| Uygulama akışı | Yalnız varsayılan MVC akışı | İnce Controller, Service iş kuralları, ViewModel sınırı |
| Arayüz | Varsayılan Razor sayfaları | MVC yönetim ekranları; API yalnızca bonus salt-okunur kapsam |
| Domain | Kalıcı çekirdek domain entity'leri hazır | Identity ile ApplicationUser dahil tamamlanmış domain |
| Test | Test projesi yok | Kritik sipariş/stok kurallarını kanıtlayan izole xUnit testleri |

## Bilinen riskler ve boşluklar

- Cookie tabanlı Identity ve rol yetkilendirmesi henüz uygulanmamıştır.
- Service katmanı, güvenli ViewModel sınırı ve yönetim ekranları henüz yoktur.
- Kritik sipariş/stok kurallarını doğrulayan test projesi bulunmamaktadır.
- LocalDB geliştirme için uygundur fakat production veya çok kullanıcılı deployment hedefi değildir.

Hassas değerleri belgelere veya yanıtlara kopyalamayın. Repository hygiene denetimi GitHub'a gönderim öncesinde çalıştırılmalıdır.
