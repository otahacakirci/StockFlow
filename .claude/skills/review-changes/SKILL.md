---
name: review-changes
description: Bir StockFlow pull request'inin veya yerel çalışma ağacı değişikliklerinin kanıta dayalı, yapılandırılmış incelemesi — gerçek diff, iki yönlü kabul ölçütü denetimi, dosya dosya repo kontrol listesi, done gate, önceki review thread'leri — ve bir insan için path:line içeren gönderilmeye hazır taslak yorumlar ile karar önerisi döndürür. PR'ı numara/URL ile incelemek, değişiklikten sonra yeniden incelemek veya PR açmadan önce yerel değişiklikleri kontrol etmek için kullan.
argument-hint: "[PR numarası/URL | working tree]"
---

# Değişiklikleri incele — StockFlow

Amaç: **tutarlı, temellendirilmiş** bir inceleme. Her bulgu diff'teki bir satırı gösterir ve bir kurala atıf yapar — görüş değil. İncelemeyi salt-okunur `reviewer` taslak olarak hazırlar; neyin gönderileceğine insan karar verir.

Ana oturumsan: hedefle ("PR <n>" veya "çalışma ağacı, <tasarım yolu>'na göre") `reviewer` subagent'ına delege et ve çıktısını sun. Yorum göndermek, approve veya request changes insanın işidir (insan senden göndermeni isterse guard her gönderim için onay ister; approve ve merge asla ajan tarafından yapılmaz).

`reviewer` isen:

## 1. Gerçek diff'i al

- PR: `gh pr view <n> --json number,title,body,baseRefName,headRefName,headRefOid,closingIssuesReferences,files`, `gh pr diff <n>`, `gh pr checks <n>` (repoda CI yok; checks boş dönebilir).
- Merge-base'e göre diff al, eski yerel dalda `base..head` değil: `git diff $(git merge-base origin/main <head>) <head>`.
- Çalışma ağacı: güncel `HEAD`'e karşı `git diff` ve `git diff --staged`.

## 2. İstek / tasarım denetimi (iki yönlü)

Bağlı issue ve `docs/designs/` içindeki tasarımdan: karşılanmayan kabul ölçütü → bulgu; dayanağı olmayan esaslı değişiklik → "kapsam dışı, niyeti doğrula". `docs/product-spec.md` dışında davranış ekleyen değişiklik → spesifikasyon/ADR güncellemesi yoksa bulgu.

## 3. Kontrol listesi

Değişen dosyaları kategorilere ayır ve [references/checklist.md](references/checklist.md) bölümlerini uygula. Her maddeyi ✅ / ❌ / ⚠️ / N/A olarak işaretle. Done gate'i çalıştır ve sonucu raporla: `dotnet build StockFlow.slnx --no-restore`, `dotnet test StockFlow.slnx --no-restore`, `dotnet format StockFlow.slnx --verify-no-changes --no-restore`. Hiçbir şeyi düzeltme. LocalDB ortam hatası "test başarısız" değil "doğrulanamadı" olarak raporlanır.

## 4. Önceki thread'ler (PR'lar)

Her yazar yanıtını güncel head'e karşı doğrula: "düzeltildi" → kodda teyit et; itiraz → atıf yapılan kuralı kontrol et; ödünleşim → iki tarafla *insan kararı gerekli* olarak işaretle.

## Yorum dili

Türkçe; eylemle başla, yorum başına tek eylem; somut hata senaryosunu ver; kurala atıf yap (`.claude/rules/<dosya>.md` › bölüm); mekanik düzeltmelerde suggestion bloğu; övgü dolgusu yok.

## Çıktı (sırayla)

1. **Özet** — hedef, yazar, bağlı issue/tasarım, dokunulan alanlar, done gate sonucu, head SHA.
2. **İstek denetimi** — ölçüt | karşılandı mı? | kanıt.
3. **Kontrol listesi** — uygulanan her bölüm için bir tablo; N/A bölümler listelenir.
4. **Taslak yorumlar** — `path:line` (head'de geçerli) + tam metin.
5. **Thread kararları** — çöz / itiraz et (taslak yanıt) / insan kararı gerekli.
6. **Karar önerisi** — approve / request changes / comment, tek satır gerekçe. Karar insanındır.
