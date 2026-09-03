---
title: "StockFlow Görev Handoff Kaydı"
status: rolling
authority: continuity
last_reviewed: "2026-09-03"
review_triggers:
  - meaningful-task-completed
  - blocker-discovered
  - validation-baseline-change
---

# StockFlow Görev Handoff Kaydı

## Son doğrulama

- Tarih: 3 Eylül 2026
- Kapsam: Sale/Purchase Draft sipariş oluşturma ve düzenleme MVC akışları

## Son tamamlanan değişiklik

Admin ve Employee için ortak Sale/Purchase Draft create/edit akışları `OrdersController` ve Razor ekranlarına bağlandı. Controller mevcut Order/Customer/Supplier/Product servislerini kullanır ve DbContext'e erişmez. Product Service'e yalnız `Id`/`Name`/`Sku` taşıyan seçim projection'ı eklendi; çok kalemli form türle eşleşen tarafı, dinamik ürün/miktar satırlarını, yerel dönüş URL'sini ve geçersiz dönüşlerde yeniden yüklenen seçimleri yönetir.

POST modelleri fiyat veya toplam taşımaz. Mevcut `OrderService` yeni satır fiyat snapshot'ı, kalan satır snapshot koruması ve toplam hesabının tek yetkili kaynağıdır. Create/update Draft durumunu korur; stok ve `StockMovement` değiştirmez. Eksik sipariş 404, terminal düzenleme yarışı 409, form/race doğrulamaları alan veya özet mesajı ve beklenmeyen tipli sonuçlar güvenli 500 üretir.

## Doğrulama kanıtı

- `dotnet format StockFlow.slnx --verify-no-changes --no-restore`: geçti.
- `dotnet build StockFlow.slnx --no-restore`: geçti; 0 hata ve 0 uyarı.
- Kullanıcıya bağlı gerçek LocalDB bağlamında 54 odaklı Order/Product/Controller/ViewModel testi geçti; başarısız veya atlanan test yoktur.
- 254 testlik tam paket geçti; başarısız veya atlanan test yoktur.
- Ajan bağlamı, repository hygiene ve `git diff --check` doğrulamaları geçti; yasaklı yerel/üretilmiş yol veya yüksek güvenli secret eşleşmesi bulunmadı.

## Açık riskler ve boşluklar

- Sipariş confirm/cancel/delete mutasyonları ve StockMovement query Service'i henüz Controller ve Razor akışlarına bağlanmamıştır.
- Uygulamanın çalışması için migration sonrasında dört `IdentitySeed` değerinin güvenli yapılandırmada bulunması gerekir.
- İlişkisel testler Windows ve çalışan SQL Server LocalDB gerektirir; CI ve çapraz platform hedefi sonraki kapsamdadır.
- LocalDB geliştirme ve öğrenme ortamıdır; production veya çok kullanıcılı deployment için tam SQL Server hedefi ayrıca yapılandırılmalıdır.

## Sonraki sınırlandırılmış görev

Admin için sipariş confirm/cancel/delete endpoint ve Razor akışlarını mevcut `IOrderService` sözleşmesine bağlamak.
