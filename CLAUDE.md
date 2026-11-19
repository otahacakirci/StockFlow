# StockFlow — Claude Code talimatları

StockFlow; kategori, ürün, müşteri, tedarikçi, satış/satın alma siparişi ve stok hareketlerini yöneten tek ASP.NET Core MVC mini ERP uygulamasıdır (`StockFlow/StockFlow.csproj`). Çekirdek değeri: Draft sipariş stoğu etkilemez, Confirm sipariş durumunu, stokları ve hareket geçmişini tek SQL transaction'da değiştirir (README). Kullanıcılar `Admin` ve `Employee` rolleridir. Ajan sisteminin kararı: [ADR-0003](docs/adr/0003-claude-code-agentic-layer.md).

## Kaynakların yetkisi

- **Hedef davranış ve kapsam:** `docs/product-spec.md` normatiftir. Kod onunla çelişiyorsa bunu mevcut-hedef farkı olarak bildir; mevcut kodu onaylanmış hedef sayma.
- **Bugünkü gerçek:** kod, yapılandırma, migration'lar ve testler. Belge özetlerine değil koda bak.
- **Kalıcı karar:** kabul edilmiş ADR (`docs/adr/`); ürün sözleşmesini sessizce daraltamaz veya genişletemez.
- **Önerilen yön:** `docs/roadmap.md` normatif değildir; maddeleri `product-spec.md` ve ADR'ye işlenmeden uygulanmaz.
- Kullanıcı ürün sözleşmesini değiştirirse aynı değişiklik kümesinde spesifikasyonu, ilgili ADR'yi ve etkilenen kural/skill dosyalarını güncelle. Bonuslar yalnız `product-spec.md` › 2.2 öncelik kapısı sağlandığında ele alınır.

## Stack

- .NET 10 / C# (`net10.0`, nullable, implicit usings), ASP.NET Core MVC + Razor + Bootstrap, cookie tabanlı ASP.NET Core Identity.
- EF Core 10 + SQL Server; geliştirme ve testlerde SQL Server LocalDB (`(localdb)\MSSQLLocalDB`). `dotnet-ef` 10.0.11 yerel araç manifestinde (`.config/dotnet-tools.json`).
- xUnit 2.9.3 ve `Microsoft.AspNetCore.Mvc.Testing` (`StockFlow.Tests/StockFlow.Tests.csproj`). Biçim `dotnet format` ile; `.editorconfig` yok.

## Komutlar (depo kökünden)

