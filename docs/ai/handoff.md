---
title: "StockFlow Görev Handoff Kaydı"
status: rolling
authority: continuity
last_reviewed: "2026-08-31"
review_triggers:
  - meaningful-task-completed
  - blocker-discovered
  - validation-baseline-change
---

# StockFlow Görev Handoff Kaydı

## Son doğrulama

- Tarih: 31 Ağustos 2026
- Kapsam: Dashboard salt-okunur Service sözleşmesi, güvenli ViewModel'ler, DI ve odaklanmış ilişkisel testler

## Son tamamlanan değişiklik

`IDashboardService`/`DashboardService`, toplam Product, düşük stoklu Product, Customer, Supplier ve Order sayılarını; yalnız Confirmed Sale siparişlerin sunucuda üretilmiş `Order.TotalAmount` değerlerinden toplam satışı ve bütün tür/durumlardan son beş sipariş özetini güvenli ViewModel'lerle sunacak biçimde eklendi. Düşük stok ölçütü eşitlik dahil `StockQuantity <= MinimumStockQuantity` koşuludur. Product ve Order metrikleri aggregate projection, Customer/Supplier sayıları scalar sorgu, son siparişler `OrderDate DESC, Id DESC` kararlı sıralı doğrudan projection kullanır. Bütün sorgular aynı DbContext üzerinde sıralı ve `AsNoTracking` çalışır; boş veritabanında güvenli sıfırlar/boş koleksiyon döner ve kalıcı veri değiştirmez. Controller, Razor, entity, mapping ve migration değişmedi.

## Doğrulama kanıtı

- `dotnet format StockFlow.slnx --verify-no-changes --no-restore`: geçti.
- `dotnet build StockFlow.slnx --no-restore`: geçti; 0 hata ve 0 uyarı.
- Kullanıcıya bağlı gerçek LocalDB bağlamında hedefli dört `DashboardServiceTests` geçti; başarısız veya atlanan test yoktur.
- Aynı bağlamda `dotnet test StockFlow.slnx --no-build --no-restore` geçti; seksen yedi test başarılı, başarısız veya atlanan test yoktur.
- LocalDB kullanıcıya bağlı olduğundan ilişkisel testler izinli sahip kullanıcı bağlamında çalıştırıldı; InMemory veya başka sağlayıcı fallback'i kullanılmadı.
- Ajan bağlamı, 209 dosyalık repository hygiene, değişen sekiz dosyada hassas içerik/whitespace ve yazma sınırı taraması ile `git diff --check` doğrulamaları geçti.

## Açık riskler ve boşluklar

- İş ekranlarında Admin/Employee rol matrisi ve role göre navigasyon henüz uygulanmamıştır.
- Category, Product, Customer, Supplier, sipariş mutation/query, StockMovement query ve Dashboard Service'leri henüz Controller ve Razor akışlarına bağlanmamıştır.
- Uygulamanın çalışması için migration sonrasında dört `IdentitySeed` değerinin güvenli yapılandırmada bulunması gerekir.
- İlişkisel testler Windows ve çalışan SQL Server LocalDB gerektirir; CI ve çapraz platform hedefi sonraki kapsamdadır.
- LocalDB geliştirme ve öğrenme ortamıdır; production veya çok kullanıcılı deployment için tam SQL Server hedefi ayrıca yapılandırılmalıdır.

## Sonraki sınırlandırılmış görev

Belirlenmedi.
