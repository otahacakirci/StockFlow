---
name: curate-knowledge
description: StockFlow ajan kütüphanesini insan onayıyla güncel tutar. Argümansız çağrıldığında bu oturumun öğrettiklerini (düzeltmeler, tekrarlanan hatalar, keşfedilen komutlar, belirsiz kurallar, auto-memory notları) gözden geçirip kural/skill güncellemesi taslağı çıkarır; bir yol verildiğinde o alanın yalnız kodda yaşayan konvansiyonlarını file:line atıfıyla bir kurala veya skill'e dönüştürür. Değişiklikler incelenmiş bir pull request ile girer.
argument-hint: "[kodlaştırılacak yol — bu oturumun öğrendiklerini işlemek için boş bırak]"
disable-model-invocation: true
---

# Bilgiyi düzenle: $ARGUMENTS

Kütüphane (`CLAUDE.md`, `.claude/rules/`, `.claude/skills/`, `.claude/agents/`) gelecekteki her oturumun okuduğu şeydir. Yalnız kanıtla ve insan onayıyla değişir. Ürün davranışı `docs/product-spec.md` içindedir ve kütüphanede çoğaltılmaz; kurallar ilgili spesifikasyon bölümüne kısa atıf verir (`CLAUDE.md` › Kaynakların yetkisi).

## Mod A — argüman yok: bu oturum ne öğretti?

1. Bu konuşmadan ve auto memory'den adayları topla: insanın yaptığı düzeltmeler, birden fazla kez olan hatalar, keşfedilmesi gereken komut veya kurulum bilgileri, belirsiz ya da kodla çelişen kurallar, eksik playbook adımları.
2. Yalnız **kalıcı** (gelecek ay da önemli), **repo veya alan geneli** (bu tek görevle ilgili değil) ve **doğrulanabilir** (kodda kontrol edilebilir veya insan tarafından söylenmiş) adayları tut. Kişisel tercihler insanın kendi `CLAUDE.local.md` dosyasına aittir.
3. Her öğe için önerilen hedefi (kural / skill / CLAUDE.md / ajan görevi / `docs/product-spec.md` veya ADR — bu ikisi kullanıcı yetkisi ister), tam cümleyi ve kanıtını göster. İnsana hangilerinin tutulacağını sor (AskUserQuestion, çoklu seçim).

## Mod B — bir yol: kodu talimata dönüştür

1. `$ARGUMENTS` altındaki kodu (ve testlerini) oku — giriş noktaları, en çok değişen dosyalar (`git log --format= --name-only -- $ARGUMENTS | sort | uniq -c | sort -rn | head`), eski kararlı bir dosya.
2. Bir inceleyicinin zorlayacağı konvansiyonları her biri `path:line` kanıtıyla çıkar; tutarsızlıkları soru olarak not et.
3. Öner: tekrar eden çok adımlı bir prosedür için **Dal A — skill** (örnek dosyalarla), ve/veya o dosyalardaki kısıtlar için `paths:` glob'lu **Dal B — kural**. İnsana hangisinin taslağını çıkaracağını sor.

## Her iki mod — taslak, inceleme, teslim

1. Bir dalda olduğundan emin ol (`git switch -c docs/knowledge-<konu>`), `main` üzerinde değil.
2. `knowledge-curator` subagent'ına yalnız onaylanan öğeleri ve kanıtlarını ver. Taslak düzenlemeleri o yapar (Claude Code `.claude/` altına yazmadan önce insana sorar). Spesifikasyon/ADR öğelerini ana oturum insan onayıyla yazar.
3. İnsana diff'i göster. Kontrol: her yeni kural kanıta atıf yapıyor; `paths:` glob'ları gerçek dosyalarla eşleşiyor; `CLAUDE.md` 200 satırın altında; yönlendirme tablosu ve kurallardaki her yol/`path:line` atfı var olan dosyalara işaret ediyor.
4. Commit et (`docs(agents): …`), push et (onay ister) ve "agent library: …" başlıklı pull request aç. `.claude/` için code owner (`.github/CODEOWNERS`) inceler ve merge eder.

Bu akışta hiçbir rolün yetkisini genişletme: yazma kapsamları `.claude/hooks/agentic/policy.json` içindedir ve onu yalnız bir insan, incelenmiş bir PR'da değiştirir.

Roadmap Faz 2 (proje ayrımı) gibi yapısal bir değişiklikten sonra kütüphaneyi bu skill ile yamamak yerine `agentic-repo-setup` skill'ini update mode'da yeniden çalıştır: roller, kapsamlar ve kural glob'ları birlikte değişir.
