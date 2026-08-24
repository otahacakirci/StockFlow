---
title: "ADR-0001: Yapay Zekâ Bağlamı ve Doğruluk Kaynağı"
status: Accepted
authority: decision
last_reviewed: "2026-08-18"
review_triggers:
  - authority-model-change
  - canonical-source-change
---

# ADR-0001: Yapay Zekâ Bağlamı ve Doğruluk Kaynağı

- Tarih: 2026-08-18
- Durum: Accepted
- Karar sahipleri: Proje sahibi
- Etkilenen gereksinimler: OKUMA-01, OKUMA-02, OKUMA-03
- Yerine geçtiği ADR: Yok
- Yerine geçen ADR: Yok

## Bağlam

Proje yapay zekâ destekli geliştirilecektir. Tek bir büyük talimat dosyası bağlam maliyetini yükseltir; yalnızca kodu okumak ise mevcut JWT/bellek içi prototip ile hedef Identity/EF Core mimarisinin karıştırılmasına neden olabilir. Mevcut ürün sözleşmesi DOCX biçimindedir ve ajanlar tarafından her araçta aynı güvenilirlikle okunamaz.

## Karar

- Kök `AGENTS.md` kısa, araçtan bağımsız giriş noktası ve değişmez çalışma kuralları olarak kullanılacaktır.
- `docs/product-spec.md` yapay zekâ ve geliştirici çalışmaları için operasyonel kanonik ürün sözleşmesi olacaktır.
- Kaynak DOCX sürüm 1.1 tarihsel anlık görüntü olarak korunacak, otomatik olarak güncel sayılmayacaktır.
- Mevcut gerçek, hedef mimari, yüksek frekanslı domain kuralları ve handoff ayrı Markdown belgelerinde tutulacaktır.
- `context-manifest.json` görev-belge eşleşmesini makinece okunabilir kılacak; yerel PowerShell betiği yapısal bütünlüğü doğrulayacaktır.
- Sağlayıcıya özel Claude/Copilot adaptörleri ve CI otomasyonu ilk sürümün dışında kalacaktır.

## Değerlendirilen alternatifler

- Yalnız DOCX: Ajan erişimi ve metin tabanlı diff kalitesi zayıf olduğu için seçilmedi.
- DOCX ve Markdown'ı eşit kanonik tutmak: Manuel senkronizasyon sapma riski oluşturduğu için seçilmedi.
- Bütün bağlamı `AGENTS.md` içine koymak: Tekrar, yükleme maliyeti ve talimat sınırı nedeniyle seçilmedi.

## Sonuçlar

Ajanlar görevle ilgili bağlamı seçerek yükleyebilir ve mevcut-hedef ayrımını korur. Karşılığında kod değişikliklerinin dokümantasyon güncelleme matrisiyle birlikte yürütülmesi gerekir. Handoff ve mevcut durum belgeleri bakım yapılmazsa eskime riski taşır; bu risk manifest ve doğrulama süreciyle görünür hâle getirilir.

## Doğrulama

- Kök `AGENTS.md` 32 KiB altında kalır.
- Manifestteki bütün belgeler ve yerel Markdown bağlantıları çözülür.
- Kanonik spesifikasyon dokuz tablo, dört Mermaid diyagramı ve AC-01–AC-10 senaryolarını içerir.
- Taze bir ajan mevcut JWT prototipi ile hedef cookie Identity mimarisini birbirinden ayırabilir.
