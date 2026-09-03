---
title: "StockFlow Görev Handoff Kaydı"
status: rolling
authority: continuity
last_reviewed: "2026-09-02"
review_triggers:
  - meaningful-task-completed
  - blocker-discovered
  - validation-baseline-change
---

# StockFlow Görev Handoff Kaydı

## Son doğrulama

- Tarih: 2 Eylül 2026
- Kapsam: Service katmanında davranış koruyan ortak politika ve hata sahipliği refaktörü

## Son tamamlanan değişiklik

Altı liste Service'indeki sayfa varsayılanı/üst sınırı, toplam sayfa, boş sonuç ve taşan sayfa davranışı `internal` `ListPagingPolicy` içinde birleştirildi. Customer ve Supplier'ın ortak iletişim normalizasyon/doğrulaması `ContactInformationPolicy` içine alındı; domain'e özgü ad, sorgu, projection, hata ve silme davranışları ayrı bırakıldı.

Category, Product, Customer, Supplier ve Order'ın transaction dışı kayıtları, mevcut domain log mesajlarını koruyan callback'lerle `TrackedPersistence` üzerinden tracker temizleyip exception'ı yeniden fırlatır. Order confirm transaction'ı değişmedi. Çapraz-domain hata sabitleri kanonik Category/Customer/Supplier/Product kataloglarına bağlandı; mevcut public sabit adları ve kararlı string değerleri alias olarak korundu. Public Service/ViewModel sözleşmesi, HTTP/rol davranışı, veri modeli ve ürün kuralları değişmedi.

## Doğrulama kanıtı

- `dotnet format StockFlow.slnx --verify-no-changes --no-restore`: geçti.
- `dotnet build StockFlow.slnx --no-restore`: geçti; 0 hata ve 0 uyarı.
- On dört veritabanısız ortak politika/planner testi geçti; başarısız veya atlanan test yoktur.
- Kullanıcıya bağlı gerçek LocalDB bağlamında 86 odaklı Service regresyonu ve 242 testlik tam paket geçti; başarısız veya atlanan test yoktur.
- Ajan bağlamı, repository hygiene ve `git diff --check` doğrulamaları geçti; yasaklı yerel/üretilmiş yol veya yüksek güvenli secret eşleşmesi bulunmadı.

## Açık riskler ve boşluklar

- Sipariş mutation ve StockMovement query Service'leri henüz Controller ve Razor akışlarına bağlanmamıştır.
- Uygulamanın çalışması için migration sonrasında dört `IdentitySeed` değerinin güvenli yapılandırmada bulunması gerekir.
- İlişkisel testler Windows ve çalışan SQL Server LocalDB gerektirir; CI ve çapraz platform hedefi sonraki kapsamdadır.
- LocalDB geliştirme ve öğrenme ortamıdır; production veya çok kullanıcılı deployment için tam SQL Server hedefi ayrıca yapılandırılmalıdır.

## Sonraki sınırlandırılmış görev

Sipariş Draft oluşturma/düzenleme mutasyon akışlarından önce StockMovement salt-okunur MVC listeleme ve detay ekranlarını mevcut query Service'e bağlamak.
