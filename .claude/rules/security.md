# Güvenlik

Secret ve kişisel verinin kaynak koda, belgeye veya loga sızmasını önler; StockFlow'un güvenlik tabanı bu kurallara dayanır (`docs/product-spec.md` › 8.2 Hata yönetimi ve logging).

## Secret ve yapılandırma

- Bağlantı dizesi ve Identity seed değerleri yalnız User Secrets veya ortam değişkenindedir (`ConnectionStrings:DefaultConnection`, `IdentitySeed:*`); kaynak kontrollü `appsettings*.json` bunları içermez — README › Güvenli bağlantı yapılandırması.
- User Secrets içeriğini okuma veya listeleme; değer gerekiyorsa kullanıcıdan kendi terminalinde ayarlamasını iste (README'deki `Read-Host` kalıbı).
- Eksik yapılandırma güvenli ve hassas değer içermeyen mesajla fail-fast olur (`StockFlow/Program.cs:26`, `StockFlow/Options/IdentitySeedOptions.cs`); bu davranışı zayıflatma.
- Örnek e-postalarda yalnız güvenli örnek domainleri kullan (`example.com`, testlerde `stockflow.test` — `StockFlow.Tests/Integration/StockFlowWebApplicationFactory.cs:12`).

## HTTP sınırı

- Global authenticated fallback policy açıktır (`StockFlow/Program.cs:74-78`); yeni anonim endpoint yalnız açık gerekçeyle `[AllowAnonymous]` alır.
- Her POST action `[ValidateAntiForgeryToken]` taşır (`StockFlow/Controllers/CategoriesController.cs:57`).
- `returnUrl` yalnız `Url.IsLocalUrl` doğrulamasından sonra kullanılır (`StockFlow/Controllers/CategoriesController.cs:217`, `StockFlow/Controllers/AccountController.cs:82`).
- Rol kararları UI'da gizlemeye ek olarak controller/action seviyesinde `[Authorize(Roles = …)]` ile zorlanır; rol adları yalnız `StockFlow/Security/AppRoles.cs` üzerinden.

## Hata ve log

- Kullanıcıya güvenli Türkçe mesaj, loga yapılandırılmış teknik bağlam (`StockFlow/Controllers/CategoriesController.cs:236-247`); production'da stack trace sızdırma.
- Customer/Supplier iletişim bilgilerini (e-posta, telefon, adres) loglama; loga yalnız kimlik ve işlem adı yaz (`StockFlow/Services/Customers/CustomerService.cs:177-179`, `:200-204`).

## Commit öncesi

- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-repository-hygiene.ps1` staged içeriği yasaklı yollar ve secret desenleri için tarar; eşleşen değeri terminale yazdırma.
