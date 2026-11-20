---
title: "ADR-0008: Hibrit Kimlik Doğrulama: MVC için Cookie, /api/v1 için JWT Bearer ve Refresh Token"
status: Accepted
authority: decision
last_reviewed: "2026-11-20"
review_triggers:
  - authentication-change
  - phase-gate-change
  - secret-management-change
---

# ADR-0008: Hibrit Kimlik Doğrulama: MVC için Cookie, /api/v1 için JWT Bearer ve Refresh Token

- Tarih: 2026-11-20
- Durum: Accepted
- Karar sahipleri: Proje sahibi ([issue #6](https://github.com/otahacakirci/StockFlow/issues/6))
- Etkilenen gereksinimler: FR-AUTH-01/02, SEC-01, SEC-03, SEC-04, SEC-05, NFR-ERR-03, NFR-LOG-02, NFR-OPS-01/02, AC-06, AC-10; [product-spec.md](../product-spec.md) › 3.1 Tablo 1a (JWT satırı), › 10.1 Faz 4, › 10.1 Faz 5 ("API destekli sipariş formu"), › 10.2 BONUS-API-02, › 10.3 "JWT/API Identity … rate limiting (Faz 4)"
- Yerine geçtiği ADR: Yok
- Yerine geçen ADR: Yok

## Bağlam

- **Bugünkü kimlik doğrulama:** Tek şema vardır: Identity uygulama cookie'si.
  - `AddIdentity<ApplicationUser, IdentityRole>` varsayılan authenticate/challenge şemasını bu cookie yapar. Ayrı bir `AddAuthentication` çağrısı veya policy scheme yoktur (`StockFlow/Program.cs:32-39`).
  - Cookie ayarları: `LoginPath=/Account/Login`, `AccessDeniedPath=/Account/AccessDenied`, ad `.StockFlow.Auth`, `HttpOnly`, `SameSite=Lax`, `SecurePolicy=Always`, 8 saat sliding (`Program.cs:41-51`).
  - Global fallback policy kimliği doğrulanmış kullanıcı ister (`Program.cs:74-78`). Middleware sırası `UseAuthentication` → `UseAuthorization`'dır (`:117-118`). Tek route MVC conventional route'tur (`:123-125`).
  - `/api` altında endpoint yoktur. `StockFlow/StockFlow.csproj` `Microsoft.AspNetCore.Authentication.JwtBearer` paketini içermez.
- **Login:** Kullanıcı e-postayla bulunur ve `PasswordSignInAsync(..., lockoutOnFailure: true)` çağrılır (`StockFlow/Controllers/AccountController.cs:41-48`). Başarısız girişte genel bir mesaj verilir ve loga yalnız `TraceIdentifier` yazılır (`:56-59`). AccessDenied 403 döner (`:74-78`).
- **Roller:** `Admin` ve `Employee`, `StockFlow/Security/AppRoles.cs` sabitleridir (SEC-03). Action seviyesinde `[Authorize(Roles = …)]` ile zorlanır.
- **Identity tabloları:**
  - `AspNetUserTokens` kullanıcı + sağlayıcı + ad başına tek değer tutar. Değer düz saklanır ve FK'si `Cascade`'dir (`StockFlow/Data/Migrations/20260824065853_AddIdentitySchema.cs:140-156`).
  - Domain tarafındaki `FK_Orders_AspNetUsers_CreatedByUserId` `Restrict`'tir (`:198-204`). `.claude/rules/data.md` domain ilişkileri için `Restrict` ister.
- **Secret kalıbı:**
  - Seed değerleri User Secrets veya ortam değişkenindedir.
  - Eksik yapılandırma `IValidateOptions` + `ValidateOnStart` ile başlangıçta durdurulur (fail-fast). Mesajlar değer içermez (`Program.cs:53-56`, `:60`; `StockFlow/Options/IdentitySeedOptions.cs:22`).
  - Hijyen betiği staged içerikte, `.md` dahil, şunları reddeder:
    - dolu bir `"Key"` JSON değeri (`scripts/validate-repository-hygiene.ps1:68`)
    - `Password`/`Secret`/`Token` gibi adlara tırnaklı sabit değer atanması (`:69`)
    - `.pem/.pfx/.key` dosyaları (`:41`)
- **Test altyapısı:**
  - Yapılandırma testlerde bellek içi değerlerle verilir (`StockFlow.Tests/Integration/StockFlowWebApplicationFactory.cs:19-28`).
  - Anonim istek login'e yönlenir ve AccessDenied 403 döner (`StockFlow.Tests/Integration/MvcAcceptanceTests.cs:20-43`). Yetkisiz Employee AccessDenied'a yönlenir (`:122-126`).
- **Kapsam:** [product-spec.md](../product-spec.md) › 3.1 Tablo 1a JWT Bearer + Refresh Token'ı Faz 4'te `/api/v1` için öngörür; MVC ekranları cookie ile kalır. Faz 0, bu ADR'yi "4 mühendislik ADR'si" arasında sayar. Faz 4'ün `Açık` olabilmesi için bu ADR'nin `Accepted` olması gerekir (SCOPE-03 (b)).
- Kısıtlar:
  - Minimal API, CORS, Redis/caching ve background service kalıcı kapsam dışıdır (spec › 10.3).
  - Faz 4 API'si Controller tabanlıdır (spec › 10.1 Faz 4).
  - Testler gerçek SQL Server ister ([ADR-0002](0002-isolated-localdb-test-databases.md)).
  - Identity `StockFlow.Infrastructure`'da, Cookie/JWT auth yapılandırması `StockFlow.Web`'de durur ([ADR-0005](0005-lean-four-project-architecture.md)).
  - RowVersion ve `If-Match` Faz 4'te [ADR-0006](0006-stock-concurrency-strategy.md)'ya dayanır; bu ADR'nin konusu değildir.
  - Faz 4 `Kapalı`dır.

## Karar

1. **Şemalar.** Üç şema tanımlanır:
   - Mevcut Identity uygulama cookie'si (`IdentityConstants.ApplicationScheme`). Ayarları `Program.cs:41-51`'deki gibi kalır.
   - `JwtBearerDefaults.AuthenticationScheme` (`Bearer`).
   - Bu ikisi arasında yönlendirme yapan tek bir policy scheme (`AddPolicyScheme`; örnek ad `StockFlow.Hybrid`).

   `AddIdentity`'den sonra `DefaultScheme`, `DefaultAuthenticateScheme`, `DefaultChallengeScheme` ve `DefaultForbidScheme` policy scheme'e ayarlanır. Sign-in ve sign-out şemaları değişmez: `SignInManager` cookie'ye açıkça yazar, MVC login/logout akışı aynı kalır.
2. **Yönlendirme kuralı (path tabanlı).**
   - `ForwardDefaultSelector`, istek yolu `/api` segmentiyle başlıyorsa (`PathString.StartsWithSegments("/api")`) `Bearer`'ı seçer. Diğer bütün yollar cookie'ye gider.
   - **Route değişmezi:** Seçici yalnız yol önekine baktığı için:
     - Bütün API controller'ları yalnız `/api/v1` altında route edilir.
     - `/api` altında MVC route'u bulunmaz.

     `/api` dışına route edilen bir API controller'ı cookie şemasına düşer. O durumda CSRF ve 302 → login davranışı API sözleşmesine sızar.
   - Seçim header'a bakmaz:
     - `/api/**` yalnız Bearer ile doğrulanır. Cookie taşıyan ama Bearer taşımayan bir `/api` isteği anonimdir. Bu yüzden API yüzeyinde CSRF yoktur ve API uçları antiforgery istemez.
     - MVC yolları yalnız cookie ile doğrulanır. Yalnız Bearer header'ı taşıyan bir MVC isteği anonim sayılır ve login'e yönlendirilir.
   - Challenge ve forbid aynı seçiciyle yönlenir. `/api` altında kimliksiz istek `401` (`WWW-Authenticate: Bearer`), yetkisiz istek `403` alır; `/Account/Login` veya `/Account/AccessDenied` yönlendirmesi olmaz. MVC tarafında SEC-01 yönlendirmesi ve AccessDenied davranışı aynen kalır.
   - Global fallback policy iki yüzeyde de geçerlidir. Hata gövdesinin biçimi (ProblemDetails) Faz 4 kapsamıdır.
   - API'nin kullanıcıları tarayıcı dışı istemcilerdir. Tarayıcıdan cross-origin çağrı desteklenmez (CORS kalıcı kapsam dışı). Tarayıcı JavaScript'ine JWT verilmez.
3. **Token ömürleri.** Değerler yapılandırılabilir ve secret değildir:
   - Access token (JWT): 15 dakika.
   - Refresh token: 7 gün. Her rotasyonda yeni token yeniden 7 gün alır.
   - Refresh ailesi (tek girişten türeyen zincir) için mutlak üst sınır 30 gündür. Sonrasında kimlik bilgisiyle yeniden giriş gerekir.
   - Doğrulamada `ClockSkew` 30 saniyedir. Çerçeve varsayılanı 5 dakika olduğu için açıkça ayarlanır.
   - MVC cookie'sinin 8 saatlik sliding ayarı değişmez.
   - Değerler varsayılanlarıyla bir options sınıfında tutulur. Değiştirmek `appsettings*.json` değişikliği ister ve insan onayına bağlıdır.
4. **Refresh token saklama ve rotasyon.**
   - **Biçim:** Refresh token JWT değildir. `RandomNumberGenerator` ile üretilen 32 baytlık (256 bit) opak bir değerdir ve base64url ile kodlanır. İstemciye yalnız bir kez, token yanıtının gövdesinde verilir; cookie'ye konmaz.
   - **Saklama:** Veritabanında yalnız SHA-256 özeti tutulur; düz değer hiçbir yerde saklanmaz. Faz 4'te ayrı bir `RefreshTokens` tablosu eklenir. Asgari alanlar:
     - Id
     - `UserId` → `AspNetUsers`
     - benzersiz `TokenHash`
     - `FamilyId`
     - `CreatedAtUtc`, `ExpiresAtUtc`, `FamilyExpiresAtUtc`
     - `ConsumedAtUtc`, `ReplacedByTokenId`
     - `RevokedAtUtc`, `RevocationReason`
     - aile açıldığındaki `SecurityStamp` anlık görüntüsü

     Refresh token, kullanıcıya ait bir kimlik eseridir; Identity destek tabloları ona emsaldir. Kesin şema ve `AspNetUsers` FK silme davranışı Faz 4 tasarımında belirlenir. `Restrict` dışında bir davranış seçilirse bu `.claude/rules/data.md` için açık bir istisna olarak kaydedilir.

     `AspNetUserTokens` kullanılmaz, çünkü kullanıcı/sağlayıcı/ad başına tek değer tuttuğu için çoklu istemciyi ve token ailesini temsil edemez.
   - **Rotasyon:** Her refresh isteği sunulan token'ı tüketir ve aynı ailede yeni bir token verir.
     - Tüketme koşullu bir güncellemeyle yapılır: `ConsumedAtUtc IS NULL AND RevokedAtUtc IS NULL` koşulu ve etkilenen satır sayısının 1 olması.
     - Eski token'ın tüketilmesi ile yenisinin eklenmesi tek transaction'dadır.
     - Eşzamanlı iki refresh'ten yalnız biri başarılı olur.
     - Aile iptali ile rotasyon aynı aile kaydı üzerinde serileştirilir. Örnek: ayrı bir aile satırının `RevokedAtUtc` alanı koşullu güncellenir ve rotasyon transaction'ı bu satırı kilitler. İptal edilmiş bir aileye yeni token eklenemez. Kilit sırası, [ADR-0006](0006-stock-concurrency-strategy.md)'daki gibi sabit tutulur: önce aile, sonra token. Kesin mekanizma Faz 4 tasarımında belirlenir.
   - **Yeniden kullanım tespiti:** Tüketilmiş veya iptal edilmiş bir token sunulursa bu bir sızıntı belirtisi sayılır. Ailenin bütün token'ları iptal edilir ve `401` döner. Tolerans penceresi yoktur.
   - **Refresh kontrolleri:** Her refresh'te üç şey kontrol edilir:
     - Kullanıcı var mı?
     - Kullanıcı kilitli mi (`IsLockedOutAsync`)?
     - Güncel `SecurityStamp` ailenin anlık görüntüsüyle aynı mı?

     Biri başarısızsa aile iptal edilir ve `401` döner. Roller her refresh'te veritabanından yeniden okunur, eski token'dan kopyalanmaz.
5. **İptal.**
   - (a) `POST /api/v1/auth/revoke` (API logout) sunulan refresh token'ın ailesini iptal eder. Uç RFC 7009 tarzında anonimdir:
     - Sahiplik kanıtı refresh token'ın kendisidir.
     - Bilinmeyen, süresi dolmuş veya zaten iptal edilmiş token için de aynı başarılı yanıt döner. Böylece uç, token'ın var olup olmadığını sızdırmaz.
     - Access token'ı süresi dolmuş istemci de önce refresh yapmadan çıkış yapabilir.
   - (b) Parola değişimi gibi `SecurityStamp`'i değiştiren olaylar, Karar 4'teki kontrol sayesinde kullanıcının bütün ailelerini bir sonraki refresh'te geçersiz kılar.
     - Identity, rol ekleme/çıkarma (`AddToRoleAsync`/`RemoveFromRoleAsync`) ve lockout ayarlama (`SetLockoutEndDateAsync`) çağrılarında stamp'i kendiliğinden yenilemez.
     - Bu yüzden rol değiştiren veya hesabı kilitleyen kod aynı işlemde `UserManager.UpdateSecurityStampAsync` çağırır.
     - Lockout ayrıca `IsLockedOutAsync` kontrolüyle de yakalanır.
   - (c) **Access token tek tek iptal edilmez.**
     - Denylist yoktur, çünkü Redis ve caching kalıcı kapsam dışıdır.
     - İstek başına `SecurityStamp` sorgusu da yapılmaz; bunun yerine kısa ömür kullanılır.
     - Kabul edilen risk: bir değişiklikten sonra access token en çok 15 dakika (+ `ClockSkew`) geçerli kalabilir.
   - (d) Süresi dolmuş veya iptal edilmiş satırlar background service ile temizlenmez (kalıcı kapsam dışı). Temizlik kullanıcının token işlemleri sırasında satır içi yapılır.
     - Temizlik yalnız `FamilyExpiresAtUtc`'si geçmiş veya iptal edilmiş ailelerin satırlarını siler.
     - Aktif bir ailenin tüketilmiş satırları silinmez. Silinirse aynı token'ın yeniden sunulması "bilinmeyen token" olarak görülür ve yeniden kullanım tespiti kaybolur.
     - Ayrıntı Faz 4 tasarımına kalır.
6. **İmzalama anahtarı ve secret yönetimi.**
   - **Algoritma:** HS256 (simetrik). Token'ı aynı uygulama hem üretir hem doğrular. `ValidAlgorithms` yalnız HS256 içerir; `none` ve algoritma karışıklığı reddedilir.
   - **Anahtar:**
     - En az 32 baytlık rastgele bir değerin base64 kodlamasıdır.
     - Yapılandırma anahtarı `Jwt:Key`, ortam değişkeni karşılığı `Jwt__Key`'dir.
     - Ad bilinçli seçilmiştir: mevcut `PopulatedJwtKeyJson` hijyen kuralı (`scripts/validate-repository-hygiene.ps1:68`), iç içe JSON biçimindeki (`"Jwt": { "Key": … }`) dolu bir `Key` değerini yakalar.
     - Kural düz `"Jwt:Key"` JSON anahtarını ve C# sabitlerini yakalamaz. `:69`'daki ad listesi `Key` içermez. Faz 4'te kural bu biçimleri de kapsayacak şekilde insan onayıyla genişletilir.
   - **Nerede durur:** Değer yalnız User Secrets'ta (geliştirme) veya ortam değişkeninde/host secret deposunda bulunur. `appsettings*.json`, kaynak kod, README örneği, test kodu sabiti veya logda bulunmaz (SEC-04, NFR-LOG-02, `.claude/rules/security.md` › Secret ve yapılandırma).
   - **Fail-fast:** Anahtar Development dahil her ortamda zorunludur.
     - Eksikse, base64 değilse veya 32 bayttan kısaysa uygulama başlangıçta durur.
     - Mekanizma `IdentitySeedOptions` kalıbıyla aynıdır: `IValidateOptions` + `ValidateOnStart`.
     - Hata mesajı anahtar değerini içermez.
     - Bedeli: kurulumda beşinci secret.
   - **Secret olmayan değerler:** `Jwt:Issuer` ve `Jwt:Audience` secret değildir. Faz 4'te `appsettings.json`'a insan onayıyla girebilirler. Boşlarsa aynı validator uygulamayı başlangıçta durdurur. Aksi hâlde uygulama başlar ama her Bearer isteği çalışma anında `401` alır.
   - **README:** Faz 4 README'si anahtarın üretimini mevcut `Read-Host`/değişken + `Remove-Variable` kalıbıyla anlatır. Değer ekrana yazdırılmaz.
   - **Testler:** Anahtar her test çalıştırmasında `RandomNumberGenerator` ile üretilir ve bellek içi yapılandırmayla verilir (`StockFlowWebApplicationFactory.cs:19-28` kalıbı). Test kodunda sabit anahtar bulunmaz.
   - **Anahtar rotasyonu:**
     - Anahtar değiştirildiğinde eski anahtarla imzalı access token'lar en geç 15 dakika içinde `401` almaya başlar.
     - Refresh token'lar JWT olmadığı için etkilenmez; istemci refresh ile yeni token alır.
     - `kid` ile çoklu anahtar desteği yoktur. Gerekirse yeni bir ADR ile eklenir.
   - **Dosya yok:** `.pem`, `.pfx` ve `.key` dosyaları repoya girmez.
7. **Rol ve claim taşıma.**
   - **Access token claim'leri:** `sub` (kullanıcı Id), `jti`, `iat`, `exp`, `iss`, `aud` ve kullanıcının her rolü için bir `role` claim'i. Rol değerleri yalnız `AppRoles` sabitleridir (SEC-03).
   - **Kişisel veri yok:** E-posta, kullanıcı adı veya başka kişisel veri token'a konmaz. JWT gövdesi okunabilir olduğu için veri en aza indirilir.
   - **Doğrulama tarafı:**
     - `MapInboundClaims` kapalı.
     - `TokenValidationParameters` içinde rol claim tipi `role`, ad claim tipi `sub`.
     - `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime` ve `ValidateIssuerSigningKey` açık.
     - `RequireExpirationTime` ve `RequireSignedTokens` açık.

     Böylece aynı `[Authorize(Roles = AppRoles.Admin)]` özniteliği iki şemada da aynı sonucu verir.
   - **Yetki matrisi:** API uçlarına [product-spec.md](../product-spec.md) › 4 Tablo 3 rol matrisi aynen uygulanır. Ayrı bir "API erişimi" izni veya rolü yoktur.
   - **Rol güncelliği:** Token'daki roller en çok bir access ömrü boyunca bayat kalabilir. Refresh rolleri yeniden okur.
8. **Token uçları (Faz 4, Controller tabanlı).** Minimal API ve `MapIdentityApi` kullanılmaz. Uçlar:
   - `POST /api/v1/auth/token`: e-posta + parola → access + refresh.
   - `POST /api/v1/auth/refresh`
   - `POST /api/v1/auth/revoke`

   Kurallar:
   - `token`, `refresh` ve `revoke` uçları gerekçeli `[AllowAnonymous]` alır (`.claude/rules/security.md` › HTTP sınırı). `revoke` için gerekçe Karar 5(a)'dadır.
   - Parola `SignInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)` ile kontrol edilir. Bu çağrı cookie üretmez ve MVC login'deki lockout güvencesini korur.
   - Başarısızlıkta "kullanıcı yok", "parola hatalı" ve "kilitli" durumları ayırt edilmez; hepsi aynı `401` gövdesini alır (SEC-05).
   - Üç anonim uç da Faz 4 rate limiting kapsamında sınırlandırılır.
   - **Loglama:** Token, refresh token, `Authorization` header'ı ve parola loglanmaz. Loga yalnız kullanıcı Id, `FamilyId`, işlem adı ve `TraceIdentifier` yazılır (NFR-LOG-02).
   - Development dışında `JwtBearerOptions.IncludeErrorDetails` kapalıdır (NFR-ERR-03, SEC-05).
9. **Zamanlama.** Karar Faz 0'da kaydedilir. **Bu ADR Faz 4'ü açmaz; Faz 4 `Kapalı` kalır.** Madde 1-8 kodda yalnız [product-spec.md](../product-spec.md) › 10.1'de Faz 4 `Açık` olduğunda ve insan onaylı PR ile uygulanır. Bugün şunlar değişmez:
   - `Program.cs`
   - csproj (JwtBearer paketi eklenmez)
   - `appsettings*.json`
   - veritabanı şeması
   - `.claude/rules/*`
   - `docs/product-spec.md` (› 10.2 BONUS-API-02 dahil)

   FR-AUTH-01/02 ve SEC-01 bugünkü metinleriyle bağlayıcı kalır.

## Değerlendirilen alternatifler

- **Header tabanlı seçici** (`Authorization: Bearer` varsa JWT, yoksa cookie; `/api` iki şemayı da kabul eder): Seçilmedi.
  - Cookie ile çağrılabilen `/api` yazma uçları CSRF yüzeyi açar ve API'de antiforgery gerektirir.
  - Bir yolun hangi şemayla korunduğu istekten isteğe değişir.

  Tarayıcıdan API çağrısı ihtiyacı doğarsa yeni bir ADR ile yeniden değerlendirilir.
- **Tek şema, API'de de cookie:** Seçilmedi. Tarayıcı dışı istemciler için uygun değildir. CSRF ve 302 → login yönlendirmesi API sözleşmesine sızar. Spec › 3.1 Tablo 1a `/api/v1` için JWT öngörür.
- **ASP.NET Core Identity API endpoints** (`MapIdentityApi` + `BearerToken` şeması): Seçilmedi.
  - Minimal API kalıcı kapsam dışıdır.
  - Token'lar JWT değil, Data Protection ile korunan opak değerlerdir; spec JWT der.
  - Refresh token'lar sunucuda saklanmadığı için tek tek iptal edilemez ve yeniden kullanım tespit edilemez.
- **Refresh token'ı JWT olarak üretmek veya düz metin saklamak:** Seçilmedi. JWT biçimli bir refresh token, sunucu tarafında iptal edilebilmek için yine depolama ister. Düz metin saklama, veritabanı sızıntısında oturum çalınmasına izin verir.
- **`AspNetUserTokens` tablosunu kullanmak:** Seçilmedi. Kullanıcı + sağlayıcı + ad başına tek satır tuttuğu için çoklu istemciyi, aileyi ve tüketim zincirini temsil edemez. Ayrıca değeri düz saklar.
- **Asimetrik imza (RS256/ES256):** Seçilmedi.
  - Token'ı doğrulayan başka bir servis yoktur (mikroservis kalıcı kapsam dışı).
  - Anahtar dosyası/sertifika yönetimi ek yük getirir ve `.pem/.pfx` dosyaları repoya giremez.

  Dış bir doğrulayıcı ihtiyacı doğarsa yeni bir ADR ile değerlendirilir.
- **Harici kimlik sağlayıcı (IdentityServer/Duende, OpenIddict, Entra ID):** Seçilmedi.
  - Spec › 3.1 kimlik doğrulamayı yerel Identity hesaplarıyla sabitler.
  - Yeni bir altyapı bileşeni ve lisans/işletim yükü getirir.
  - Spec'in "teknoloji mevcut problemi çözmeli" ilkesine aykırıdır.
- **Access token denylist'i veya istek başına `SecurityStamp` doğrulaması:** Seçilmedi. Denylist paylaşılan bir cache ister (kalıcı kapsam dışı). İstek başına veritabanı sorgusu ise kısa ömürlü token'ın amacını ortadan kaldırır.
- **Refresh yeniden kullanımında tolerans penceresi:** Seçilmedi. Eşzamanlı meşru refresh'i affeder ama yeniden kullanım tespitini zayıflatır ve tasarımı karmaşıklaştırır.

## Sonuçlar

- Olumlu:
  - MVC akışı ve mevcut testleri değişmez (SCOPE-02).
  - API yüzeyinde CSRF ve 302 yönlendirmesi yoktur.
  - Rol kararı iki yüzeyde aynı `AppRoles` özniteliğiyle verilir.
  - Refresh token sızıntısının etkisi rotasyon ve yeniden kullanım tespitiyle sınırlanır.
  - İmzalama anahtarının değişmesi refresh token'ları geçersiz kılmaz.
  - Mevcut hijyen kuralı iç içe JSON biçimindeki dolu `Key` değerini yakalar. Düz `Jwt:Key` JSON anahtarı ve C# sabitleri için kural Faz 4'te genişletilir (Karar 6).
- Maliyetler ve kabul edilen riskler:
  - Access token en çok 15 dakika (+ `ClockSkew`) iptal edilemez.
  - Faz 4'te yeni bir tablo ve migration gerekir.
  - Yeni zorunlu secret (`Jwt:Key`) temiz kurulum adımlarına (NFR-OPS-01, AC-10) eklenir.
  - Eşzamanlı iki meşru refresh'te ikinci istek aileyi iptal eder ve istemci yeniden giriş yapmak zorunda kalır.
- Faz 4 açıldığında güncellenecek belgeler. Bunlar onaylanmış iş değildir; Faz 4 tasarımında kesinleşir:
  - `.claude/rules/security.md` › HTTP sınırı: "Her POST action `[ValidateAntiForgeryToken]` taşır" maddesi Bearer ile korunan API POST'larında uygulanamaz. Madde cookie ile kimliği doğrulanan MVC POST'larıyla sınırlanır (`/curate-knowledge`).
  - API için yeni bir `.claude/rules/` dosyası.
  - README › İlk kurulum: beşinci secret.
  - `docs/database-schema.md`: `RefreshTokens` tablosu.
  - [product-spec.md](../product-spec.md) › 10.2: BONUS-API-02 bugün API'de JWT olmamasını söyler; Faz 4'ü açan PR bu maddeyi yeniden yazar.
- Faz 5 etkisi:
  - [product-spec.md](../product-spec.md) › 10.1 Faz 5 yalnız "API destekli sipariş formu" der. `docs/roadmap.md` › Faz 5 bunu tarayıcıdan `/api/v1` çağrısı olarak somutlaştırır. Bu karar tarayıcıdan `/api/v1` çağrısına doğrudan izin vermez.
  - Bu ADR spec maddesini daraltmaz.
  - Faz 5'i açan PR maddeyi iki yoldan biriyle karşılar: cookie + antiforgery ile korunan, aynı origin'deki MVC JSON action'ları ya da bu ADR'nin yerine geçen ve `/api` seçicisini değiştiren yeni bir ADR.

## Doğrulama

- Bugün:
  - Bu ADR'deki bağlantılar çözülür ve `docs/adr/README.md` indeksi günceldir.
  - `node .claude/hooks/agentic/test-guard.mjs` ve repo hijyeni betiği geçer.
  - Kod, şema, paket, yapılandırma ve test değişmez.
- Faz 4. Bu senaryolar Faz 4 tasarımının test planına girer ve gerçek HTTP ile SQL Server üzerinde koşar:
  1. Korumalı bir `/api/v1` ucuna token'sız istek → `401`, `Location` header'ı yok.
  2. Geçerli cookie ile, Bearer olmadan `/api/v1` isteği → `401`.
  3. Yalnız Bearer ile MVC sayfası isteği → login'e `302`.
  4. Employee token'ıyla Admin'e özel API ucu → `403`.
  5. Süresi geçmiş, imzası bozuk, `alg=none` veya yanlış `aud`/`iss` taşıyan token → `401`.
  6. Refresh rotasyonu: eski token ikinci kez kullanılınca `401` döner ve aile iptal edilir.
  7. Eşzamanlı iki refresh'ten tam biri başarılı olur.
  8. Parola değişiminden, rol değişiminden veya Admin lockout'undan sonra refresh → `401`. Rol ve lockout değişiminde stamp'in yenilendiği de doğrulanır (Karar 5(b)).
  9. Token ucunda art arda hatalı parola lockout'u tetikler; yanıt kullanıcının var olup olmadığını sızdırmaz.
  10. `Jwt:Key` eksik veya kısaysa uygulama başlangıçta durur; mesajda değer yoktur.
  11. Mevcut `MvcAcceptanceTests` değişmeden yeşil kalır.
  12. Tüketilmiş bir token'ın yeniden kullanımı ile aynı ailede eşzamanlı meşru rotasyon → işlem sonunda ailede geçerli token kalmaz.
  13. `[ApiController]` taşıyan her endpoint'in route şablonu `api/` ile başlar (Karar 2, route değişmezi).
  14. `revoke` ucu geçerli, bilinmeyen ve iptal edilmiş token için aynı yanıtı verir; geçerli token'ın ailesi iptal edilir.
