---
title: "StockFlow Görev Handoff Kaydı"
status: rolling
authority: continuity
last_reviewed: "2026-08-24"
review_triggers:
  - meaningful-task-completed
  - blocker-discovered
  - validation-baseline-change
---

# StockFlow Görev Handoff Kaydı

## Son doğrulama

- Tarih: 24 Ağustos 2026
- Kapsam: xUnit ve izole SQL Server LocalDB test altyapısı

## Son tamamlanan değişiklik

`StockFlow.Tests`, EF Core InMemory yerine SQL Server provider kullanacak biçimde güncellendi. Veritabanına dokunan her test yalnız sabit `(localdb)\MSSQLLocalDB` örneğinde benzersiz `StockFlow_Tests_<guid>` veritabanı oluşturuyor, gerçek migration zincirini uyguluyor ve test sonunda güvenlik kontrolünden sonra siliyor. Test altyapısı uygulama yapılandırmasını ve dış connection string'i kabul etmiyor. İki Identity seed testi ilişkisel altyapıya taşındı; saf eksik yapılandırma testi veritabanı oluşturmadan çalışıyor. Hedef seçimi, migration smoke testi ve seed regresyonları dahil sekiz xUnit testi bulunuyor.

## Doğrulama kanıtı

- `dotnet restore StockFlow.slnx`: geçti.
- `dotnet build StockFlow.slnx --no-restore`: geçti; 0 hata ve 0 uyarı.
- `dotnet test StockFlow.slnx --no-restore`: sekiz test geçti; üç ilişkisel test ayrı geçici LocalDB veritabanlarında paralel çalıştı.
- Migration smoke testi iki migration'ın uygulandığını ve bekleyen migration kalmadığını doğruladı.
- Güvenlik testleri geliştirme veritabanı adını, alternatif LocalDB örneğini ve LocalDB dışı sunucuyu veri erişiminden önce reddetti.
- Test koşusu tamamlandığında `StockFlow_Tests_` önekli geçici veritabanı kalmadı.
- Ajan bağlamı, repository hygiene ve biçim kontrolleri geçti.

## Açık riskler ve boşluklar

- İş ekranlarında Admin/Employee rol matrisi ve role göre navigasyon henüz uygulanmamıştır.
- Service tabanlı kalıcı veri akışı ve kritik sipariş/stok xUnit testleri henüz uygulanmamıştır.
- Uygulamanın çalışması için migration sonrasında dört `IdentitySeed` değerinin güvenli yapılandırmada bulunması gerekir.
- İlişkisel testler Windows ve kurulu SQL Server LocalDB gerektirir; CI ve çapraz platform hedefi sonraki kapsamdadır.
- LocalDB geliştirme ve öğrenme ortamıdır; production veya çok kullanıcılı deployment için tam SQL Server hedefi ayrıca yapılandırılmalıdır.

## Sonraki sınırlandırılmış görev

Sipariş ve stok iş kurallarını `ApplicationDbContext` üzerinden yürüten Service katmanı oluşturulmalı; Draft/Confirm/Cancel, fiyat snapshot'ı ve atomik stok davranışları izole xUnit testleriyle kanıtlanmalıdır.
