---
title: "ADR-0009: Testcontainers MSSQL ile İzole Test Veritabanları"
status: Accepted
authority: decision
last_reviewed: "2026-11-20"
review_triggers:
  - test-database-change
  - test-execution-platform-change
  - persistence-provider-change
---

# ADR-0009: Testcontainers MSSQL ile İzole Test Veritabanları

- Tarih: 2026-11-20
- Durum: Accepted
- Karar sahipleri: Proje sahibi ([issue #9](https://github.com/otahacakirci/StockFlow/issues/9))
- Etkilenen gereksinimler: NFR-TEST-01, NFR-TEST-03, AC-10; [product-spec.md](../product-spec.md) › 3.1 Tablo 1a (Testcontainers satırı), › 10.1 Faz 0
- Yerine geçtiği ADR: [ADR-0002](0002-isolated-localdb-test-databases.md)
- Yerine geçen ADR: Yok

## Bağlam

- [ADR-0002](0002-isolated-localdb-test-databases.md), veritabanına dokunan testleri `(localdb)\MSSQLLocalDB` ile sınırladı. Bu karar testleri Windows'a ve kurulu LocalDB'ye bağladı. ADR-0002, çapraz platform veya CI ihtiyacında yeniden gözden geçirilmesini kendisi öngörmüştü.
- [product-spec.md](../product-spec.md) › 10.1 Faz 0 (`Açık`) Testcontainers (MSSQL) ve Ubuntu üzerinde çalışan CI ister. LocalDB Linux'ta yoktur.
- ADR-0002'nin değerli kısımları korunmalıdır:
  - Gerçek SQL Server davranışı: constraint, migration ve transaction.
  - Test başına izolasyon ve xUnit paralelliği.
  - Testlerin geliştirme veya production verisine asla bağlanmaması.
  - InMemory, SQLite ya da geliştirme veritabanına geri dönüş yasağı.
- Kısıtlar:
  - Kaynak kodda secret bulunmaz (`.claude/rules/security.md`).
  - Test altyapısı uygulamanın bağlantı ayarlarını veya User Secrets değerlerini okumaz.
  - `StockFlowWebApplicationFactory` ve `Program.cs` değişmemelidir.

## Karar

1. **Konteyner:** Veritabanına dokunan xUnit testleri, test koşusu başına tek bir SQL Server konteynerinde çalışır (`StockFlow.Tests/Infrastructure/MsSqlTestServer.cs`).
   - Konteyner `Testcontainers.MsSql` paketiyle (4.16.0) açılır.
   - İmaj etiketi sabittir: `mcr.microsoft.com/mssql/server:2022-CU28-ubuntu-22.04` (`MsSqlTestServer.cs:8`). `latest` kullanılmaz. Etiket yalnız bu ADR'yi güncelleyen bir değişiklikle yükseltilir.
   - Başlatma tembeldir ve yalnız bir kez yapılır (`Lazy<Task<…>>`, `ExecutionAndPublication`; `:11-13`). Konteyner ilk veritabanı testinde açılır; DB'siz testler Docker'a dokunmaz.
   - Konteyner yeniden kullanımı (`WithReuse`) kapalıdır. Koşu sonunda konteyneri Testcontainers kaynak toplayıcısı (Ryuk) kaldırır.
2. **İzolasyon:** Her test `StockFlow_Tests_<guid>` biçiminde benzersiz bir veritabanı oluşturur, mevcut EF Core migration zincirini uygular ve test sonunda veritabanını siler. Bu, ADR-0002'deki gibidir.
3. **Eşzamanlı oluşturma:** Tek SQL Server örneğinde eşzamanlı `CREATE DATABASE` çağrıları hata 1807 ile başarısız olabilir. Bu yüzden veritabanı oluşturma süreç içi bir semafor altında sıraya alınır. Migration'lar semafor dışında paralel koşar (`StockFlow.Tests/Infrastructure/SqlServerTestDatabase.cs:16`, `:143-155`).
4. **Hedef doğrulaması:** Migration ve silme öncesinde şunlar doğrulanır:
   - Hedef yalnız bu koşunun konteyner adresi olabilir.
   - Veritabanı adı zorunlu öneki ve GUID son ekini taşımalıdır.

   LocalDB, yabancı host ve farklı port reddedilir (`SqlServerTestDatabase.cs:51-66`).
5. **Kimlik bilgisi:**
   - Bağlantı, koşu başına çalışma anında üretilen `sa` parolasını kullanır (`MsSqlTestServer.cs:71-75`). Parola kaynak kodda, kaynak kontrollü yapılandırma dosyalarında, hata mesajlarında veya logda bulunmaz. Yalnız süreç belleğinde ve MVC test hostunun bellek içi yapılandırmasında yaşar (`StockFlow.Tests/Integration/StockFlowWebApplicationFactory.cs:23`, `:56`).
   - Uç nokta tipi `record` değildir, böylece `ToString()` parolayı yazdırmaz (`:78-85`).
   - Bağlantı tümleşik kimlik doğrulama kullanmaz. Güvenilir temizlik için connection pooling kapalıdır (`SqlServerTestDatabase.cs:206-239`).
6. **Dış girdi yok:** Test altyapısı uygulamanın `DefaultConnection` ayarını, User Secrets değerlerini veya ortam bağlantılarını okumaz ve dışarıdan connection string kabul etmez. Testcontainers yalnız Docker'a erişim için kendi standart değişkenlerini (`DOCKER_HOST`, `TESTCONTAINERS_*`) okur. Erişilemeyen bir `DOCKER_HOST` hata vermez; Testcontainers otomatik keşfettiği başka bir uç noktaya, örneğin Docker Desktop named pipe'ına, düşebilir. Bu davranış Windows'ta `DOCKER_HOST=tcp://127.0.0.1:1` ile gözlendi. Hedef doğrulaması koşunun başlattığı konteynere sabit olduğundan bu durum veri güvenliğini etkilemez.
7. **DB'siz yol:** Veritabanı gerektirmeyen seçenek doğrulama testleri konteyner başlatmaz. Bu testler kimlik bilgisi içermeyen, çözümlenemeyen sabit bir adres kullanır: `stockflow-tests-uninitialized.invalid` (`SqlServerTestDatabase.cs:14`, `:68-99`).
8. **Ortam hatası:** Docker kurulu değilse veya konteyner başlamazsa veritabanı testleri anında ve açık bir Türkçe "ortam hatası, testler geçmiş sayılmaz" mesajıyla başarısız olur (`MsSqlTestServer.cs:22-29`). Hiçbir test atlanmaz.
9. **Yedek yol yok:** LocalDB, EF Core InMemory, SQLite veya geliştirme veritabanına geri dönüş yoktur. LocalDB yalnız uygulamanın yerel geliştirme veritabanı olarak kalır.
10. **Devralma:** ADR-0002'nin "gerçek SQL Server; InMemory/SQLite/geliştirme veritabanı yasağı" kuralı bu ADR'ye aynen geçer. ADR-0004–0008'in ADR-0002'ye yaptığı bu yöndeki atıflar bu ADR üzerinden geçerli kalır.

## Değerlendirilen alternatifler

- **LocalDB'yi korumak:** Kurulum gerektirmez. Seçilmedi, çünkü Linux'ta yoktur ve Faz 0'ın Ubuntu CI kapısını karşılayamaz.
- **LocalDB + Docker çift yol** (ortam değişkeniyle seçim): Windows'ta Docker'sız çalışmaya izin verir. Seçilmedi:
  - İki altyapının bakımını gerektirir.
  - İki motor arasında davranış farkı (collation, sürüm) gizlenir.
  - Sessiz geri dönüş riski doğar.
- **Test başına konteyner:** Tam izolasyon sağlar. Seçilmedi, çünkü her konteyner başlatması saniyeler ve yaklaşık 2 GB bellek ister; 300'ü aşkın testte pratik değildir.
- **Paylaşılan veritabanı + Respawn ile sıfırlama:** Daha hızlıdır. Seçilmedi, çünkü ek paket gerektirir, test sırası ve paralellik bağımlılığı oluşturur. ADR-0002 de paylaşılan veritabanını reset altyapısı, paralellik ve test sırası bağımlılığı nedeniyle reddetmişti.
- **Harici/paylaşılan SQL Server:** Seçilmedi, çünkü ek secret, servis ve işletim kapsamı gerektirir ve geliştiriciler arasında paylaşılan duruma yol açar.

## Sonuçlar

- Olumlu:
  - Testler Windows'ta Docker Desktop (Linux konteyner modu) ve Linux'ta Docker Engine ile aynı motor ve sürümle çalışır.
  - Faz 0 CI'ı (Ubuntu) mümkün olur.
  - Test başına izolasyon ve xUnit paralelliği korunur.
  - `StockFlowWebApplicationFactory` ve `Program.cs` değişmez.
- Maliyetler ve kabul edilen riskler:
  - Testler için Docker zorunludur. Docker yoksa ilişkisel testler koşmaz; bu durum ortam hatası olarak raporlanır.
  - MSSQL imajı yalnız `linux/amd64` için yayımlanır. ARM/Apple Silicon makineler desteklenmez.
  - İlk koşu imaj indirmesini içerir ve yavaştır. Konteyner yaklaşık 2 GB bellek ister.
  - Konteynerin varsayılan collation'ı `SQL_Latin1_General_CP1_CI_AS`'tir. Türkçe Windows'taki LocalDB'den farklı olabilir. Bugünkü bütün testler bu collation'da geçer. Collation'a duyarlı yeni testler bu farkı hesaba katar.
  - Temizlik Ryuk'a bağlıdır. `TESTCONTAINERS_RYUK_DISABLED=true` ayarlanırsa konteyner koşudan sonra kalır ve elle kaldırılmalıdır.
  - Developer edition imajı `ACCEPT_EULA=Y` ister. Bu kabul yalnız test kullanımı için proje sahibince verilmiştir.

## Doğrulama

- Güvenlik testleri şunları doğrular: LocalDB, yabancı host ve farklı port reddedilir; geliştirme veritabanı adı reddedilir; konteyner adresi kabul edilir; başlatılmamış veritabanına erişim reddedilir; başlatma hatası ortam hatası mesajına sarılır (`StockFlow.Tests/Infrastructure/SqlServerTestDatabaseSafetyTests.cs`).
- İzolasyon testi aynı anda sekiz veritabanı oluşturur. Hiçbiri hata 1807 vermez, veritabanları birbirinin verisini görmez ve dispose sonrası adlar kalmaz (`SqlServerTestDatabaseIsolationTests.cs`).
- Smoke testleri konteynerde bütün migration'ların uygulandığını, bekleyen migration kalmadığını ve hedefin LocalDB olmadığını doğrular (`SqlServerTestDatabaseSmokeTests.cs`).
- Windows ve Docker Desktop üzerinde `dotnet test StockFlow.slnx --no-restore` 311 testi 0 başarısız ve 0 atlanan ile tamamlar. Koşu sonrası mssql veya Ryuk konteyneri kalmaz.
- Ubuntu 24.04 üzerinde aynı komut 311 testi 0 başarısız ve 0 atlanan ile tamamlar. Ortam: `mcr.microsoft.com/dotnet/sdk:10.0-noble` konteyneri, x86_64, host Docker soketi bağlı, `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`. Yerel Docker Engine üzerinde koşu Faz 0 CI'ı (#10) ile kalıcı olarak doğrulanacaktır.
- Docker'a erişim yokken (aynı Ubuntu konteyneri, soket bağlı değil) 98 veritabanı testi bir saniyenin altında ortam hatası mesajıyla başarısız olur. 213 DB'siz test yeşil kalır, hiçbir test atlanmaz.
