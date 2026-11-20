---
name: plan-work
description: StockFlow için bir istek, hata veya değişiklik talebini, implementer ajanlarının üzerine inşa edeceği tek bir teknik tasarım dosyasına (docs/designs/) dönüştürür — isteği okur, açık soruları toplar, mevcut kodu denetler, işi rollerin sahip olduğu paketlere böler ve her kabul ölçütünü iş kalemlerine ve testlere bağlar. Planla, tasarla, analiz et, kapsamla veya böl istendiğinde kullan.
argument-hint: "[GitHub issue numarası, URL veya kısa açıklama]"
---

# İşi planla — StockFlow

Çıktı: **tek tasarım dosyası**, `docs/designs/<yyyy-mm-dd>-<kebab-konu>.md` (issue varsa `ISSUE-<numara>-<kebab-konu>.md`). Uygulayan kişinin elinde yalnız bu dosya olur, bu konuşma olmaz. Planlarken kaynak kodu asla düzenleme.

## 1. İsteği anla

1. İsteği eksiksiz oku: başlık, gövde, kabul ölçütleri, yorumlar. GitHub issue ise `gh issue view <n> --comments`; repo henüz issue kullanmıyor, istekler çoğunlukla metin olarak gelir.
2. Belirsiz, çelişkili veya eksik her noktayı listele ("mevcut kayıtlara ne olur?", "Admin mi Employee mi yapabilir?").
3. İnsana erişimin varsa şimdi sor. `planner` subagent'ı olarak çalışıyorsan soramazsın: her noktayı seçtiğin varsayım ve gerekçesiyle *Açık sorular* altına yaz.

## 2. Kodu ve sözleşmeyi denetle

- Bugünkü davranışı koddan ve testlerden oku; kapsam/iş kuralı değişiyorsa `docs/product-spec.md` ilgili bölümünü (› 4 roller, › 5 gereksinimler, › 6 veri, › 7 sipariş/stok, › 9 kabul senaryoları), kalıcı kararlar için `docs/adr/` oku.
- İstek `docs/product-spec.md` kapsamında mı? Değilse (ör. fazı `Kapalı` roadmap maddesi, › 10.3 kalıcı kapsam dışı bileşen, yeni domain kavramı) bu bir **kapsam değişikliği bulgusudur**: tasarım önce spesifikasyon/ADR güncellemesini ve kullanıcı onayını paket olarak listeler. Faz kapılı istekte › 10.1 durumunu ve fazın FR/AC'lerinin spec'te olup olmadığını denetle.
- Aynı alana dokunan Controller, Service, ViewModel, View, Entity, Configuration ve testleri bul (`StockFlow/`, `StockFlow.Tests/`).
- Hangi kurallar geçerli: `.claude/rules/web-layer.md`, `services.md`, `orders-stock.md`, `data.md`, `testing.md`, `security.md`. Bölüm adlarını ver.
- Şema değişikliği var mı (yeni migration)? `Program.cs`, csproj veya `appsettings*.json` değişikliği var mı? Bunlar insan onayı ister; tasarımda ayrı kalem olarak işaretle.
- `.claude/rules/editing-discipline.md` › Dokümantasyon tablosundan etkilenen belgeleri listele.

## 3. İşi böl

Her gereksinimi tam bir pakete yerleştir:

| Paket | Sahip rol | Kapsam |
| --- | --- | --- |
| Data | `data-implementer` | `StockFlow/Data/**` (üretilmiş migration Designer/Snapshot hariç), `StockFlow/Entities/**`, `StockFlow/Options/**`, `StockFlow.Tests/Data/**`, `StockFlow.Tests/Infrastructure/**` |
| Service | `service-implementer` | `StockFlow/Services/**`, Service sözleşmesindeki `StockFlow/ViewModels/**` modelleri, `StockFlow.Tests/Services/**` |
| Web | `web-implementer` | `StockFlow/Controllers/**`, `StockFlow/Views/**`, `StockFlow/ViewModels/**`, `StockFlow/wwwroot/js/**`, `StockFlow/wwwroot/css/**`, `StockFlow/ModelBinding/**`, `StockFlow/Models/**`, `StockFlow/Security/**`, `StockFlow.Tests/Controllers/**`, `StockFlow.Tests/Integration/**`, `StockFlow.Tests/ViewModels/**`, `StockFlow.Tests/ModelBinding/**` |
| Tests | `test-writer` | `StockFlow.Tests/**` (ek kapsama, karakterizasyon) |
| Onaylı | ana oturum + insan | `StockFlow/Program.cs` (DI kaydı), `*.csproj`, `appsettings*.json`, `docs/product-spec.md`, ADR, README, `docs/database-schema.md` güncellemeleri |

`StockFlow/ViewModels/**` hem Service hem Web paketinin kapsamındadır: tasarımda her ViewModel dosyasını tek pakete ata ki paralel çalışan iki rol aynı dosyaya dokunmasın. Paketler arası bağımlılığı yaz ("Web 1, Service 1'in `ISupplierService` sözleşmesine bağlı"). Hangi paketlerin paralel koşabileceğini işaretle (genellikle Data → Service → Web sıralıdır).

## 4. Tasarımı yaz

[references/design-template.md](references/design-template.md) başlıklarını aynı sırayla kullan. Her kabul ölçütü en az bir iş kalemine ve bir teste (veya açık bir manuel kontrole) bağlanır.

## Kapanış

Dosya yolunu, bölüm tablosunu ve açık soruları ver. Uygulamaya başlama.
