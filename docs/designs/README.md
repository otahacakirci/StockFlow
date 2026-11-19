---
title: "StockFlow Ajan Tasarım Belgeleri"
status: active
authority: handoff-space
last_reviewed: "2026-11-19"
review_triggers:
  - delivery-process-change
  - agent-role-change
---

# StockFlow Ajan Tasarım Belgeleri

`planner` her istek için buraya bir tasarım yazar: `<yyyy-mm-dd>-<kebab-konu>.md` (GitHub issue varsa `ISSUE-<numara>-<kebab-konu>.md`). Şablon: `.claude/skills/plan-work/references/design-template.md`. Karar kaydı: [ADR-0003](../adr/0003-claude-code-agentic-layer.md).

- Implementer'lar planner'ın konuşmasını değil tasarımı okur. Uygulama başlamadan önce bir insan tasarımı onaylar.
- Tasarım geçicidir: işin son pull request'i onu (insanın onayıyla) siler. Kalıcı bilgi `/curate-knowledge` ile `.claude/rules/` veya `.claude/skills/` altına, kalıcı teknik karar [ADR](../adr/README.md) olarak gider. Ayrı bir rolling handoff belgesi yoktur.
- Tasarım ürün gereksinimi değildir; hedef davranış için [ürün spesifikasyonu](../product-spec.md) normatif kalır.
- Yolları `kod` biçiminde yaz; Markdown bağlantısı eklersen hedefin var olduğunu kontrol et (otomatik bağlantı doğrulayıcısı yoktur).
