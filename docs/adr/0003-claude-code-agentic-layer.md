---
title: "ADR-0003: Claude Code Ajan Sistemi ve Guardrail'ler"
status: Accepted
authority: decision
last_reviewed: "2026-11-19"
review_triggers:
  - agent-role-change
  - guardrail-policy-change
  - authority-model-change
---

# ADR-0003: Claude Code Ajan Sistemi ve Guardrail'ler

- Tarih: 2026-11-19
- Durum: Accepted
- Karar sahipleri: Proje sahibi
- Etkilenen gereksinimler: Yok (ürün davranışı değişmez)
- Yerine geçtiği ADR: [ADR-0001](0001-ai-context-source-of-truth.md)
- Yerine geçen ADR: Yok

## Bağlam

ADR-0001 araçtan bağımsız bir bağlam sistemi kurdu: kök `AGENTS.md`, `docs/ai/` altında mevcut durum, handoff, mimari, domain kuralları ve iş akışı belgeleri, `context-manifest.json` yönlendirmesi ve bunları doğrulayan `scripts/validate-agent-context.ps1`. Bu sistem ajanlara ne yapmaları gerektiğini anlatır ancak hiçbir kuralı zorlamaz: bir ajan `main`'e push edebilir, User Secrets değerlerini listeleyebilir, üretilmiş migration dosyalarını elle düzenleyebilir veya planlama, uygulama ve onayı tek başına yapabilir. Ayrıca `current-state.md` ve `handoff.md` her değişiklikte elle bakım isteyen anlık görüntülerdi ve koddan kopmuştu (ör. build uyarı sayısı). Proje Claude Code ile geliştirilmektedir; Claude Code yola göre yüklenen kurallar, isteğe bağlı skill'ler, dar yetkili subagent'lar ve her araç çağrısından önce çalışan hook'lar sunar.

## Karar

- Eski bağlam sistemi kaldırılır: `AGENTS.md`, `docs/ai/` dizininin tamamı ve `scripts/validate-agent-context.ps1`. `docs/product-spec.md` normatif ürün sözleşmesi, kod/test bugünkü gerçeğin kaynağı, ADR'ler kalıcı karar kaydı olarak kalır.
- Kök `CLAUDE.md` tek giriş noktasıdır: kaynak yetkisi, değişmez proje sınırları, komutlar, Definition of Done, bilinen ortam gerçekleri, yönlendirme tablosu, git akışı ve yasaklar.
- `.claude/rules/` yola göre yüklenen kısa kuralları (kod `path:line` ve spesifikasyon bölüm atıflarıyla), `.claude/skills/` isteğe bağlı playbook'ları (`plan-work`, `deliver`, `review-changes`, `curate-knowledge`, `add-management-slice`), `.claude/agents/` dar yetkili rolleri (planner, web/service/data implementer, test-writer, reviewer, knowledge-curator) tutar.
- Görev devamlılığı rolling handoff belgesiyle değil iş ürünleriyle sağlanır: tasarım `docs/designs/` (geçici), kanıtlı implementer raporu, taslak review. Kalıcı bilgi `/curate-knowledge` ile incelenmiş PR'dan kütüphaneye girer.
- Kritik kurallar `.claude/hooks/agentic/` içindeki `PreToolUse` guard'ı ve `.claude/settings.json` deny/ask izinleriyle zorlanır: `main`'e push, merge/approve ve secret listeleme reddedilir; push, silme, veritabanı migration'ı ve paket değişikliği insan onayı ister; her rol yalnız kendi katmanına yazar. Bu repoda `bypassPermissions` modu kapalıdır.
- Ajan yapılandırması ve guardrail değişiklikleri `.github/CODEOWNERS` ile code owner incelemesi gerektirir.

## Değerlendirilen alternatifler

- Eski sistemi koruyup `CLAUDE.md`'yi `@AGENTS.md` ile ona bağlamak: İki paralel bağlam kaynağı ve elle bakım isteyen anlık görüntüler sürüyordu; proje sahibi tek sisteme geçmeyi seçti.
- Yalnız `AGENTS.md`: Kural zorlama, rol ayrımı ve yola göre yükleme sağlamaz.
- Kişisel (depo dışı) Claude ayarları: Gözden geçirilemez ve paylaşılamaz.

## Sonuçlar

- Kritik sınırlar istem iyi niyetine değil koda dayanır; ajan değişiklikleri PR ile incelenir.
- Claude Code dışındaki araçlar için hazır giriş noktası (`AGENTS.md`) yoktur; gerekirse `CLAUDE.md`'ye işaret eden ince bir dosya eklenebilir.
- Kaybedilen otomatik kontroller: spesifikasyon yapısı (AC-01..AC-10, tablo/diyagram sayısı) ve `docs/**/*.md` yerel bağlantı doğrulaması. Kütüphane yolları `agentic-repo-setup` doğrulayıcısıyla kontrol edilir.
- `.claude/` katmanı bakım ister: kod yapısı değişince (ör. roadmap Faz 2 proje ayrımı) rol kapsamları, kural glob'ları ve `policy.json` birlikte güncellenmelidir.
- Guard Node.js gerektirir; hook'lar workspace güvenilir olarak işaretlenene kadar çalışmaz. Shell üzerinden yazma kapsamı en iyi çaba düzeyindedir; Windows'ta OS sandbox yoktur, bu yüzden CODEOWNERS ve branch protection son savunma hattıdır.

## Doğrulama

- `node .claude/hooks/agentic/test-guard.mjs` geçer.
- `CLAUDE.md` 200 satırın altında kalır; yönlendirme tablosundaki ve kurallardaki her yol mevcuttur.
- Depoda `AGENTS.md`, `docs/ai/` veya `validate-agent-context.ps1` atfı kalmaz (bu ADR ve ADR-0001'in tarihsel notu hariç).
