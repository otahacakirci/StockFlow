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
- Kapsam: StockMovement salt-okunur sorgu Service sözleşmesi, güvenli ViewModel'ler, DI ve odaklanmış ilişkisel testler

## Son tamamlanan değişiklik

`IStockMovementQueryService`/`StockMovementQueryService`, Product/Order kimliği, StockIn/StockOut türü ve inclusive UTC takvim günü aralığı filtreleriyle eklendi. Listeleme whitelist tarih sıralaması, kararlı kimlik tie-breaker'ı ve 20 varsayılan/100 üst sınırlı normalize sayfalama kullanır. Liste/detay projection'ları Product adı/SKU'su, OrderNumber, pozitif miktar, açıklama ve sıfır offset'li UTC hareket tarihini güvenli ViewModel'lerle taşır. Ters tarih aralığı `Validation`, eksik hareket `NotFound` döndürür. Bütün sorgular `AsNoTracking` kullanır ve kalıcı veri değiştirmez; StockMovement/Product stok yazımı yalnız mevcut `OrderService` confirm transaction'ında kalır. Controller, Razor, entity, mapping ve migration değişmedi.

## Doğrulama kanıtı

- `dotnet format StockFlow.slnx --verify-no-changes --no-restore`: geçti.
- `dotnet build StockFlow.slnx --no-restore`: geçti; 0 hata ve 0 uyarı.
- Kullanıcıya bağlı gerçek LocalDB bağlamında hedefli yedi `StockMovementQueryServiceTests` ve StockMovement sorguları ile OrderService/planner kapsamındaki yirmi bir test geçti; başarısız veya atlanan test yoktur.
- Aynı bağlamda `dotnet test StockFlow.slnx --no-build --no-restore` geçti; seksen üç test başarılı, başarısız veya atlanan test yoktur.
- LocalDB kullanıcıya bağlı olduğundan ilişkisel testler izinli sahip kullanıcı bağlamında çalıştırıldı; InMemory veya başka sağlayıcı fallback'i kullanılmadı.
- Ajan bağlamı, 201 dosyalık repository hygiene, değişen on bir dosyada hassas içerik/whitespace ve yazma sınırı taraması ile `git diff --check` doğrulamaları geçti.

## Açık riskler ve boşluklar

- İş ekranlarında Admin/Employee rol matrisi ve role göre navigasyon henüz uygulanmamıştır.
- Category, Product, Customer, Supplier, sipariş mutation/query ve StockMovement query Service'leri henüz Controller ve Razor akışlarına bağlanmamıştır.
- Uygulamanın çalışması için migration sonrasında dört `IdentitySeed` değerinin güvenli yapılandırmada bulunması gerekir.
- İlişkisel testler Windows ve çalışan SQL Server LocalDB gerektirir; CI ve çapraz platform hedefi sonraki kapsamdadır.
- LocalDB geliştirme ve öğrenme ortamıdır; production veya çok kullanıcılı deployment için tam SQL Server hedefi ayrıca yapılandırılmalıdır.

## Sonraki sınırlandırılmış görev

Belirlenmedi.
