---
title: "ADR-0002: İzole LocalDB Test Veritabanları"
status: Accepted
authority: decision
last_reviewed: "2026-08-24"
review_triggers:
  - test-database-change
  - test-execution-platform-change
  - persistence-provider-change
---

# ADR-0002: İzole LocalDB Test Veritabanları

- Tarih: 2026-08-24
- Durum: Accepted
- Karar sahipleri: Proje sahibi
- Etkilenen gereksinimler: NFR-TEST-01, NFR-TEST-03, AC-10
- Yerine geçtiği ADR: Yok
- Yerine geçen ADR: Yok

## Bağlam

Service katmanındaki sipariş ve stok kuralları SQL Server constraint, migration ve transaction davranışlarına bağlı olacaktır. EF Core InMemory sağlayıcısı test verisini süreç içinde ayırsa da ilişkisel constraint'leri, gerçek SQL üretimini ve SQL Server transaction davranışını kanıtlamaz. Testlerin geliştirme veya production verisine bağlanması ise kabul edilemez veri kaybı ve test sırası riski oluşturur.

İlk geliştirme hedefi Windows'tur ve proje geliştirme için SQL Server LocalDB kullanır. Docker, harici test sunucusu ve çapraz platform CI bu aşamanın kapsamı dışındadır.

## Karar

- Veritabanına dokunan xUnit testleri yalnız `(localdb)\MSSQLLocalDB` üzerinde çalışacaktır.
- Her test `StockFlow_Tests_<guid>` biçiminde benzersiz bir veritabanı oluşturacak, mevcut EF Core migration zincirini uygulayacak ve test sonunda veritabanını silecektir.
- Test altyapısı uygulamanın `DefaultConnection` ayarını, User Secrets değerlerini veya ortam bağlantılarını okumayacak ve dışarıdan connection string kabul etmeyecektir.
- Migration ve silme öncesinde sabit LocalDB örneği, zorunlu veritabanı adı öneki ve GUID son eki doğrulanacaktır.
- Test bağlantısı Windows tümleşik kimlik doğrulaması kullanacak, kimlik bilgisi içermeyecek ve güvenilir temizlik için connection pooling'i kapatacaktır.
- Veritabanı gerektirmeyen seçenek doğrulama testleri LocalDB veritabanı oluşturmadan çalışacaktır.
- LocalDB kullanılamıyorsa testler InMemory veya geliştirme bağlantısına geri dönmeden açık biçimde başarısız olacaktır.

## Değerlendirilen alternatifler

- EF Core InMemory: Hızlı ve platform bağımsızdır; ilişkisel constraint, migration ve transaction davranışını temsil etmediği için Service testlerinin ana sağlayıcısı olarak seçilmedi.
- Test koşusu veya sınıf başına paylaşılan veritabanı: Daha hızlıdır; reset altyapısı, paralellik ve test sırası bağımlılığı oluşturduğu için seçilmedi.
- Harici SQL Server veya Docker: CI ve çapraz platform için daha esnektir; ek secret, servis ve işletim kapsamı gerektirdiği için ilk aşamaya alınmadı.

## Sonuçlar

- Service testleri production sağlayıcısına yakın ilişkisel davranışla çalışır ve geliştirme/production verisinden veritabanı düzeyinde ayrılır.
- Her test kendi veritabanını kullandığı için test sırası ve xUnit paralelliği sonucu değiştirmez.
- Migration çalıştırma ve veritabanı oluşturup silme, InMemory testlerine göre daha yavaştır.
- İlk test altyapısı Windows ve kurulu SQL Server LocalDB gerektirir; CI veya çapraz platform ihtiyacında bu ADR gözden geçirilmelidir.

## Doğrulama

- Güvenlik testleri geliştirme veritabanı adını ve LocalDB dışı sunucu hedeflerini reddeder.
- Smoke testi geçici veritabanında bütün migration'ların uygulandığını ve bekleyen migration kalmadığını doğrular.
- Identity seed ilişkisel testleri ayrı veritabanlarında çalışır ve tam test koşusu sonunda `StockFlow_Tests_` önekli veritabanı kalmaz.
- `dotnet test StockFlow.slnx --no-restore` sekiz testi başarıyla tamamlar.
