---
title: "StockFlow Görev Handoff Kaydı"
status: rolling
authority: continuity
last_reviewed: "2026-08-30"
review_triggers:
  - meaningful-task-completed
  - blocker-discovered
  - validation-baseline-change
---

# StockFlow Görev Handoff Kaydı

## Son doğrulama

- Tarih: 30 Ağustos 2026
- Kapsam: Customer yönetim Service sözleşmeleri, güvenli ViewModel'ler, sipariş geçmişi koruması, DI ve odaklanmış testler

## Son tamamlanan değişiklik

`ICustomerService`/`CustomerService`; ad/e-posta/telefon aramalı, whitelist ad sıralamalı, 20 varsayılan/100 üst sınırlı sayfalı listeleme; sipariş sayılı detay; doğrulamalı oluşturma/düzenleme ve sipariş geçmişi korumalı silme akışlarıyla eklendi. Salt-okunur sorgular `AsNoTracking` projection kullanır; sipariş formları için yalnız `Id`/`Name` taşıyan ayrı seçim verisi üretilir. Alanlar mapping sınırlarıyla doğrulanıp trimlenir, boş opsiyonel iletişim/adres alanları `null` yapılır. Herhangi bir Draft, Confirmed veya Cancelled Order fiziksel silmeyi `BusinessRule` ile engeller. Beklenmeyen kalıcılaştırma hataları kişisel veri loglanmadan kaydedilir, tracker temizlenir ve yeniden fırlatılır.

## Doğrulama kanıtı

- `dotnet format StockFlow.slnx --no-restore` ve `--verify-no-changes`: geçti.
- `dotnet build StockFlow.slnx --no-restore`: geçti; 0 hata ve 0 uyarı.
- Kullanıcıya bağlı gerçek LocalDB bağlamında hedefli on üç `CustomerServiceTests` geçti; başarısız veya atlanan test yoktur.
- Aynı bağlamda `dotnet test StockFlow.slnx --no-build --no-restore` geçti; elli beş test başarılı, başarısız veya atlanan test yoktur.
- LocalDB'nin kullanıcıya bağlı olması nedeniyle sandbox hesabındaki ilk tanı örneği göremedi; izinli sahip kullanıcı bağlamında örnek doğrulandı ve bütün ilişkisel testler fallback kullanılmadan tamamlandı.
- Ajan bağlamı, repository hygiene, on yeni Customer dosyası için ek hassas içerik taraması ve `git diff --check` doğrulamaları geçti.

## Açık riskler ve boşluklar

- İş ekranlarında Admin/Employee rol matrisi ve role göre navigasyon henüz uygulanmamıştır.
- Category, Product, Customer ve sipariş/stok Service'leri henüz Controller ve Razor akışlarına bağlanmamıştır.
- Uygulamanın çalışması için migration sonrasında dört `IdentitySeed` değerinin güvenli yapılandırmada bulunması gerekir.
- İlişkisel testler Windows ve çalışan SQL Server LocalDB gerektirir; CI ve çapraz platform hedefi sonraki kapsamdadır.
- LocalDB geliştirme ve öğrenme ortamıdır; production veya çok kullanıcılı deployment için tam SQL Server hedefi ayrıca yapılandırılmalıdır.

## Sonraki sınırlandırılmış görev

Supplier yönetim Service katmanı ayrı ve sınırlı bir görev olarak ele alınmalıdır.
