---
title: "StockFlow Mimari Karar Kayıtları"
status: active
authority: decision-process
last_reviewed: "2026-11-20"
review_triggers:
  - adr-process-change
---

# StockFlow Mimari Karar Kayıtları

ADR'ler uzun ömürlü ve uygulanması başka görevleri etkileyen teknik kararları kaydeder. Günlük görev notu, geçici hata analizi veya ürün backlog'u ADR değildir.

## Durumlar

- `Proposed`: Değerlendiriliyor; uygulama otoritesi değildir.
- `Accepted`: Geçerli ve uygulanması beklenen karar.
- `Superseded`: Daha yeni bir ADR tarafından değiştirilmiş tarihsel kayıt.

Başka durum değeri kullanılmaz.

## Yeni ADR oluşturma

1. [0000-template.md](0000-template.md) dosyasını sonraki dört basamaklı numarayla kopyala.
2. Bağlamı, kararı, alternatifleri, sonuçları ve etkilenen gereksinim kimliklerini doldur.
3. Karar ürün davranışını veya kapsamını değiştiriyorsa aynı değişiklikte [ürün spesifikasyonunu](../product-spec.md) güncelle ve kullanıcı yetkisini kaydet.
4. Yerine geçen ADR varsa eski kaydı `Superseded` yap ve iki dosyayı karşılıklı bağla.
5. Karar ajanların çalışma biçimini etkiliyorsa `CLAUDE.md` ve ilgili `.claude/rules/` dosyalarını aynı değişiklikte güncelle (`/curate-knowledge`); bu indekse satır ekle.

## Karar indeksi

| ADR | Durum | Karar |
| --- | --- | --- |
| [0001](0001-ai-context-source-of-truth.md) | Superseded | Ajan bağlamının Markdown kanonik kaynak ve modüler belgelerle yönetilmesi (yerine 0003) |
| [0002](0002-isolated-localdb-test-databases.md) | Accepted | Veritabanına dokunan xUnit testlerinin test başına benzersiz ve güvenlik kontrollü LocalDB veritabanı kullanması |
| [0003](0003-claude-code-agentic-layer.md) | Accepted | Ajan bağlamının `CLAUDE.md` ve `.claude/` kurallar, skill'ler, roller ve guardrail hook'larıyla yönetilmesi; eski `AGENTS.md`/`docs/ai/` sisteminin kaldırılması |
| [0004](0004-product-spec-v2-phase-gated-scope.md) | Accepted | Ürün sözleşmesi v2: kapsamın `product-spec.md` § 10.1 faz kapılarıyla açılması, kalıcı kapsam dışı ve faz kapılı bileşen ayrımı |
| [0005](0005-lean-four-project-architecture.md) | Accepted | Yalın 4 projeli modüler monolit (Domain, Application, Infrastructure, Web) hedefi; MediatR, CQRS, generic Repository/UoW, AutoMapper ve mikroservisin eklenmemesi; Faz 2 kapalı kalır |
