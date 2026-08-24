---
title: "StockFlow Yapay Zekâ Bağlam Merkezi"
status: active
authority: index
last_reviewed: "2026-08-18"
review_triggers:
  - context-document-added
  - context-routing-change
  - authority-model-change
---

# StockFlow Yapay Zekâ Bağlam Merkezi

Bu dizin, bir ajanın bütün depoyu her görevde yeniden okumadan doğru bağlamı yüklemesini sağlar. Kök [AGENTS.md](../../AGENTS.md) değişmez çalışma kurallarını, [context-manifest.json](context-manifest.json) ise makinece okunabilir yönlendirmeyi taşır.

## Yetki modeli

| Kaynak | Rol | Çelişkide davranış |
| --- | --- | --- |
| [Ürün spesifikasyonu](../product-spec.md) | Hedef kapsam ve davranış için normatif | Kod farklıysa fark/teknik borç kaydedilir; kod hedefi yeniden tanımlamaz. |
| Kod, yapılandırma, migration ve testler | Bugünkü gerçek | Belge özetiyle çelişirse doğrudan inceleme sonucu esas alınır ve özet güncellenir. |
| Kabul edilmiş [ADR](../adr/README.md) | Kalıcı teknik karar | Ürün sözleşmesini değiştirecekse aynı değişiklikte spesifikasyon da güncellenir. |
| [Handoff](handoff.md) | Görevler arası devamlılık | Karar veya gereksinim kaynağı olarak kullanılmaz. |

## Her görevde oku

1. [Mevcut durum](current-state.md)
2. [Handoff](handoff.md)
3. Değiştirilecek kod ve en yakın testler

## Göreve göre ek bağlam

| Görev türü | Oku |
| --- | --- |
| Özellik, kapsam, kabul ölçütü | [Ürün spesifikasyonu](../product-spec.md), [domain kuralları](domain-rules.md) |
| Controller, Service, veri erişimi, Identity | [Hedef mimari](architecture.md), [ürün spesifikasyonu](../product-spec.md) |
| Entity, EF Core mapping, migration veya ERD | [Veritabanı şeması](../database-schema.md), [domain kuralları](domain-rules.md) |
| Sipariş, stok, roller, silme politikası | [Domain kuralları](domain-rules.md), ilgili spesifikasyon bölümü |
| Test, build, teslim veya belge bakımı | [Geliştirme iş akışı](development-workflow.md) |
| Kalıcı teknoloji veya sorumluluk kararı | [ADR rehberi](../adr/README.md) ve geçerli ADR'ler |

## Bağlam bütçesi

Kök talimat dosyasına ayrıntılı gereksinim kopyalamayın. Görevle ilgisiz belgeleri zorunlu okuma listesine eklemeyin. Bir kural tek bir kanonik yerde ayrıntılı tutulmalı; diğer belgeler kısa özet ve bağlantı vermelidir.

## Güncellik

Belgelerin `last_reviewed` alanı içeriğin son doğrulandığı tarihi gösterir, otomatik doğruluk garantisi değildir. Kod değişikliğinde [güncelleme matrisi](development-workflow.md#dokümantasyon-güncelleme-matrisi) uygulanır ve son olarak doğrulama betiği çalıştırılır.