| Görev | Komut |
| --- | --- |
| Bağımlılık | `dotnet restore StockFlow.slnx` ve `dotnet tool restore` |
| Build | `dotnet build StockFlow.slnx --no-restore` |
| Bütün testler | `dotnet test StockFlow.slnx --no-restore` |
| Tek test sınıfı | `dotnet test StockFlow.Tests/StockFlow.Tests.csproj --no-restore --filter "FullyQualifiedName~StockFlow.Tests.Services.CategoryServiceTests"` |
| Format kontrolü | `dotnet format StockFlow.slnx --verify-no-changes --no-restore` |
| Repo hijyeni (commit öncesi) | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-repository-hygiene.ps1` |
| Uygulamayı çalıştır | `dotnet run --project StockFlow/StockFlow.csproj --launch-profile https` (önce README'deki User Secrets ve migration adımları) |
| Guard testleri | `node .claude/hooks/agentic/test-guard.mjs` |

**Done gate:** `dotnet build StockFlow.slnx --no-restore` ve `dotnet test StockFlow.slnx --no-restore` geçmeden ve `dotnet format StockFlow.slnx --verify-no-changes --no-restore` temiz olmadan bir değişikliği "bitti" diye sunma; sonuçları, çalıştırılmayan kontroller dahil raporla. Commit öncesi repo hijyeni de çalışır.

## Definition of Done

- İstenen davranış uygulanmış, kapsam dışı ekleme yapılmamış.
- İlgili testler eklenmiş/güncellenmiş ve çalıştırılmış (red → green kanıtıyla).
- Güvenlik ve atomiklik kuralları korunmuş.
- Etkilenen belgeler güncellenmiş (`.claude/rules/editing-discipline.md` › Dokümantasyon).
- Done gate ve, commit varsa, repo hijyeni geçmiş.

## Bilinen ortam gerçekleri

- İlişkisel testler Windows ve çalışan SQL Server LocalDB ister (ADR-0002); CI ve çapraz platform test hedefi yok. LocalDB açılamazsa testleri "geçti" sayma, ortam hatası olarak raporla.
- Uygulama migration'ları otomatik uygulamaz; çalışmak için migration uygulanmış veritabanı ve dört `IdentitySeed` değeri User Secrets/ortamda olmalıdır (README).
- Build bugün 0 hata, 4 uyarı verir: `System.Security.Cryptography.Pkcs` 9.0.11 geçişli bağımlılığı için NU1902/NU1903. Bunlar bilinen durumdur; yeni uyarı ekleme.
- LocalDB yalnız geliştirme/öğrenme ortamıdır, production hedefi değildir.

## Mimari özet

| Alan | İçerik | Bağımlılık yönü |
| --- | --- | --- |
| `StockFlow/Controllers`, `Views`, `ViewModels`, `wwwroot`, `ModelBinding`, `Models`, `Security` | HTTP, Razor UI, giriş/çıkış sözleşmeleri, roller | → Services |
| `StockFlow/Services` | İş kuralları, durum geçişleri, transaction sınırı, `ServiceResult` | → Data, Entities |
| `StockFlow/Data`, `Entities`, `Options` | `ApplicationDbContext`, Fluent mapping, migration, seeder | — |
| `StockFlow/Program.cs` | Composition root: DI kaydı, Identity/cookie, auth fallback, seeder çağrısı | hepsi |
| `StockFlow.Tests` | xUnit; Controllers (DB'siz), Services/Data/Integration (LocalDB) | → StockFlow |

Ayrıntı ve sorumluluk tablosu: `docs/product-spec.md` › 3.2 Mimari sorumluluklar. Üretilmiş dosyalar: `StockFlow/Data/Migrations/*.Designer.cs` ve `ApplicationDbContextModelSnapshot.cs` (`dotnet ef` üretir); `StockFlow/wwwroot/lib/**` üçüncü taraf dağıtımıdır (`.gitattributes`).

## Değişmez proje sınırları

- Zorunlu MVP tek ASP.NET Core MVC uygulamasıdır: Razor/Bootstrap UI, Service iş kuralları, EF Core `ApplicationDbContext`, SQL Server ve cookie tabanlı Identity.
- Controller doğrudan `ApplicationDbContext` kullanmaz; Entity nesneleri kullanıcı girdisi olarak kabul edilmez.
- Draft sipariş stok değiştirmez. Confirm stok, `StockMovement` ve sipariş durumunu tek atomik sınırda yazar. Confirmed ve Cancelled terminaldir.
- JWT, yazma amaçlı Web API, Clean Architecture, CQRS, MediatR, generic Repository/Unit of Work, AutoMapper, FluentValidation, mikroservis ve benzerleri açık kapsam değişikliği olmadan eklenmez (`docs/product-spec.md` › 10.3).

## Önce nereye bak

| Görev | Oku |
| --- | --- |
| Herhangi bir değişiklik | `.claude/rules/editing-discipline.md`, `.claude/rules/security.md` |
| Özellik, kapsam, kabul ölçütü | `docs/product-spec.md` (› 5 Fonksiyonel gereksinimler, › 9 Kabul senaryoları) |
| Controller, View, ViewModel, JS/CSS | `.claude/rules/web-layer.md` |
| Service iş kuralı | `.claude/rules/services.md` |
| Sipariş, stok, StockMovement | `.claude/rules/orders-stock.md`, `docs/product-spec.md` › 7 |
| Entity, mapping, migration, seeder | `.claude/rules/data.md`, `docs/database-schema.md` |
| Test yazma/çalıştırma | `.claude/rules/testing.md` |
| Kalıcı teknik karar | `docs/adr/README.md`, `docs/adr/0000-template.md` |
| Yeni yönetim ekranı (Service + Controller + Views) | `.claude/skills/add-management-slice/SKILL.md` |
| İşi planlama | `.claude/skills/plan-work/SKILL.md` |
| Değişiklik inceleme | `.claude/skills/review-changes/SKILL.md` |

## Ajan sistemi

| Rol | Yapar | Yapamaz |
| --- | --- | --- |
| `planner` | `docs/designs/` altına tasarım belgesi yazar | Kaynak kod, test veya yapılandırma düzenlemek |
| `web-implementer` | Controllers, Views, ViewModels, wwwroot (lib hariç), ModelBinding, Models, Security ve ilgili testler | Service/Data, `Program.cs`, csproj |
| `service-implementer` | `StockFlow/Services/**`, Service sözleşmesindeki `StockFlow/ViewModels/**` modelleri ve `StockFlow.Tests/Services/**` | Controller/View/Data katmanı, `Program.cs` |
| `data-implementer` | Data, Entities, Options ve `StockFlow.Tests/Data/**`, `StockFlow.Tests/Infrastructure/**` | Üretilmiş migration Designer/Snapshot dosyalarını elle düzenlemek |
| `test-writer` | `StockFlow.Tests/**` | Üretim kodu |
| `reviewer` | Taslak review yorumları ve karar önerisi | Dosya düzenlemek, yorum göndermek, approve/merge |
| `knowledge-curator` | `CLAUDE.md`, `.claude/rules/`, `.claude/skills/`, `.claude/agents/` güncelleme taslağı | Kaynak kod, guardrail, `policy.json` |

`StockFlow/ViewModels/**` bilinçli olarak web ve service implementer'larının ortak kapsamındadır (Service'ler bugün ViewModel sözleşmesi kullanır); tasarım her ViewModel dosyasını tek pakete atar.

- Teslim `/deliver <istek>`, inceleme `/review-changes <PR | working tree>`, bilgi güncellemesi `/curate-knowledge [yol]` ile başlar.
- Ajanlar sohbet değil iş ürünü devreder: tasarım `docs/designs/`, rapor red → green test kanıtı ve done gate sonucu, review taslak yorumlar. Tasarım belgeleri geçicidir; kalıcı bilgi `.claude/rules/` veya `.claude/skills/` altına, kalıcı karar ADR'ye gider.
- `Program.cs`, `*.csproj`, `StockFlow.slnx`, `.config/dotnet-tools.json`, `appsettings*.json`, `scripts/*.ps1` ve `docs/product-spec.md` değişiklikleri insan onayı ister; subagent'lar bunları değiştiremez, ihtiyacı "bağımlılık" olarak raporlar.
- Bir insan ajanı düzelttiğinde ve düzeltme bu görevin ötesinde geçerliyse `/curate-knowledge` ile kural/skill güncellemesine ve PR'a dönüşür; anlık düzenlemeyle değil.
- Guardrail'ler `.claude/hooks/agentic/` ile zorlanır (politika: `.claude/hooks/agentic/policy.json`). Bir araç çağrısı reddedilirse geçici yol arama; raporla ve devam et.

## Git akışı — ajanlar her zaman pull request ile teslim eder (kesin kural)

1. İlk düzenlemeden önce dalı kontrol et. `main` üzerindeysen önce dal aç: `git switch -c <tür>/<kısa-konu>` (tür conventional commit türüyle aynı: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`).
2. Yalnız o dalda, açık yollar stage ederek commit et. Mesaj Conventional Commits biçimindedir: `<tür>(<kapsam>): <özet>`, ör. `feat(orders): add admin draft order actions` (`git log` geçmişindeki commit'lerin neredeyse tamamı bu biçimdedir).
3. Push insan onayı ister. GitHub'da `main` hedefli pull request aç.
4. Orada dur. Merge ve approve insan kararıdır.

## Asla yapma

- Secret, parola, connection string veya User Secrets değerini okumak, yazdırmak, belgeye/koda/loga koymak (`dotnet user-secrets list` engellidir).
- `appsettings*.json` içine bağlantı dizesi veya kimlik bilgisi eklemek.
- Migration `*.Designer.cs`, `ApplicationDbContextModelSnapshot.cs` veya `StockFlow/wwwroot/lib/**` dosyalarını elle düzenlemek.
- Testlerde InMemory sağlayıcısına veya geliştirme veritabanına geri dönmek (ADR-0002).
- Draft siparişte stok değiştirmek; stok/StockMovement yazımını `OrderService` confirm transaction'ı dışına taşımak.
- Yukarıdaki "Değişmez proje sınırları" listesindeki kapsam dışı bileşenleri açık kapsam değişikliği olmadan eklemek.
- Açık onay olmadan dosya silmek; ilgisiz kodu yeniden biçimlendirmek veya refactor etmek; `docs/product-spec.md` dosyasını kullanıcı yetkisi olmadan değiştirmek.
- `main`'e push, approve veya merge.

## Öncelik sırası

1. Oturumdaki açık insan talimatı kazanır; git akışı, "Değişmez proje sınırları" ve "Asla yapma" listesi hariç. Bunlar yalnız bu dosyaya yapılan, incelenmiş bir PR ile değişir.
2. `.claude/rules/*` skill içeriğinden önce gelir; bir `SKILL.md` kendi `references/` dosyalarından önce gelir.
3. Hedef davranış için `docs/product-spec.md`, bugünkü gerçek için kod ve testler esastır. Talimat ile kod çelişirse bunu söyle ve ikisini de "düzeltmeden" önce sor.

Belgeler Türkçe; kod sembolleri ve dosya adları İngilizce kalır.
