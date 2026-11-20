# Tasarım şablonu

`docs/designs/<yyyy-mm-dd>-<kebab-konu>.md` (issue varsa `ISSUE-<numara>-<kebab-konu>.md`) olarak kaydet. Düz cümleler; analizi nasıl yaptığının anlatımı yok. Yolları `kod` biçiminde yaz; Markdown bağlantısı kullanırsan hedefin var olduğundan emin ol.

```markdown
# <ID veya tarih>: <başlık>

- Kaynak: <GitHub issue URL'si veya "metin istek">
- Tarih: <yyyy-mm-dd>
- Paketler: Data ☐ · Service ☐ · Web ☐ · Tests ☐ · Onaylı ☐

## İstenen

<Gereksinim kendi sözlerinle, 3–8 cümle. Kim yararlanır, neden. product-spec kapsamında mı?>

## Bilinmesi gereken mevcut mantık

<Bu alana dokunan her Controller/Service/ViewModel/View/Entity/test — yol, bugün ne yaptığı, bu iş onu genişletiyor mu, değiştiriyor mu, dokunmuyor mu. İstekle veya bir kuralla çelişen mevcut davranış. Gerçekten hiçbir şey yoksa "Yeni alan".>

## Geçerli kurallar

<`.claude/rules/…` ve `.claude/skills/…` bölümlerine işaret eden maddeler.>

## Bölüm

| # | Gereksinim / kabul ölçütü | Paket (sahip rol) | Neden orada |
| --- | --- | --- | --- |
| 1 | … | … | … |

## Data işi

<Entity alanları, Fluent mapping (uzunluk, precision, check constraint, Restrict), migration adı, seeder etkisi. Yoksa "Yok".>

## Service işi

<Arayüz imzaları, `ServiceResult` dönüşleri, yeni `<Domain>ServiceErrorCodes` sabitleri, doğrulama kuralları, transaction ihtiyacı, sahiplendiği ViewModel dosyaları.>

## Web işi

<Action'lar ve HTTP fiilleri, `[Authorize]` rolleri, ServiceResult → 400/404/409/500 eşlemesi, View dosyaları, Türkçe UI metinleri, boş/hata durumları, `_Layout.cshtml` gezinme değişikliği.>

## Onaylı değişiklikler

<`Program.cs` DI kaydı, csproj/paket, appsettings, product-spec/ADR/README/`docs/database-schema.md` güncellemeleri — insan onayıyla ana oturumda yapılır.>

## Test planı

| Kabul ölçütü | Test dosyası | Senaryo(lar) |
| --- | --- | --- |
| 1 | `StockFlow.Tests/...` | … |

## İş sırası

<ör. Data 1 → Service 1 → Web 1. Paralel koşabilecek paketler.>

## Açık sorular

<Numaralı: soru, kimin yanıtlaması gerektiği, yanıtlanmazsa alınan varsayım. Yoksa "Yok — istek netti.">

## Manuel doğrulama

<3–6 somut adım: `dotnet run --project StockFlow/StockFlow.csproj --launch-profile https`, https://localhost:7141 üzerinde Admin ve Employee ile giriş, …>
```
