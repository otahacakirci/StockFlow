---
title: "StockFlow Görev Handoff Kaydı"
status: rolling
authority: continuity
last_reviewed: "2026-08-26"
review_triggers:
  - meaningful-task-completed
  - blocker-discovered
  - validation-baseline-change
---

# StockFlow Görev Handoff Kaydı

## Son doğrulama

- Tarih: 26 Ağustos 2026
- Kapsam: `OrderStockConfirmationPlanner` ayrıştırması ve davranış-korumalı `OrderService` refactor'ı

## Son tamamlanan değişiklik

Sale/Purchase yeni stok hesabı, somut scoped kaydedilen ve `OrderService` constructor'ına verilen bağımlılıksız `internal sealed OrderStockConfirmationPlanner` sınıfına taşındı. Planner yalnız sayısal satır girdilerinden hareket türü ve yeni stok miktarlarını ya da mevcut hata koduyla ProductId/requested/available bağlamını üretir; EF entity'si, logger veya kalıcılık bağımlılığı taşımaz. `OrderService`, planı transaction içindeki kalıcı Draft doğrulamasından sonra çağırır; yapılandırılmış hata logu, tracked Product mutation'ı, `StockMovement` üretimi, UTC tarih/açıklama, terminal durum, tek `SaveChangesAsync`, commit, rollback ve change-tracker temizliği Service içinde kalır. `BuildValidatedDraft` adı, doğrulama yaptığını açıkça göstermek üzere `ValidatePricesAndBuildDraft` olarak netleştirildi. Dış Service sözleşmeleri, hata kategorileri/kodları, kullanıcı mesajları ve iş kuralları değişmedi.

## Doğrulama kanıtı

- `dotnet build StockFlow.slnx --no-restore`: geçti; 0 hata ve 0 uyarı.
- `dotnet test ... --list-tests`: geçti; sekiz altyapı/Identity, on ilişkisel Service ve dört saf planner testi olmak üzere yirmi iki test keşfedildi.
- Dört hedefli planner testi ve LocalDB gerektirmeyen dokuz testlik paket geçti.
- Hedefli `OrderServiceTests` ve tam `dotnet test StockFlow.slnx --no-build --no-restore` koşuları başlatıldı; sabit `(localdb)\MSSQLLocalDB` otomatik örneği oluşturulamadığı için ilişkisel test sınıfları kurulumda başarısız oldu. Aynı çevresel zaman aşımını kalan testlerde tekrarlamamak için koşular kesin hata kanıtından sonra durduruldu.
- `sqllocaldb start MSSQLLocalDB` aynı runtime hatasıyla başarısız oldu. InMemory, geliştirme veritabanı veya dış connection string fallback'i kullanılmadı.
- `dotnet format ... --verify-no-changes`, ajan bağlamı, repository hygiene ve `git diff --check` doğrulamaları geçti.

## Açık riskler ve boşluklar

- İş ekranlarında Admin/Employee rol matrisi ve role göre navigasyon henüz uygulanmamıştır.
- Sipariş/stok Service'i henüz Controller ve Razor akışlarına bağlanmamıştır.
- Uygulamanın çalışması için migration sonrasında dört `IdentitySeed` değerinin güvenli yapılandırmada bulunması gerekir.
- Bu makinedeki `MSSQLLocalDB` örneği otomatik başlatılamadığı için yeni ve mevcut ilişkisel testlerin başarılı çalışma zamanı kanıtı alınamamıştır; LocalDB runtime onarımı repo kapsamı dışındadır.
- İlişkisel testler Windows ve çalışan SQL Server LocalDB gerektirir; CI ve çapraz platform hedefi sonraki kapsamdadır.
- LocalDB geliştirme ve öğrenme ortamıdır; production veya çok kullanıcılı deployment için tam SQL Server hedefi ayrıca yapılandırılmalıdır.

## Sonraki sınırlandırılmış görev

Çalışan bir `MSSQLLocalDB` ortamında yirmi iki testlik tam paket çalıştırılmalı; ardından sipariş Service'i rol korumalı ince MVC Controller ve Razor akışlarına bağlama görevi ayrıca ele alınmalıdır.
