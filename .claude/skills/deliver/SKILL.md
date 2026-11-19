---
name: deliver
description: Bir StockFlow isteğini tasarımdan incelenmiş bir pull request'e kadar, uzman subagent'ları (planner, implementer'lar, test-writer, reviewer) insan onay kapılarıyla koordine ederek taşır. Ana oturum koordine eder, asla kendisi uygulamaz. Bir insan bir işi uçtan uca teslim etmeyi istediğinde kullan.
argument-hint: "[GitHub issue numarası, URL veya kısa açıklama]"
disable-model-invocation: true
---

# Teslim: $ARGUMENTS

Bu teslimatın **lider**isin. Koordine edersin; uzmanlar işi kendi bağlamlarında yapar; geri alınamaz her karar insana aittir. Bu akışta kaynak kodu kendin düzenleme.

## 0. Ön kontrol

- `git status` ve dalı kontrol et. `main` üzerindeysen önce iş dalını aç: `git switch -c <tür>/<kısa-konu>` (`feat`, `fix`, `refactor`, `test`, `docs`, `chore`). Çalışma ağacında ilgisiz değişiklik varsa her şeyden önce insana nasıl ilerleneceğini sor.
- İnsanın izleyebilmesi için aşağıdaki aşamalarla bir görev listesi oluştur.

## 1. Tasarım — sonra onay için DUR

`planner` subagent'ına yalnız şunu ver: isteğin referansı/metni ve "plan-work'e göre tasarımı yaz". Döndüğünde insana tasarım yolunu, bölüm tablosunu ve açık soruları göster; AskUserQuestion ile sor: onayla / revize et (açık soru yanıtlarıyla) / dur. Onay olmadan devam etme. İnsan açık soruları yanıtlarsa yanıtları planner'a geri gönder, tasarımı güncellet, tekrar sor.

Tasarım bir kapsam değişikliği bulgusu içeriyorsa (`docs/product-spec.md` dışı davranış), insan spesifikasyon/ADR güncellemesini açıkça onaylamadan uygulamaya geçme.

## 2. Uygula — her delegasyonda tek paket

Onaylanan tasarımdaki her paket için, tasarımdaki sırayla, sahip role delege et (`data-implementer`, `service-implementer`, `web-implementer`). Her prompt yalnız şunları içerir: tasarım yolu, paket kimliği ("yalnız Service 1–2") ve bağımlılık varsa önceki rolün final raporu. Bağımsız paketleri paralel başlat (tek mesajda birden fazla Agent çağrısı); `StockFlow/ViewModels/**` dosyası iki pakette birden geçiyorsa paralel başlatma.

Kanıtla kapıla: rolün raporu red → green test kanıtını ve geçen done gate'i (`dotnet build StockFlow.slnx --no-restore`, `dotnet test StockFlow.slnx --no-restore`, `dotnet format StockFlow.slnx --verify-no-changes --no-restore`) içermeden ilerleme. Raporlanan bloker, eksik bağımlılık, LocalDB ortam hatası veya başarısız gate insana gider — etrafından dolaşma.

**Onaylı değişiklikler** (`StockFlow/Program.cs` DI kaydı, csproj, appsettings): implementer'lar bunları bağımlılık olarak raporlar. Tam değişikliği insana göster, onayıyla ana oturumda uygula (guard onay sorar).

Tasarım bir Tests paketi listeliyorsa veya kapsama boşluğu kalıyorsa `test-writer`'a aynı şekilde delege et.

## 3. Bağımsız doğrula

Done gate'i birleşik çalışma ağacında kendin çalıştır ve `git diff --stat` çıktısını tasarım kapsamıyla karşılaştır. Rapor bir iddiadır; bu kanıttır. Tasarım kapsamı dışındaki her şey insan için bir bulgudur.

Ardından `.claude/rules/editing-discipline.md` › Dokümantasyon tablosunu uygula: etkilenen README, `docs/database-schema.md` veya ADR güncellemesini insana göster ve onayıyla yaz; `docs/product-spec.md` yalnız kullanıcı yetkisiyle değişir. Kalıcı bir konvansiyon ortaya çıktıysa final yanıtta `/curate-knowledge` öner.

## 4. İnceleme — sonra insan için DUR

`reviewer`'a delege et: "çalışma ağacındaki değişiklikleri <tasarım yolu>'na göre incele; yalnız taslak". Özetini, taslak yorumlarını ve karar önerisini sun. İnsana sor: kabul / bu yorumları düzelt / dur. Düzeltmeler için ilgili yorumları sahip role gönder (2. adım kuralları), yeniden doğrula, değişiklik büyükse yeniden incelet.

## 5. Pull request ile teslim

İnsan kabul ettikten sonra:

1. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-repository-hygiene.ps1` çalıştır (staged içerik). Açık yolları stage et (asla `git add -A`) ve iş dalında Conventional Commits biçiminde commit et: `feat(<alan>): <özet>`.
2. İnsana bu PR'ın iş için son PR olup olmadığını sor; yalnız o zaman tasarım dosyasını kaldır (silme onay ister).
3. Push et (guard insandan onay ister) ve `gh pr create --base main --title "<conventional başlık>" --body-file -` ile PR aç; issue varsa bağla. Repoda PR şablonu yoktur.
4. Dur. Merge insanın kararıdır.

## Final yanıt

Tablo: aşama → rol → sonuç → kanıt (gate sonuçları, test sayıları); reviewer'ın karar önerisi; PR URL'si; insanın sıradaki adımı. Oturum kalıcı bir düzeltme veya konvansiyon ortaya çıkardıysa `/curate-knowledge` öner.
