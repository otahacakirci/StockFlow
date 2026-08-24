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
- Paket envanterinde EF Core 10 SQL Server provider/design-time paketleri ve ASP.NET Core Identity EF store paketi bulunur; geçici JWT, IdentityModel ve Swagger paketleri yoktur.
- Yerel araç manifesti `dotnet-ef` 10.0.11 sürümünü sabitler. `StockFlow.Tests` projesi xUnit 2.9.3 ve EF Core InMemory 10.0.11 kullanır.

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
- Service katmanı ve yönetim ekranlarına ait ViewModel akışları henüz yoktur. Login formu ayrı, doğrulamalı bir ViewModel kullanır.
- Kaynak kontrollü ayarlarda bağlantı dizesi, başlangıç e-postası veya parola yoktur. LocalDB ve Identity seed değerleri güvenli yapılandırmada kalır.
- Üç izole xUnit testi seed tekrarının kopya üretmediğini, mevcut kullanıcıya eksik rolü eklediğini ve eksik yapılandırmanın veritabanı erişiminden önce fail-fast olduğunu doğrular.
- `.gitignore`, `.gitattributes` ve staged içeriği denetleyen repository hygiene betiği GitHub öncesi güvenlik tabanını oluşturur.

## Doğrulanmış build taban çizgisi

24 Ağustos 2026 tarihinde solution derlemesi (`dotnet build StockFlow.slnx --no-restore`) sonucu:

- 0 hata
- 0 uyarı

## Hedefle önemli farklar

| Alan | Mevcut | Hedef |
| --- | --- | --- |
| Kimlik | Cookie Identity, özel MVC login/logout, global auth fallback ve idempotent Admin/Employee seed hazır | İş ekranlarında rol matrisi ve role göre navigasyon |
| Veri | Domain entity/mapping/migration hazır ve LocalDB'ye uygulanmış; uygulama akışı henüz veritabanını kullanmıyor | EF Core 10 + SQL Server + Service üzerinden kalıcı veri akışı |
| Uygulama akışı | Yalnız varsayılan MVC akışı | İnce Controller, Service iş kuralları, ViewModel sınırı |
| Arayüz | Varsayılan Razor sayfaları | MVC yönetim ekranları; API yalnızca bonus salt-okunur kapsam |
| Domain | ApplicationUser dahil çekirdek kalıcı entity'ler hazır | Service tabanlı iş akışlarında kullanıcı/audit bağının kullanılması |
| Test | Identity seed için üç izole xUnit testi var | Kritik sipariş/stok kurallarını kanıtlayan ek izole xUnit testleri |

## Bilinen riskler ve boşluklar

- İş ekranları bulunmadığı için Admin/Employee rol matrisi henüz controller/action ve navigasyon seviyesinde uygulanmamıştır.
- Uygulama çalışmadan önce dört `IdentitySeed` secret değeri ve migration uygulanmış veritabanı zorunludur; uygulama otomatik migration çalıştırmaz.
- Service katmanı, güvenli ViewModel sınırı ve yönetim ekranları henüz yoktur.
- Kritik sipariş/stok kurallarını doğrulayan testler henüz bulunmamaktadır.
- LocalDB geliştirme için uygundur fakat production veya çok kullanıcılı deployment hedefi değildir.

Hassas değerleri belgelere veya yanıtlara kopyalamayın. Repository hygiene denetimi GitHub'a gönderim öncesinde çalıştırılmalıdır.
