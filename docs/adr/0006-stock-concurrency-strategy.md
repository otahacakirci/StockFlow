---
title: "ADR-0006: Stok Eşzamanlılık Stratejisi: Sıralı Pesimistik Kilit ve Order RowVersion"
status: Accepted
authority: decision
last_reviewed: "2026-11-20"
review_triggers:
  - concurrency-strategy-change
  - phase-gate-change
  - database-provider-change
---

# ADR-0006: Stok Eşzamanlılık Stratejisi: Sıralı Pesimistik Kilit ve Order RowVersion

- Tarih: 2026-11-20
- Durum: Accepted
- Karar sahipleri: Proje sahibi ([issue #4](https://github.com/otahacakirci/StockFlow/issues/4))
- Etkilenen gereksinimler: FR-STOCK-04, FR-STOCK-07, BR-ATOM-01, BR-ATOM-04..06, BR-STATE-01/02, NFR-ERR-01/02, FR-UX-02; [product-spec.md](../product-spec.md) › 7.1, › 10.1 Faz 1, › 10.3 "Optimistic concurrency (Faz 1)"
- Yerine geçtiği ADR: Yok
- Yerine geçen ADR: Yok

## Bağlam

- Sipariş onayı varsayılan `READ COMMITTED` altında, kilit ipucu olmadan siparişi, kalemleri ve ürünleri okur (`StockFlow/Services/Orders/OrderService.cs:151`, `:155-158`). Yeni stoğu bellekteki değerden hesaplayıp mutlak değer olarak yazar (`:420`). `Order` ve `Product` üzerinde concurrency token yoktur. Tek `SaveChangesAsync` ve commit ile biter (`:196-197`).
- `CancelDraftAsync`, `UpdateDraftAsync` ve `DeleteDraftAsync` transaction veya kilit kullanmaz. Durumu kilitsiz okur, sonra yazar (`OrderService.cs:96-142`, `:227-250`, `:255-300`). Toplamı ve sipariş tarafı değişmeyen, yalnız kalemleri değişen bir taslak düzenlemesi Order satırı için `UPDATE` üretmez (`:327-328`).
- `CK_Products_StockQuantity_NonNegative` stoğun negatife düşmesini engeller. Negatif olmayan kayıp güncellemeyi ise engellemez: stok 10 iken iki 3'lük satış eşzamanlı onaylanırsa ikisi de 7 yazar.
- Bugün eşzamanlı isteklerde üç risk vardır:
  - Aynı ürünü içeren iki Sale'de kayıp güncelleme (overselling).
  - Aynı taslağın çift onayı: stok bir kez düşer ama iki hareket kümesi yazılır.
  - Confirm ile Cancel, Update veya Delete yarışı: sonuçta hareketli bir `Cancelled` sipariş kalabilir ya da `Restrict` FK ihlali beklenmeyen 500 döner.
- **Bilinen risk:** BR-ATOM-06 ve BR-STATE-01/02 bugün yalnız ardışık isteklerde sağlanır. Düzeltme [product-spec.md](../product-spec.md) › 10.1 Faz 1 kapsamındadır. Bu ADR'nin kabulü Faz 1 kapısının SCOPE-03 (b) koşuludur.
- Kısıtlar:
  - Tek SQL Server veritabanı ve tek atomik onay transaction'ı (spec › 7.1).
  - Saf planner kararı ve tek `SaveChangesAsync` değişmezi (`.claude/rules/orders-stock.md`).
  - Faz 1'de tüm yetersiz stok kalemleri raporlanmalıdır.
  - Testler gerçek SQL Server ister ([ADR-0002](0002-isolated-localdb-test-databases.md)).
  - Redis, mesajlaşma ve dağıtık kilit kalıcı kapsam dışıdır ([ADR-0004](0004-product-spec-v2-phase-gated-scope.md), spec › 10.3).

## Karar

1. **İzolasyon:** Sipariş onayı ve sipariş kümesini değiştiren her işlem, açık bir transaction içinde ve açıkça belirtilmiş `IsolationLevel.ReadCommitted` ile çalışır. Doğruluğu izolasyon seviyesi değil, aşağıdaki açık kilitler sağlar. `UPDLOCK` ipucu satır sürümünü değil son commit edilmiş satırı kilitleyerek okuduğu için strateji, veritabanında `READ_COMMITTED_SNAPSHOT` açılsa da geçerli kalır.
2. **Pesimistik satır kilitleri (`UPDLOCK, ROWLOCK`):** Onay şu sırayla ilerler:
   - (a) Order satırını `UPDLOCK, ROWLOCK` ile okur. Durum ve RowVersion kontrolü bu kilitli okumadan sonra yapılır.
   - (b) Kalemleri okur.
   - (c) Kalemlerdeki ürünleri **ProductId artan sırada, her ürün için ayrı bir kilitli okumayla** kilitler. SQL Server tek ifadede kilit alma sırasını garanti etmediği için tek bir `IN (...)` sorgusu kullanılmaz.
   - (d) Planner'ı yalnız kilit altında okunan taze stoklarla çalıştırır.
   - (e) Planı tracked entity'lere uygular; tek `SaveChangesAsync` ve commit ile bitirir.

   Kilitler commit veya rollback'e kadar tutulur. Kilitli okuma parametreli `FromSql` ile yapılır, string birleştirme kullanılmaz. Ürünler kilitten önce tracker'a yüklenmez (`Include(...Product)` kullanılmaz). Önceden izlenen bir entity'yi yeniden sorgulamak değerlerini yenilemez ve bayat stok okunur.
3. **Kilit sırası (deadlock önleme kuralı):** Bir transaction içinde kilit sırası her zaman **Order satırı → OrderItems → Products (artan `Id`) → StockMovement ekleme** şeklindedir.
   - Order'ı veya kalemlerini değiştiren her işlem (Confirm, UpdateDraft, Cancel, DeleteDraft) önce Order satırını `UPDLOCK` ile alır.
   - Ürün kilitleyen her yeni akış, Faz 3 transfer ve düzeltme dahil, artan `ProductId` sırasına uyar.
   - Tek ürünü güncelleyen Product düzenleme akışı tek satır kilidi aldığı için döngü oluşturmaz.
4. **Order RowVersion (optimistic, istekler arası):**
   - `Order` üzerinde EF concurrency token olarak bir `rowversion` sütunu bulunur (`IsRowVersion()`). Order satırı, sipariş kümesinin (Order + OrderItems) sürüm sınırıdır.
   - Kalemleri değiştiren her işlem Order satırını da güncellenmiş olarak işaretler. Böylece toplam değişmese bile sürüm artar.
   - Draft düzenleme formu ile Confirm, Cancel ve Delete formları kullanıcının gördüğü sürümü taşır. Kilit altında okunan sürüm farklıysa işlem reddedilir. Böylece Admin, onay sayfasını açtıktan sonra değişen ve görmediği kalemleri onaylayamaz.
   - RowVersion Faz 4 `ETag`/`If-Match` için de temeldir. Bu ADR Faz 4'ü açmaz.
5. **Product RowVersion:** Stok doğruluğu için gerekli değildir; stok koruması madde 2'deki kilitlerden gelir. Product düzenleme formunda bayat yazma tespiti veya Faz 4 `ETag` için eklenebilir. Eklenirse onay akışı kilit altında okuduğu için onunla çakışmaz. Ekleme kararı bu ADR'de verilmez, gerekirse Faz 1 tasarımında verilir.
6. **Son savunma hattı:** `CK_Products_StockQuantity_NonNegative` ve terminal durum kontrolü korunur. Kilitler bunların yerine geçmez.
7. **Çakışma eşlemesi:** Yeni bir `ServiceErrorCategory` eklenmez. Bütün çakışmalar `BusinessRule` olarak döner. Confirm, Cancel ve Delete POST'larında mevcut web eşlemesi bunu `409 Conflict` yapar ve kullanıcı güncel durumu görür (`StockFlow/Controllers/OrdersController.cs:341-377`). Draft düzenleme (Edit POST) bugün yalnız `order.not_draft` kodunu 409'a eşler (`OrdersController.cs:198-201`) ve diğer kodları 500'e düşürür (`:214`). Faz 1'de `order.concurrency_conflict` için de 409 eşlemesi eklenir.

   | Durum | Hata kodu | Kategori → HTTP |
   | --- | --- | --- |
   | Kilitten sonra sipariş artık Draft değil (çift onay, Confirm/Cancel yarışı) | mevcut `order.not_draft` | BusinessRule → 409 |
   | Kilit altında okunan taze stok yetersiz | mevcut `stock.insufficient` | BusinessRule → 409 |
   | Formdaki Order sürümü güncel değil, `DbUpdateConcurrencyException`, SQL 1205 (deadlock kurbanı) veya 1222 (kilit zaman aşımı) | yeni `order.concurrency_conflict` | BusinessRule → 409 |

   - Kullanıcı mesajı: "Sipariş başka bir işlemle eşzamanlı olarak değiştirildi. Güncel durumu kontrol edip yeniden deneyin."
   - Her durumda transaction geri alınır ve change tracker temizlenir. Loga yalnız `OrderId`, varsa `ProductId` ve hata kodu yazılır.
   - Kilit beklemesi sınırlıdır: transaction başında `SET LOCK_TIMEOUT 5000` (5 saniye) ayarlanır. Aşılırsa 1222 oluşur ve `order.concurrency_conflict` → 409 döner. Bu ayar transaction'a değil oturuma bağlıdır ve commit/rollback ile geri dönmez. Bu yüzden aynı bağlantıdaki sonraki sorgular etkilenmesin diye transaction sonunda (başarı veya hata) `SET LOCK_TIMEOUT -1` ile varsayılana geri alınır. Komut zaman aşımı (`SqlException` -2) beklenmeyen hata olarak kalır.
8. **Yeniden deneme politikası:**
   - Sunucu bir çakışmayı, deadlock kurbanı (1205) dahil, otomatik olarak yeniden denemez. Kilit sırası kuralı deadlock'u önlemelidir. Deadlock oluşursa kural ihlali görünür olur.
   - Kullanıcının yeniden denemesi güvenlidir: ilk deneme commit edildiyse ikincisi `order.not_draft` alır, edilmediyse hiçbir değişiklik kalmamıştır (BR-ATOM-05).
   - `EnableRetryOnFailure` bugün kapalıdır (`StockFlow/Program.cs:21-29`). İleride açılırsa, kullanıcının başlattığı transaction retrying strategy ile uyumsuz olduğundan onay transaction'ı `Database.CreateExecutionStrategy().ExecuteAsync(...)` içine alınmalıdır.
   - Faz 4 API'sindeki `Idempotency-Key` ve `If-Match` → `412` bu ADR'ye dayanır ama Faz 4 kapsamındadır.
9. **Zamanlama:** Karar Faz 0'da kaydedilir. **Bu ADR Faz 1'i açmaz; Faz 1 `Kapalı` kalır.** Madde 1-8 kodda yalnız [product-spec.md](../product-spec.md) › 10.1'de Faz 1 `Açık` olduğunda ve insan onaylı PR ile uygulanır. O zamana kadar bugünkü davranış ve `.claude/rules/orders-stock.md` geçerlidir.

## Değerlendirilen alternatifler

- **RowVersion-only (Product + Order, tamamen optimistic):** EF'te yerleşiktir ve ham SQL istemez. Seçilmedi, çünkü:
  - Popüler bir üründe her eşzamanlı onay kullanıcıya 409 olarak döner. Kilitte bekleyip başarabilecek işlem reddedilir.
  - Product RowVersion, stokla ilgisi olmayan her Product düzenlemesini onayla çakıştırır.
  - Çakışma ancak `SaveChanges` anında fark edilir; planner o ana kadar bayat veriyle çalışmış olur.

  Order RowVersion, istekler arası bayat form tespiti için yine de seçildi (madde 4).
- **Koşullu `UPDATE`** (`StockQuantity = StockQuantity - @q WHERE Id = @id AND StockQuantity >= @q`, `ExecuteUpdateAsync` ve etkilenen satır sayısı kontrolü): Kayıp güncellemeyi tek ifadede önler; okuma-hesap-yazma döngüsü yoktur. Seçilmedi, çünkü:
  - Stok kararı planner'dan SQL'e kayar. İlk başarısız satırda durur ve Faz 1'in "tüm yetersiz kalemleri raporla" maddesini ek okuma olmadan karşılayamaz.
  - `ExecuteUpdate` change tracker'ı atlar. Onayda iki ayrı yazma mekanizması oluşur ve tek `SaveChangesAsync` değişmezi bozulur.
  - BR-ATOM-01'deki "herhangi bir Product değiştirilmeden önce bütün kalemler doğrulanır" sırası tersine döner (değişiklik rollback ile geri alınsa bile).

  Order için koşullu durum güncellemesi (`WHERE Status = Draft`) de değerlendirildi. Order `UPDLOCK` ve durum kontrolü aynı garantiyi verdiği için ayrıca seçilmedi.
- **`SERIALIZABLE`:** Ek kod istemez. Seçilmedi, çünkü aynı ürünleri okuyan iki onay paylaşılan (S) kilitleri tutar ve X kilidine yükseltmeye çalışırken kilit dönüşüm deadlock'una girer. Böylece çakışma mekanizması 1205 deadlock'u olur ve yeniden deneme zorunlu hâle gelir. Range kilitleri de gereksiz satırları bloke eder.
- **`SNAPSHOT` izolasyonu:** Veritabanında `ALLOW_SNAPSHOT_ISOLATION` açılmasını gerektirir ve çakışmayı commit anında 3960 hatasıyla bildirir. Davranışı ve sorunları RowVersion-only ile aynıdır. Seçilmedi.
- **Uygulama düzeyi kilit** (in-process `SemaphoreSlim`, dağıtık kilit, Redis): Çok örnekli çalışmada geçersizdir ya da kalıcı kapsam dışı bir bileşen ister (spec › 10.3). Seçilmedi.

## Sonuçlar

- Olumlu:
  - Overselling ve çift onay veritabanı kilitleriyle imkânsız hâle gelir.
  - Planner tek karar noktası olarak kalır ve tüm yetersiz kalemleri raporlayabilir.
  - Tek transaction ve tek `SaveChangesAsync` değişmezi korunur.
  - 409 eşlemesi mevcut Controller kalıbıyla çalışır.
- Maliyetler ve kabul edilen riskler:
  - Kilitli okuma ham SQL (`FromSql`) ister ve SQL Server'a özgüdür. Veritabanı sağlayıcısı değişirse bu ADR yeniden açılır.
  - Her ürün için bir kilitli okuma ek bir gidiş-dönüş demektir. Sipariş kalem sayısı küçük olduğu için kabul edilir.
  - Aynı ürüne yönelik onaylar sıraya girer. Bekleme süresi kilit zaman aşımıyla sınırlıdır.
  - Bütün sipariş mutasyonları önce Order kilidini almak zorundadır. Bu kurala uymayan yeni bir akış deadlock riski taşır.
  - Faz 1'e kadar eşzamanlı onay riski sürer. Bu risk üretimde değil, öğrenme ortamındadır (LocalDB, `CLAUDE.md` › Bilinen ortam gerçekleri).
- Faz 1 için öngörülen iş (onaylanmış iş değildir; Faz 1 tasarımında kesinleşir):
  - Data: `Order.RowVersion` ve migration.
  - Service: `OrderService` onay ve mutasyon akışları, `order.concurrency_conflict` kodu.
  - Web: Draft düzenleme ile Confirm, Cancel ve Delete formlarında sürüm alanı; Edit POST'ta `order.concurrency_conflict` için 409 eşlemesi.
  - Tests: paralel testler.
  - Kütüphane: `.claude/rules/orders-stock.md`, `services.md`, `web-layer.md` (`/curate-knowledge`).

## Doğrulama

- Bugün:
  - Bu ADR'deki bağlantılar çözülür ve `docs/adr/README.md` indeksi günceldir.
  - `node .claude/hooks/agentic/test-guard.mjs` ve repo hijyeni betiği geçer.
  - Kod, şema ve test değişmez.
- Faz 1 doğrulaması. Testler gerçek SQL Server üzerinde koşar ve her transaction ayrı bir `DbContext` kullanır. 1, 2 ve 6. maddeler [product-spec.md](../product-spec.md) › 10.1 Faz 1 çıkış kanıtını somutlaştırır; 3, 4 ve 5. maddeler bu ADR'nin kararının uygulandığını gösteren ek doğrulamalardır:
  1. Stoğu yalnız birine yeten, aynı ürünlü iki Sale eşzamanlı onaylanır. Tam biri `Confirmed` olur, diğeri `stock.insufficient` alır; stok yalnız bir düşüş kadar azalır.
  2. Aynı taslak eşzamanlı iki kez onaylanır. Tam bir başarı ve bir `order.not_draft` oluşur; hareket sayısı kalem sayısına eşittir.
  3. Confirm ile Cancel, Update ve Delete eşzamanlı çalışır. Hiçbir sonuç 500 değildir; `Cancelled` ya da silinmiş sipariş hareket taşımaz.
  4. Ürün kümeleri `{A,B}` ve `{B,A}` olan iki sipariş tekrar tekrar eşzamanlı onaylanır ve 1205 oluşmaz.
  5. Bayat form sürümü `order.concurrency_conflict` → 409 üretir.
  6. "Hareket net toplamı = mevcut stok" defter mutabakatı sağlanır (OpeningBalance ile birlikte).
