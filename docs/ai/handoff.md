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
- Kapsam: Sipariş salt-okunur sorgu Service sözleşmesi, güvenli ViewModel'ler, DI ve odaklanmış ilişkisel testler

## Son tamamlanan değişiklik

`IOrderQueryService`/`OrderQueryService`, mevcut `IOrderService`/`OrderService` mutasyon davranışını değiştirmeden eklendi. Liste sorgusu `OrderType`/`OrderStatus` filtrelerini, whitelist tarih sıralamasını ve 20 varsayılan/100 üst sınırlı normalize sayfalamayı uygular; liste, detay ve Draft edit sonuçları Customer/Supplier/OrderItem/Product verilerini güvenli ViewModel'lere doğrudan project eder. Bütün sorgular `AsNoTracking` kullanır, kalıcı veri değiştirmez ve seçim listelerini mevcut Customer/Supplier Service'lerinde bırakır. Eksik sipariş `NotFound`, terminal sipariş için edit sorgusu `BusinessRule` döndürür. Controller, Razor, entity, mapping ve migration değişmedi.

## Doğrulama kanıtı

- `dotnet format StockFlow.slnx --verify-no-changes --no-restore`: geçti.
- `dotnet build StockFlow.slnx --no-restore`: geçti; 0 hata ve 0 uyarı.
- Kullanıcıya bağlı gerçek LocalDB bağlamında hedefli sekiz `OrderQueryServiceTests` ve sorgu/mutation/planner kapsamındaki yirmi iki sipariş testi geçti; başarısız veya atlanan test yoktur.
- Aynı bağlamda `dotnet test StockFlow.slnx --no-build --no-restore` geçti; yetmiş altı test başarılı, başarısız veya atlanan test yoktur.
- LocalDB'nin kullanıcıya bağlı olması nedeniyle sandbox hesabı otomatik örneği oluşturamadı; izinli sahip kullanıcı bağlamında aynı SQL Server testleri fallback kullanılmadan tamamlandı.
- Ajan bağlamı, 190 dosyalık repository hygiene, değişen on dört dosyada ek hassas içerik taraması ve `git diff --check` doğrulamaları geçti.

## Açık riskler ve boşluklar

- İş ekranlarında Admin/Employee rol matrisi ve role göre navigasyon henüz uygulanmamıştır.
- Category, Product, Customer, Supplier ve sipariş mutation/query Service'leri henüz Controller ve Razor akışlarına bağlanmamıştır.
- Uygulamanın çalışması için migration sonrasında dört `IdentitySeed` değerinin güvenli yapılandırmada bulunması gerekir.
- İlişkisel testler Windows ve çalışan SQL Server LocalDB gerektirir; CI ve çapraz platform hedefi sonraki kapsamdadır.
- LocalDB geliştirme ve öğrenme ortamıdır; production veya çok kullanıcılı deployment için tam SQL Server hedefi ayrıca yapılandırılmalıdır.

## Sonraki sınırlandırılmış görev

Belirlenmedi.
