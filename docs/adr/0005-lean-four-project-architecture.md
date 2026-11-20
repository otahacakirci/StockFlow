---
title: "ADR-0005: Yalın 4 Projeli Modüler Monolit ve Bilinçli Olarak Eklenmeyen Soyutlamalar"
status: Accepted
authority: decision
last_reviewed: "2026-11-20"
review_triggers:
  - architecture-change
  - phase-gate-change
---

# ADR-0005: Yalın 4 Projeli Modüler Monolit ve Bilinçli Olarak Eklenmeyen Soyutlamalar

- Tarih: 2026-11-20
- Durum: Accepted
- Karar sahipleri: Proje sahibi (issue #3)
- Etkilenen gereksinimler: ARCH-01, ARCH-02, SCOPE-01..04; `docs/product-spec.md` › 3.2, › 10.1 Faz 2, › 10.3
- Yerine geçtiği ADR: Yok
- Yerine geçen ADR: Yok

## Bağlam

StockFlow bugün tek bir ASP.NET Core MVC projesidir (`StockFlow/StockFlow.csproj`); testler ayrı projededir (`StockFlow.Tests`). Controller'lar `ApplicationDbContext` kullanmaz; bu kural bugün kodda sağlanır ama **derleyici veya test tarafından zorlanmaz**, yalnız disiplinle korunur.

Disiplinin yetmediğinin somut kanıtı: 16 Service dosyası `StockFlow.ViewModels.*` tiplerini kullanır, yani sunum katmanına ait giriş modelleri iş katmanına sızmıştır (ör. `StockFlow/ViewModels/Products/ProductCreateInputModel.cs`). Ayrıca `StockFlow/Services/Orders/OrderService.cs` 950 satırdır ve iş kuralları entity'lerin dışında, Service'te toplanır (`docs/roadmap.md` › 4).

Asıl kazanım proje sayısı değil, **bağımlılık yönünün derleyici ve testle zorlanmasıdır**. Aynı zamanda ekip tek kişidir, süreç tek, veritabanı tek ve sipariş onayı tek atomik transaction'dır (`docs/product-spec.md` › 7); bu yüzden fazladan soyutlama maliyeti de gerçektir.

## Karar

1. **Hedef yapı:** Dört proje: `StockFlow.Domain`, `StockFlow.Application`, `StockFlow.Infrastructure`, `StockFlow.Web`. Bağımlılık yönü:

   ```mermaid
   flowchart TD
       Web["StockFlow.Web (Razor UI + REST API v1 + Cookie/JWT Auth + OpenAPI)"] --> App["StockFlow.Application (Use-Case Services + DTOs + Validation + Ports)"]
       Web --> Infra["StockFlow.Infrastructure (EF Core + Migrations + Identity + Seeders)"]
       Infra --> App
       Infra --> Domain["StockFlow.Domain (Rich Entities + Value Objects + Stock & Cost Rules)"]
       App --> Domain
       Tests["StockFlow.Tests (Domain + Concurrency + Architecture + API/MVC Integration)"] --> Web
       Tests --> Domain
   ```

   Diyagram [roadmap](../roadmap.md) › 5 "Hedef Mimari" ile aynıdır; roadmap normatif değildir ve değişirse bu ADR bağımsız kalır. Diyagramdaki REST API v1, JWT ve OpenAPI öğeleri hedef yöndür; Faz 4 kapılıdır (`docs/product-spec.md` › 10.1) ve bu ADR onları açmaz.

2. **Kurallar:**
   - `Domain` hiçbir projeye ve dış NuGet paketine bağlı değildir.
   - `Application` ASP.NET Core MVC'ye bağlı olmaz; sunum tipleri (MVC `ViewModel`/input modelleri) `Web`'de kalır, `Application` kendi DTO'larını kullanır.
   - `Web` → `Infrastructure` referansı yalnız composition root (DI kaydı) içindir; Controller'lar `ApplicationDbContext` kullanmaz.
   - `Application`'ın veriye erişimi gerekirse **generic olmayan, use-case'e özel portlarla** olur; somut port imzaları bu ADR'de uydurulmaz, Faz 2 tasarımında belirlenir.
   - Test projesinin referans ayrıntısı ve NetArchTest kural listesi Faz 2 tasarımına bırakılır.

3. **Zorlama:** Proje referansları (derleyici) ve Faz 2'de NetArchTest mimari testleri. İkisi birbirini tamamlar: referans grafiği yönü yasaklar, NetArchTest ise isim alanı ve tip düzeyindeki ihlalleri (ör. `Application` içinde `Microsoft.AspNetCore.Mvc` kullanımı) yakalar.

4. **Bilinçli olarak eklenmeyenler:** MediatR, CQRS ayrımı, generic Repository / Unit of Work, AutoMapper ve mikroservis. `ApplicationDbContext` zaten unit-of-work ve repository işlevini görür. Bu bileşenlerin tamamı `docs/product-spec.md` › 10.3'te **kalıcı kapsam dışıdır**; bu ADR o sözleşmeyi değiştirmez, gerekçesini kaydeder. Dörtten fazla proje ise 10.3 listesinde değil, bu ADR'nin kendi kararıdır (proje sayısı dörtte tutulur).

5. **Zamanlama ve kapsam:** Karar Faz 0'da kaydedilir. **Bu ADR Faz 2'yi açmaz; Faz 2 `Kapalı` kalır.** 4 projeli yapı, kodda ve `CLAUDE.md` › Mimari özet tablosunda yalnız `docs/product-spec.md` › 10.1 Faz 2 `Açık` olduğunda ve insan onaylı PR ile kurulur. Bugünkü tek proje yapısı o zamana kadar geçerlidir.

6. **Korunan sözleşmeler:** `ServiceResult`, `TimeProvider`, atomik sipariş onayı (spec › 7) ve InMemory yasağı ([ADR-0002](0002-isolated-localdb-test-databases.md)) taşıma sırasında değişmez.

## Değerlendirilen alternatifler

- **Tek proje + klasör disiplini (bugünkü durum):** Sıfır geçiş maliyeti, en az yapı. Seçilmedi: bağımlılık yönünü derleyici zorlayamaz; ViewModel sızıntısı bunun bugünkü kanıtıdır. Faz 2 `Kapalı` olduğu sürece bugünkü yapı geçerli kalır.
- **MediatR:** İstek/handler hattı ve pipeline davranışları (loglama, doğrulama) kazandırırdı. Seçilmedi: tek süreçli, tek veritabanlı ve az sayıda use-case'li uygulamada dolaylılık ve "handler arama" maliyeti kazancı aşar; Service sınıfları zaten use-case sınırıdır.
- **CQRS ayrımı (ayrı komut/sorgu modeli ve handler hattı):** Okuma ve yazma modellerini bağımsız ölçeklerdi. Seçilmedi: ayrı okuma modeli ihtiyacı yok; listeler ve dashboard zaten doğrudan EF projeksiyonudur. Ayrım yalnız kod miktarını ve eşleme yükünü artırır.
- **Generic Repository / Unit of Work:** Veri erişimini soyutlar, EF'e bağımlılığı gizlerdi. Seçilmedi: `DbContext` zaten bu işlevleri görür; sarmalayıcı `Include`, projeksiyon ve transaction gibi EF yeteneklerini gizler veya sızdırır ve test edilebilirliği artırmaz, çünkü testler gerçek SQL Server (LocalDB) kullanır (ADR-0002).
- **AutoMapper:** Eşleme kodunu azaltırdı. Seçilmedi: eşleme çalışma zamanına kayar, hatalar derleme zamanında görünmez; açık projeksiyon ve elle eşleme derleyiciyle doğrulanır ve stok/maliyet gibi hassas alanlarda gizli kural riskini kaldırır.
- **Mikroservis:** Bağımsız dağıtım ve ölçekleme sağlardı. Seçilmedi: tek ekip, tek veritabanı ve stok + hareket + durumu tek transaction'da değiştiren sipariş onayı, dağıtık tutarlılık ve işletme maliyetini haklı çıkarmaz.
- **Dörtten fazla proje (tam Clean/Onion):** Daha ince katmanlar sağlardı. Seçilmedi: bu ölçekte faydası belirsiz, taşıma ve gezinme maliyeti yüksektir.

## Sonuçlar

- Bağımlılık yönü derleyici ve NetArchTest ile zorlanır; `Domain` testi altyapısız yazılabilir; `Application` MVC'den bağımsız olur.
- Faz 2 geçişi büyük bir taşıma işidir. Aynı değişiklik kümesinde `CLAUDE.md` mimari tablosu ve rol kapsamları, `.claude/hooks/agentic/policy.json`, `.claude/rules/*` yol kalıpları ve ilgili belgeler güncellenir ([ADR-0003](0003-claude-code-agentic-layer.md), [ADR-0004](0004-product-spec-v2-phase-gated-scope.md)).
- Kabul edilen risk: Faz 2'ye kadar belgelenen hedef ile kod ayrışır; `docs/product-spec.md` › 3.2 bugünkü yapıyı tarif etmeyi sürdürür.
- Yeni soyutlama ihtiyacı doğarsa (ör. gerçek bir okuma ölçeği sorunu) bu ADR'yi yerine geçen yeni bir ADR ve gerekirse spec güncellemesi ile değiştirmek gerekir; sessizce eklenmez.

## Doğrulama

- Faz 2 çıkış kanıtı: NetArchTest mimari kuralları yeşil, `Domain` dış pakete bağlı değil, `Application` MVC'ye bağlı değil, mevcut tüm testler yeşil.
- Bugün: ADR bağlantıları çözülür, `docs/adr/README.md` indeksi günceldir, `node .claude/hooks/agentic/test-guard.mjs` ve repo hijyeni betiği geçer, `git diff` kapsam dışı dosya içermez.
