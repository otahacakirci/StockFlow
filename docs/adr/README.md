---
title: "StockFlow Mimari Karar Kayıtları"
status: active
authority: decision-process
last_reviewed: "2026-08-18"
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
5. Manifest ve bağlam doğrulama betiğini güncelle/çalıştır.

## Karar indeksi

| ADR | Durum | Karar |
| --- | --- | --- |
| [0001](0001-ai-context-source-of-truth.md) | Accepted | Ajan bağlamının Markdown kanonik kaynak ve modüler belgelerle yönetilmesi |
