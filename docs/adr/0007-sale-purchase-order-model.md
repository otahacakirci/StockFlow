---
title: "ADR-0007: Sale/Purchase Sipariş Modeli: Tek Tablo, Tek Aggregate Kökü"
status: Accepted
authority: decision
last_reviewed: "2026-11-20"
review_triggers:
  - order-model-change
  - phase-gate-change
  - database-provider-change
---

# ADR-0007: Sale/Purchase Sipariş Modeli: Tek Tablo, Tek Aggregate Kökü

- Tarih: 2026-11-20
- Durum: Accepted
- Karar sahipleri: Proje sahibi ([issue #5](https://github.com/otahacakirci/StockFlow/issues/5))
- Etkilenen gereksinimler: FR-ORD-01/04/05/07, FR-PROD-02/04, FR-STOCK-02/03, FR-QUERY-02, FR-DASH-03/04, § 6 Tablo 4, § 6.2, BR-STATE-03; [product-spec.md](../product-spec.md) › 10.1 Faz 2, Faz 3
- Yerine geçtiği ADR: Yok
- Yerine geçen ADR: Yok

## Bağlam

- Bugünkü model:
  - Sale ve Purchase tek `Orders` tablosunda, tek `Order` sınıfıyla tutulur (`StockFlow/Entities/Order.cs`). Tür ayrımı `Type` sütunudur (`OrderType { Sale = 1, Purchase = 2 }`). Taraf nullable `CustomerId`/`SupplierId` sütunlarındadır.
  - Taraf kuralını `CK_Orders_PartyMatchesType` korur: Type=1 ise yalnız `CustomerId`, Type=2 ise yalnız `SupplierId` dolu olabilir (`StockFlow/Data/Configurations/OrderConfiguration.cs:16-19`). `UX_Orders_OrderNumber` türlerden bağımsız tek benzersiz indekstir (`:44-46`).
  - Kalemler tek `OrderItems` tablosunda, `Orders`'a tek FK ile bağlıdır (`StockFlow/Data/Configurations/OrderItemConfiguration.cs:29-32`). Stok hareketleri tek ve zorunlu `StockMovements.OrderId` FK'siyle bağlıdır (`StockFlow/Data/Configurations/StockMovementConfiguration.cs:36-39`).
- Türe göre dallanma kalıtımla değil `if`/`switch` ile yapılır. Bu dallanma Service, planner, Controller ve View'lara dağılmıştır. Örnek: stok yönü tek bir `switch` ile seçilir (`StockFlow/Services/Orders/OrderStockConfirmationPlanner.cs:24-29`).
- Draft güncellemesi siparişin türünü yerinde değiştirebilir (`StockFlow/Services/Orders/OrderService.cs:363-368`). Bu davranış testlidir: bir Sale taslağı Purchase olarak güncellenir; Id ve korunan kalem snapshot'ları değişmez (`StockFlow.Tests/Services/OrderServiceTests.cs:74-127`). Spec FR-ORD-07 tür değişimine açıkça ne izin verir ne de yasaklar. Proje sahibi bu davranışın korunmasına karar verdi.
- [product-spec.md](../product-spec.md) › 10.1 Faz 3'te türe göre ayrışacak davranışlar:
  - Alış maliyeti / ağırlıklı ortalama maliyet (`UnitCost`, `SalePrice`) ve stok değerlemesi. Spec bunu Product gereksinimlerine (FR-PROD-02/04) ve FR-ORD-05'e bağlar. MAC'in Purchase onayında güncellenmesi ve maliyetin Purchase kaleminden gelmesi bu ADR'nin yorumudur; kesin yerini Faz 3 tasarımı belirler.
  - Onaylı sipariş iadesi: Sale iadesi stoğa giriş, Purchase iadesi stoktan çıkış.
  - Düşük stoktan Purchase taslağı.
- Ortak kalan davranışlar: Draft → Confirmed/Cancelled yaşam döngüsü, kalem ve fiyat snapshot'ı, toplam, sipariş numarası, tek atomik onay, [ADR-0006](0006-stock-concurrency-strategy.md) kilit sırası, tek sipariş listesi ve dashboard.
- Issue ve roadmap "mal kabul"ü de Faz 3 ayrışma gerekçesi olarak anar. Spec › 10.1 Faz 3'te bu madde yoktur. Bu yüzden mal kabul bu kararın gerekçesi değildir.
- Kısıtlar:
  - ADR-0006 tek kök satır kilidini varsayar: Order satırı → OrderItems → Products (artan Id) → StockMovement.
  - [ADR-0005](0005-lean-four-project-architecture.md): Domain dış pakete bağımlı değildir; somut imzalar Faz 2 tasarımına bırakılır.
  - Tek veritabanı ve tek atomik onay transaction'ı (spec › 7.1).
  - Testler gerçek SQL Server ister ([ADR-0002](0002-isolated-localdb-test-databases.md)).
  - Faz 2 ve Faz 3 `Kapalı`dır.

## Karar

1. **Tablo stratejisi:** Sale ve Purchase tek `Orders` ve tek `OrderItems` tablosunda kalır. Tür ayrımı mevcut `Type` sütunudur. EF Core kalıtımı (TPH alt sınıfları, TPT, TPC) kullanılmaz. Taraf invariantı veritabanında `CK_Orders_PartyMatchesType` ile korunmaya devam eder.
2. **Aggregate:** Tek aggregate kökü `Order`'dır. Sınırı Order + OrderItems'tır; bu, ADR-0006 madde 4'teki sürüm sınırıyla aynıdır. Türe özgü davranış kök üzerinde tür korumalı metotlarla ifade edilir. Ayrı `SaleOrder`/`PurchaseOrder` sınıfları veya ayrı Service'ler oluşturulmaz.
3. **Türe özgü veri (Faz 3 ilkesi):**
   - Türe özgü yeni alanlar ek (additive) sütun veya tablo olarak eklenir.
   - Kural tek satırda ifade edilebiliyorsa check constraint ile korunur (bugünkü taraf kuralı gibi).
   - Kural tablolar arasıysa (örnek: "maliyet yalnız Purchase kaleminde") Faz 3 tasarımı iki yolu değerlendirir:
     - (a) Domain + Service guard ile koruma.
     - (b) Alt tabloya `OrderType` kopyası, `(OrderId, OrderType)` → `Orders(Id, Type)` alternatif anahtarına bileşik FK ve satır check constraint'i.
   - Somut sütunlar bu ADR'de belirlenmez.
4. **API etkisi.** İmzalar gösterim amaçlıdır; kesin hâlleri Faz 2/3 tasarımında belirlenir (ADR-0005 madde 2):
   - **Oluşturma:** Kök, türü taraf bilgisinden türeten tek bir giriş noktası sunar. Örnek: `Order.CreateDraft(OrderParty party, string createdByUserId, IReadOnlyCollection<OrderLine> lines, DateTime utcNow)`. `OrderParty` yalnız `Customer(id)` veya `Supplier(id)` olabilir. Böylece geçersiz bir tür-taraf kombinasyonu derleme zamanında temsil edilemez. `CK_Orders_PartyMatchesType` son savunma hattı olarak kalır. Faz 3 düşük stok taslağı aynı fabrikayı `OrderParty.Supplier(...)` ile kullanır.
   - **Draft düzenleme:** `ChangeParty(OrderParty)` ve `ReplaceItems(...)` yalnız Draft'ta çalışır. Tür değişimi bugünkü gibi yerinde yapılır: Id, `OrderNumber` ve korunan kalem snapshot'ları değişmez.
   - **Onay:** `Confirm(...)` tek ve türden bağımsız bir giriş noktasıdır. Stok yönü (Sale → çıkış, Purchase → giriş) çağırandan alınmaz, kökün `Type`'ından türetilir. Bugünkü planner kararı kökün veya bir domain servisinin içine taşınır. Tek kök satır olduğu için ADR-0006 kilit sırası değişmez. Faz 3'te Purchase onayındaki MAC güncellemesi aynı giriş noktasının Purchase dalıdır.
   - **İade (Faz 3):** `Return(...)` tek ve türden bağımsız bir giriş noktasıdır. Yön `Type`'tan türetilir: Sale iadesi giriş, Purchase iadesi çıkış.
     - İade, onaylı siparişin durumunu yeniden açmaz.
     - Bir kalem için iade edilen toplam miktar onaylanan miktarı aşamaz; aynı iadenin iki kez yazılması da buna dahildir. Kısmi iadeye izin verilip verilmeyeceği Faz 3 tasarımına kalır.
     - Yazım, ADR-0006 kilit sırasıyla tek transaction'da yapılır.
     - İadenin ayrı bir kayıt mı yoksa yeni bir durum mu olacağı Faz 3 tasarımına kalır. Bugün BR-STATE-03 geçerlidir.
5. **Mevcut veri ve migration:**
   - Bu karar için şema değişikliği, migration veya veri dönüşümü **yoktur**.
   - Faz 2 zengin domain geçişi (private setter, fabrika metotları) aynı sütunlara eşlenir. Kabul ölçütü: geçişten sonra `dotnet ef migrations add` boş migration üretir. Boş değilse istenmeyen bir şema farkı vardır.
   - Faz 3 eklemeleri yalnız ek niteliktedir. Mevcut satırlar silinmez ve yeniden numaralanmaz. Mevcut Confirmed Purchase satırları için maliyet backfill kuralını Faz 3 tasarımı belirler.
   - Migration'lar `dotnet ef` ile üretilir. Üretilmiş Designer ve Snapshot dosyaları elle düzenlenmez (`.claude/rules/data.md`).
   - Gelecekte ayrı tablolara geçmek gerekirse bir kaçış yolu vardır: bugünkü Id'ler türler arasında benzersiz olduğundan satırlar `IDENTITY_INSERT` ile Id korunarak kopyalanabilir. Bu yol ancak bu ADR'nin yerine geçen yeni bir ADR ile açılır.
6. **Zamanlama:** Karar Faz 0'da kaydedilir. **Bu ADR Faz 2'yi ve Faz 3'ü açmaz.** Bugünkü kod ve `.claude/rules/orders-stock.md` değişmeden geçerlidir. [product-spec.md](../product-spec.md) değişmez.

## Değerlendirilen alternatifler

Seçenekler:
- **A:** Tek tablo, tek sınıf (seçilen).
- **B:** TPH. Aynı tablo; `SaleOrder`/`PurchaseOrder` alt sınıfları; ayırıcı `Type`.
- **C:** TPT. Taban `Orders` + 1:1 `SaleOrders`/`PurchaseOrders`.
- **D:** TPC. İki tam tablo, ortak Id sequence.
- **E:** Ayrı tablolar / ayrı aggregate: `SaleOrders` + `SaleOrderItems`, `PurchaseOrders` + `PurchaseOrderItems`.

| Ölçüt | A | B | C | D | E |
| --- | --- | --- | --- | --- | --- |
| Şema/migration | Yok | SQL şeması aynı; yalnız model snapshot farkı | İki yeni tablo, `INSERT…SELECT`; taraf sütunları, CK ve indeksler tabandan taşınır | İki tablo, ortak sequence, veri kopyası, eski tablonun kaldırılması | Dört tablo, Id korunarak kopya, `StockMovements` FK'sinin bölünmesi, eski tabloların kaldırılması (en yüksek) |
| Veri dönüşümü | Yok | Yok | Var; Id korunur | Var | Var; `OrderItems` ve `StockMovements` yeniden bağlanır |
| Taraf invariantı | DB CK (bugün) | DB CK + derleme zamanı ayrımı | Alt tabloda `NOT NULL`; "her Order'ın tam bir alt satırı olur" kuralı bildirimsel korunamaz (trigger gerekir) | Tabloda `NOT NULL` | Tabloda `NOT NULL`; `StockMovements` iki nullable FK + "tam biri" CK'si ister, problem oraya taşınır |
| `OrderNumber` benzersizliği | Tek unique indeks | Aynı | Tabanda tek indeks | Tablolar arası garanti yok | Tablolar arası garanti yok |
| Kalem ve hareket FK'leri | Tek FK | Aynı | Tabana FK; değişmez | Soyut tipe FK verilemez; kalemler ve hareketler bölünür | Bölünür |
| Liste/dashboard (FR-QUERY-02, FR-DASH-03/04) | Değişmez | Polimorfik sorgu; değişmez | Her sorguda JOIN | `UNION ALL` | Elle UNION; sorgu Service'leri değişir |
| ADR-0006 kilit sırası | Değişmez | Değişmez | Taban satır kilitlenir | İki kök tablo; kural genellenmeli | İki kök tipi; kural genellenmeli |
| Draft tür değişimi (bugün testli) | Korunur | EF change tracker ile yapılamaz (CLR tipi değişmez); Id korunarak ancak tracker dışı ham SQL ile | EF ile yapılamaz; alt satır silinip ham SQL ile yeniden eklenir | Satır tablolar arasında taşınır | Satır tablolar arasında taşınır; kalemler ve hareketler yeniden bağlanır |
| Faz 3 türe özgü alan | Ek sütun/tablo; tablolar arası kural için Karar 3 | Alt sınıf property'si, aynı tabloda nullable | Alt tabloda `NOT NULL` | Tabloda `NOT NULL` | Tabloda `NOT NULL` |
| Kod etkisi | Yok; Faz 2'de kök metotlar | Faz 2'de hiyerarşi | Hiyerarşi + mapping | Hiyerarşi + sequence | Service, planner, Controller, View ve ViewModel'ler ikiye bölünür |

- **B (TPH):** Şema maliyeti sıfırdır ve derleme zamanı ayrımı sağlar. Seçilmedi, çünkü:
  - Draft tür değişimini EF change tracker ile yapılamaz hâle getirir. Id ve `OrderNumber` korunarak değişim ancak tracker ve tek `SaveChangesAsync` dışında ham SQL ile yapılabilir. Diğer yollar silip yeniden oluşturmak (yeni Id ve numara) ya da değişimi yasaklamaktır.
  - Aynı derleme zamanı güvencesini `OrderParty` değer nesnesi kalıtım olmadan verir.

  Tür değişimi ileride yasaklanırsa B kabul edilebilir bir varyanttır. Şema aynı kaldığı için bu yeni bir ADR ile değerlendirilir.
- **C (TPT):** Taraf sütunlarını `NOT NULL` yapar. Seçilmedi, çünkü "tam bir alt satır" kuralını veritabanında bildirimsel olarak koruyamaz, her sorguya JOIN ekler ve veri taşıması ister.
- **D (TPC):** Seçilmedi. Soyut köke FK verilemediği için kalemler ve hareketler bölünmek zorunda kalır ve tablolar arası numara benzersizliği kaybolur.
- **E (ayrı aggregate):** Seçilmedi, çünkü:
  - Nullable çift problemini `StockMovements`'a taşır.
  - Liste, dashboard ve numara birliğini bozar.
  - Migration maliyeti en yüksek seçenektir.

  Faz 3 ayrışması (maliyet alanı, iade yönü) bu maliyeti haklı çıkaracak kadar büyük değildir. Yaşam döngüsü, kilit ve kalem modeli ortaktır.

## Sonuçlar

- Olumlu:
  - Migration gerekmez.
  - Taraf invariantı veritabanında kalır.
  - ADR-0006, FR-QUERY-02 ve dashboard sorguları değişmez.
  - Draft tür değişimi korunur.
  - Faz 2 geçişi boş-migration ölçütüyle doğrulanabilir.
- Maliyetler ve kabul edilen riskler:
  - Türe özgü alanlar nullable olur. Tablolar arası kurallar check constraint ile değil, Karar 3'teki mekanizmalarla korunur.
  - Türe göre dallanma kökte toplanmazsa dağınık kalır. Faz 2 bunu kökte toplamakla yükümlüdür.
  - Spec'e mal kabul gibi büyük, türe özgü bir ayrışma girerse bu ADR yeniden açılır.

## Doğrulama

- Bugün:
  - Bu ADR'deki bağlantılar çözülür ve `docs/adr/README.md` indeksi günceldir.
  - `node .claude/hooks/agentic/test-guard.mjs` ve repo hijyeni betiği geçer.
  - Kod, şema, migration ve test değişmez.
- Faz 2:
  - Zengin domain geçişinden sonra `dotnet ef migrations add` boş migration üretir.
  - `OrderServiceTests.UpdateDraftAsync_PreservesExistingSnapshotAndUsesCurrentPriceForNewItem` (Draft tür değişimi; bugün `StockFlow.Tests/Services/OrderServiceTests.cs:74-127`) yeşil kalır.
  - Tür değişiminde `OrderNumber`'ın korunması ayrıca test edilir; mevcut test bugün `OrderNumber`'ı doğrulamaz.
  - Geçersiz tür-taraf kombinasyonu domain testinde temsil edilemez.
- Faz 3:
  - İade ve maliyet akışları için SQL Server üzerinde atomiklik ve defter mutabakatı testleri ([product-spec.md](../product-spec.md) › 10.1 Faz 3 çıkış kanıtı).
