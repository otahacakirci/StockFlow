# StockFlow sunum rehberi

Bu paket, staj yapılan işyerinde 3–4 teknik meslektaşa yönelik hazırlanmıştır. Ana anlatım yaklaşık 18 dakika 20 saniye, canlı geçişlerle 18–22 dakika sürer. Soru-cevap için 5 dakika bırakılır. Ana sunum 14 slayt, teknik ekler 4 slayttır.

## 1. Aşama: sunum kurgusu

**Ana fikir:** StockFlow, siparişin hazırlık ve onay aşamalarını ayırarak stok miktarını ve siparişe bağlı hareket geçmişini yönetir.

Anlatıyı bir satış siparişinin etrafında kur. Önce iş ihtiyacını açıkla, sonra kullanıcıya görünen akışı göster. Mimariye, veri modeline ve kod ayrıntılarına bu örneği açıklamak için geç. Teknoloji isimlerini sıralamak yerine her birinin bu akıştaki görevini anlat.

| Bölüm | Slayt | Süre | Dinleyicinin alacağı mesaj |
| --- | --- | --- | --- |
| Açılış ve problem | 1–2 | 1:30 | Stok miktarı ile değişikliğin kaynağını birlikte takip etme ihtiyacı |
| Kapsam ve yaşam döngüsü | 3–4 | 2:40 | Kim hangi işi yapıyor, stok ne zaman değişiyor? |
| Sayısal örnek ve demo | 5–6 | 5:00 | Draft stok değiştirmez, onay stok ve hareket üretir |
| Mimari ve veri modeli | 7–8 | 3:00 | Bu davranışın sorumlulukları ve ilişkileri |
| Atomik onay ve fiyat | 9–10 | 3:00 | Tutarlılık ve geçmiş fiyatın korunması |
| Yetki ve kullanım | 11–12 | 1:50 | Güvenli giriş sınırları ve günlük operasyon |
| Kazanımlar ve kapanış | 13–14 | 1:20 | Uygulanan tercihler ve bilinçli kapsam sınırı |
| Soru-cevap | Gerektiğinde 15–18 | Yaklaşık 5:00 | Koda dayalı ayrıntılar |

### Ne kadar ayrıntı?

Dinleyiciler teknik olduğu için temel MVC dersine dönüşen bir anlatı yerine proje kararlarını göster. Ana sunumda bütün entity alanlarını, bütün controller metotlarını veya bütün CRUD ekranlarını gezme. Bir ekran eylemini UI, Service ve veritabanı arasında takip etmek yeterli derinliği sağlar. Gerçek kodu soru gelirse 15. slaytta veya kaynak dosyadaki kısa bölümde aç.

Süre kısalırsa 1–2–4–5–6–7–9–13–14 sırasını kullan. Demo için 3 dakika ayır. Bu sürüm yaklaşık 12–14 dakika sürer. Teknik sorular uzarsa ek slaytlara geç, ana akışı yeniden anlatma.

### Sunuş tarzı

“Bu projede şunu yaptım, çünkü bu iş akışında şu ihtiyaç vardı” cümlesini temel al. Her slayt için tek ana mesaj söyle, ardından ekrandaki bir kanıtı göster. Gösterdiğin alanın adını söylemeden imleçle hızlıca gezinme. Bir sonraki ekrana geçmeden önce gözlenen sonucu bir cümleyle bağla. Bilmediğin soruda varsayım üretme: “Mevcut kodda bu koruma yok, sonraki geliştirme alanı olarak ele alırım” diyebilirsin.

Kişisel deneyim bölümündeki ifadeleri kendi yaptığın çalışmayla eşleştir. Sana ait olmayan bir katkıyı veya yaşamadığın bir geliştirme hikâyesini sahiplenme. Projede dış destek kullandıysan, sorulduğunda katkı sınırını açıkça anlat.

## 2. Aşama: hazır materyaller

- StockFlow_Sunum.pptx: düzenlenebilir slaytlar, native tablolar/şemalar ve konuşmacı notları.
- StockFlow_Sunum.pdf: aynı slaytların sabit görünümü. Font veya uygulama uyumsuzluğunda kullanılabilir.
- Sunum_Rehberi.html ve .md: akış, slayt başına metin, demo, soru-cevap ve kaynaklar.
- Cevrimdisi_Anlatim.html: internet ve veritabanı gerektirmeyen etkileşimli iş kuralı açıklaması.
- Teknik_Semalar.md: mimari, durum, ER ve onay akışının Mermaid kaynakları.
- Slayt_Gorselleri/: her slaytın PNG çıktısı. 4, 7, 8 ve 9 numaralar bağımsız teknik şema olarak kullanılabilir.
- Konusmaci_Karti.md: sunum sırasında hızlıca bakılacak kısa hatırlatıcı.

### Kullanım sınırı

İçerik, 8 Eylül 2026 tarihinde ürün belgeleri, uygulama servisleri, controller’lar, ViewModel’ler ve EF mapping dosyaları incelenerek hazırlanmıştır. Bu çalışma sırasında açık bir StockFlow web oturumu bulunmadı. Paket gerçek uygulama ekran görüntüsü veya kaydedilmiş canlı demo içermez. Çevrimdışı anlatım bağımsız bir açıklama modelidir, StockFlow uygulaması değildir. Ekranlarında bu ayrım açıkça yazılıdır. Kurgusal 10 / 7 / 3 stok örneği mevcut veritabanının durumu olarak sunulmaz.

## Slayt başına konuşma metni

Metinleri ezberlemek yerine ilk cümleyi ve gösterilecek noktayı hatırla. Aynı metinler PowerPoint konuşmacı notlarında da bulunur.
### 1. StockFlow (0:30)

StockFlow, ürünleri, iş ortaklarını ve siparişleri aynı uygulamada yöneten bir mini ERP projesi. Bugün önce kısa bir satış akışı göstereceğim. Sonra sipariş onaylandığında stok ve hareket geçmişinin nasıl birlikte değiştiğini, bu davranışı hangi mimari kararlarla kurduğumu anlatacağım.

Kaynak: `StockFlow/Program.cs`, `StockFlow/StockFlow.csproj`, `docs/product-spec.md`

### 2. Çözülen iş problemi (1:00)

Projeyi, stok ile sipariş bilgisini birlikte takip etme ihtiyacı üzerinden anlatıyorum. Bir ürünün mevcut adedi tek başına yeterli değil. Bu sayının hangi satın alma veya satış işlemiyle değiştiğini de görebilmeliyim. StockFlow ürün listesini, sipariş yaşam döngüsünü ve stok hareketlerini birbirine bağlıyor. Burada ölçülmüş bir zaman veya maliyet tasarrufu iddiam yok. Göstereceğim somut fayda, ilgili kayıtların aynı iş akışı içinde görülebilmesi.

Kaynak: `StockFlow/Services/Dashboard/DashboardService.cs`, `StockFlow/Views/StockMovements/Details.cshtml`, `docs/product-spec.md`

### 3. Kapsam ve kullanıcı rolleri (1:20)

Uygulamada kategori, ürün, müşteri, tedarikçi, sipariş, stok hareketi ve dashboard ekranları var. Ekranları tek tek gezmek yerine görev ayrımını öne çıkarıyorum. Employee müşterileri oluşturup düzenleyebilir, Sale ve Purchase taslaklarını hazırlayabilir. Tedarikçi yönetim ekranlarına giremez, ancak Purchase formunda tedarikçi seçebilir. Admin katalog ve tedarikçi kayıtlarını yönetir, sipariş onayını ve iptalini yapar. Silme yetkileri de sınırlıdır. Ayrıntılı rol matrisi ek slaytta bulunuyor.

Kaynak: `StockFlow/Controllers/OrdersController.cs`, `StockFlow/Controllers/SuppliersController.cs`, `StockFlow/Controllers/CustomersController.cs`, `StockFlow/Views/Shared/_Layout.cshtml`

### 4. Sipariş yaşam döngüsü (1:20)

Her sipariş Draft olarak başlar. Bu aşamada kalemleri ve miktarları değiştirebilirim. Taslak stok düşürmez ve rezervasyon yapmaz. Admin onay verdiğinde Purchase stok girişi, Sale stok çıkışı oluşturur. Diğer seçenek taslağı Cancelled durumuna geçirmek. Bu işlem stok hareketi üretmez. Confirmed ve Cancelled terminaldir. Onaylanmış siparişi düzenlemek, iptal etmek veya yeniden açmak mevcut kapsamda yok. Hazırlama ile stok etkisini bu şekilde ayırdım.

Kaynak: `StockFlow/Services/Orders/OrderService.cs:81`, `StockFlow/Services/Orders/OrderService.cs:146`, `StockFlow/Services/Orders/OrderService.cs:228`, `StockFlow/Entities/DomainEnums.cs`

### 5. Bir satışın stok üzerindeki etkisi (1:00)

Bu sayılar anlatım için hazırlanmış kurgusal bir örnek. Ürün A için başlangıç stoğu 10, eşik 3, fiyat 250 lira olsun. Employee 7 adetlik satış taslağı oluşturduğunda stok hâlâ 10. Admin onayladığında stok 3 oluyor, sipariş Confirmed oluyor ve 7 adetlik bir StockOut hareketi oluşuyor. Toplam 7 çarpı 250, yani 1.750 lira. Stok eşikle aynı değere geldiği için ürün düşük stok listesine giriyor. Hareket miktarı pozitif tutuluyor, yönü StockOut alanı belirliyor.

Kaynak: `StockFlow/Services/Orders/OrderStockConfirmationPlanner.cs`, `StockFlow/Services/Orders/OrderService.cs:411`, `StockFlow/Services/Dashboard/DashboardService.cs:23`

### 6. Demo: taslaktan stok hareketine (4:00)

Önce Ürünler ekranında SUNUM-A kodlu ürünün 10 adet stoğunu gösteriyorum. Siparişler ekranında yeni satış taslağı açıp kurgusal müşteriyi ve 7 adet ürünü seçiyorum. Kaydettikten sonra taslak durumunu ve 1.750 liralık tutarı gösteriyorum. İkinci sekmedeki ürün detayını yenileyerek stoğun 10 kaldığını belirtiyorum. Admin hesabında sipariş detayından Onayla ekranını açıp onayı tamamlıyorum. Sonra siparişe bağlı Stok hareketleri bağlantısını izliyor, 7 adet çıkışı gösteriyorum. Ürün ekranında 3 adet kalan stokla örneği bağlıyorum. Hesap değişimini hızlandırmak için iki ayrı tarayıcı profili kullanılabilir. Canlı ortam kullanılamazsa aynı sayısal akışı çevrimdışı anlatım dosyasında gösteriyorum ve bunun açıklama modeli olduğunu söylüyorum.

Kaynak: `StockFlow/Views/Orders/Create.cshtml`, `StockFlow/Views/Orders/Details.cshtml`, `StockFlow/Views/Orders/Confirm.cshtml`, `StockFlow/Views/StockMovements/Index.cshtml`, `StockFlow/Views/Products/Details.cshtml`

### 7. Tek uygulamada sorumluluk ayrımı (1:30)

Tek bir ASP.NET Core MVC uygulaması var. Razor ve Bootstrap ekranı oluşturuyor. Controller form veya sorgu girdisini ViewModel üzerinden alıyor, ilgili servisi çağırıyor ve sonucu ekrana çeviriyor. Stok yeterliliği, fiyat ve durum geçişi gibi kurallar Service katmanında. EF Core ApplicationDbContext kalıcılığı SQL Server üzerinde yönetiyor. Controller doğrudan DbContext kullanmıyor. Entity nesnelerini de form modeli olarak kabul etmiyorum. Aynı host içinde bu ayrım, uygulamanın kapsamına uygun bir sorumluluk sınırı oluşturuyor. OrderQueryService okuma ekranlarını ayırıyor, fakat ayrı bir CQRS altyapısı yok.

Kaynak: `StockFlow/Program.cs`, `StockFlow/Controllers/OrdersController.cs:18`, `StockFlow/Data/ApplicationDbContext.cs`, `StockFlow/ViewModels/Orders/OrderDraftInputModel.cs`, `docs/ai/architecture.md`

### 8. Veri modelinin merkezi sipariş (1:30)

Yedi iş tablosu var. Category ürünleri grupluyor. Order, OrderItem üzerinden birden fazla ürün taşıyor. Aynı ürün aynı siparişte tek satırda yer alabiliyor. Sale siparişinde müşteri zorunlu ve tedarikçi boş. Purchase için bunun tersi geçerli. Bu eşleşmeyi hem Service hem veritabanı constraint'i koruyor. StockMovement hem siparişe hem ürüne bağlı. Böylece stok değişikliğinden ilgili işleme gidebiliyorum. Identity kullanıcı tabloları aynı DbContext içinde. CreatedByUserId şemada nullable olsa da uygulamanın yeni sipariş akışı mevcut bir kullanıcıyı zorunlu tutuyor. Şema görseli iş modeline odaklanıyor, bütün Identity destek tablolarını göstermiyor.

Kaynak: `StockFlow/Data/ApplicationDbContext.cs`, `StockFlow/Data/Configurations/OrderConfiguration.cs`, `StockFlow/Data/Configurations/OrderItemConfiguration.cs`, `StockFlow/Data/Configurations/StockMovementConfiguration.cs`, `StockFlow/Services/Orders/OrderService.cs:44`

### 9. Onay işleminin atomik sınırı (2:00)

Onayda açık bir veritabanı transaction'ı başlatıyorum. Siparişi kalemleri ve ürünleriyle okuyup hâlâ Draft olduğunu, tarafını ve kalemlerini doğruluyorum. Stok planlayıcısı, entity değiştirmeden bütün kalemler için sonucu hesaplıyor. Sale içinde tek bir ürünün bile stoğu yetersizse plan reddediliyor. Plan uygunsa ürün stokları, hareket kayıtları ve Confirmed durumu hazırlanıyor. Tek SaveChangesAsync sonrasında transaction commit ediliyor. Hata durumunda rollback ve tracker temizliği var. Bu yapı kısmi kayıt kalmasını önlüyor. Ancak transaction kullanmak eşzamanlı iki onayın tüm yarışlarını tek başına çözmez. Kodda rowversion tabanlı optimistic concurrency koruması bulunmuyor. Bu sınırı ek slaytta ayrıca belirttim.

Kaynak: `StockFlow/Services/Orders/OrderService.cs:146`, `StockFlow/Services/Orders/OrderService.cs:182`, `StockFlow/Services/Orders/OrderService.cs:194`, `StockFlow/Services/Orders/OrderService.cs:914`, `StockFlow/Services/Orders/OrderStockConfirmationPlanner.cs`

### 10. Sipariş fiyatı kayıt anını korur (1:00)

Sipariş kalemi girişinde yalnız ProductId ve Quantity var. Kullanıcıdan UnitPrice almıyorum. Yeni kalemin fiyatını Product.Price üzerinden okuyup OrderItem.UnitPrice alanına kopyalıyorum. Örneğin 250 liralık üründen iki adetlik taslak 500 lira tutar. Ürünün güncel fiyatı sonradan 300 lira olsa da mevcut satırın fiyatı 250 lira kalır. Taslağa yeni ürün eklenirse onun güncel fiyatı alınır. Toplamı da sunucuda hesaplıyorum. Mevcut modelde Purchase için ayrıca tedarikçi alış fiyatı alanı yok, o da aynı Product.Price kaynağını kullanıyor. Bu nedenle kâr veya maliyet muhasebesi iddiası kurmuyorum.

Kaynak: `StockFlow/ViewModels/Orders/OrderItemInputModel.cs`, `StockFlow/Services/Orders/OrderService.cs:115`, `StockFlow/Services/Orders/OrderService.cs:562`, `StockFlow/Services/Orders/OrderService.cs:576`

### 11. Yetki ve veri giriş sınırları (1:00)

Oturum için cookie tabanlı Identity kullanıyorum. Varsayılan policy kimliği doğrulanmış kullanıcı gerektiriyor. Admin'e özgü işlemlerde endpoint üzerinde ayrıca rol kısıtı var. Ekranda bir düğmeyi gizlemek tek başına yeterli olmadığı için aynı yetkiyi sunucuda da uyguluyorum. POST işlemleri antiforgery koruması taşıyor. Ürün düzenleme modeli stok miktarını, sipariş kalemi giriş modeli fiyatı taşımıyor. Service iş kurallarını ayrıca uyguluyor. Beklenen hatalar Validation, NotFound ve BusinessRule olarak ayrılıyor. Beklenmeyen hata teknik bağlamla loglanıyor, production görünümü stack trace göstermiyor.

Kaynak: `StockFlow/Program.cs`, `StockFlow/Controllers/OrdersController.cs:246`, `StockFlow/ViewModels/Products/ProductUpdateInputModel.cs`, `StockFlow/Services/Common/ServiceErrorCategory.cs`, `StockFlow/Views/Shared/Error.cshtml`

### 12. Günlük kullanım ve sorgular (0:50)

Operasyon ekranlarında yalnız kayıt eklemeyi değil, kaydı bulmayı da ele aldım. Ürünlerde ad veya SKU araması, kategori ve düşük stok filtreleri var. Siparişlerde tür ve durumla filtreleme yapılıyor. Sorgular veritabanında sayfalanıyor ve yalnız ekranın ihtiyaç duyduğu alanları ViewModel'e yansıtıyor. Dashboard satış tutarına yalnız Confirmed Sale kayıtlarını alıyor. Bu tutar tahsilat veya kâr anlamına gelmiyor. Düşük stok kuralı eşitliği de içeriyor. Tarihler UTC etiketiyle, tutarlar Türk lirası biçiminde gösteriliyor. Ölçülmüş performans kazanımı iddia etmiyorum.

Kaynak: `StockFlow/Services/Products/ProductService.cs:21`, `StockFlow/Services/Orders/OrderQueryService.cs:18`, `StockFlow/Services/Dashboard/DashboardService.cs`, `StockFlow/Views/Home/Index.cshtml`

### 13. Teknik kazanımlar ve kapsam sınırı (1:00)

Bu projede benim için temel teknik kazanım, bir ekran eyleminin veritabanındaki tutarlı karşılığını tasarlamak oldu. MVC, Service ve EF Core sınırlarını somut sipariş akışı üzerinde uyguladım. Mevcut uygulama bir mini ERP kapsamına odaklanıyor. Ödeme, kargo, rezervasyon, depo transferi ve onaylı siparişin ters kaydı bulunmuyor. Sonraki iş olarak eşzamanlı onayları, gerekçeli stok düzeltmesini ve yayın ortamını değerlendirirdim. Bunları tamamlanmış özellikler olarak sunmuyorum.

Kaynak: `docs/product-spec.md`, `StockFlow/Services/Orders/OrderService.cs`, `StockFlow/Entities/DomainEnums.cs`, `StockFlow/Program.cs`

### 14. Sorular (0:20)

StockFlow'un merkezinde sipariş yaşam döngüsü var. Taslak hazırlığı, onay yetkisi ve stok etkisini ayrı sorumluluklarla birleştirdim. İsterseniz şimdi onay işleminin koduna veya veri modelindeki ayrıntılara geçebiliriz.

Kaynak: `StockFlow/Services/Orders/OrderService.cs`

### 15. Ek: onay akışındaki gerçek kod (Soru gelirse)

Bu parça ConfirmDraftAsync metodundaki başarılı yolun doğrudan alıntısı. Sipariş okuma, transaction başlangıcı ve hata dalları bu kısa alıntının dışında kalıyor. CreatePlan kararı bütün stok kontrollerini tamamladıktan sonra ApplyConfirmationPlan değişiklikleri hazırlıyor. SaveChangesAsync tek kayıt çağrısı, CommitAsync ise açık transaction'ın başarılı bitişi. Kodun tamamını açacaksam metot başını ve catch / rollback bölümünü de göstereceğim.

Kaynak: `StockFlow/Services/Orders/OrderService.cs:182`, `StockFlow/Services/Orders/OrderService.cs:193`

### 16. Ek: rol matrisi (Soru gelirse)

Bu matris endpoint yetkilerini özetliyor. Admin de müşteri ve taslak sipariş hazırlayabilir. Employee Sale ve Purchase taslağı oluşturup düzenleyebilir. Purchase tarafının seçim verisini OrdersController servislerden alıyor, bu Supplier CRUD ekranına erişim yetkisi anlamına gelmiyor. Kayıt silme yetkisi olan kullanıcı için de geçmiş koruma kuralları devam ediyor. Rollerin ayrı olması sipariş bazlı sahiplik izolasyonu anlamına gelmiyor. Mevcut ekranda yalnız kendi siparişlerini görme kuralı yok.

Kaynak: `StockFlow/Controllers/OrdersController.cs`, `StockFlow/Controllers/CustomersController.cs`, `StockFlow/Controllers/SuppliersController.cs`, `StockFlow/Controllers/ProductsController.cs`, `StockFlow/Controllers/CategoriesController.cs`, `StockFlow/Controllers/StockMovementsController.cs`

### 17. Ek: geçmişi koruyan silme kuralları (Soru gelirse)

Bağlı kayıtları kontrol ederek fiziksel silmeye izin veriyorum. Ürünün sipariş kalemi veya stok hareketi varsa silinmiyor. Müşteri ve tedarikçi, herhangi bir durumda siparişe bağlıysa silinmiyor. Kategori için bağlı ürün bulunmamalı. Sipariş için yalnız hareket geçmişi olmayan Draft silinebiliyor. İlgili foreign key'lerde Restrict silme davranışı var. Bu soft delete değil. Ayrıca geçmişsiz ürünün silinmesinde stok sıfır olsun diye ek bir şart bulunmuyor.

Kaynak: `StockFlow/Services/Categories/CategoryService.cs`, `StockFlow/Services/Products/ProductService.cs:259`, `StockFlow/Services/Customers/CustomerService.cs`, `StockFlow/Services/Suppliers/SupplierService.cs`, `StockFlow/Services/Orders/OrderService.cs:251`, `StockFlow/Data/Configurations/OrderConfiguration.cs`

### 18. Ek: atomiklik ve eşzamanlılık (Soru gelirse)

Ardışık tekrar onay ve eşzamanlı onay farklı konular. Daha önce Confirmed olmuş bir siparişi yeniden onaylama isteği durum kontrolünde reddedilir. Ancak iki istek aynı anda Draft okuyabilir veya iki farklı satış aynı stok değerinden hareket edebilir. Mevcut onay metodu transaction isolation seviyesini açıkça yükseltmiyor ve modelde rowversion concurrency token bulunmuyor. Gelecek tasarımda stok kaydı ve sipariş durumu birlikte ele alınmalı. Optimistic concurrency, koşullu güncelleme veya uygun kilitleme yaklaşımından hangisinin kullanılacağını işlem hacmi ve kullanım senaryosu belirler. Bunu bugün çözülmüş gibi anlatmıyorum.

Kaynak: `StockFlow/Services/Orders/OrderService.cs:150`, `StockFlow/Data/Configurations/ProductConfiguration.cs`, `StockFlow/Data/Configurations/OrderConfiguration.cs`, `docs/product-spec.md`

## Canlı demo: 4 dakikalık ana senaryo

### Sunumdan önce hazırlanacak kurgusal kayıtlar

Bu kayıtlar hazır veri önerisidir. Paket oluşturulurken uygulamanın veritabanına eklenmemiştir. Mevcut kayıtları değiştirmek yerine sunuma ayrılmış yeni kayıtlar kullan. SKU doluysa yeni bir son ek seç ve bütün demo boyunca aynı SKU'yu kullan.

| Kayıt | Değerler |
| --- | --- |
| Kategori | Sunum Kategorisi |
| Ürün A | Ad: Sunum Ürünü A, SKU: SUNUM-A, fiyat: 250,00, başlangıç stoğu: 10, minimum stok: 3 |
| Ürün B (isteğe bağlı) | Ad: Sunum Ürünü B, SKU: SUNUM-B, fiyat: 100,00, başlangıç stoğu: 1, minimum stok: 0 |
| Müşteri | Sunum Müşterisi. İletişim alanları boş kalabilir |
| Tedarikçi (isteğe bağlı) | Sunum Tedarikçisi. İletişim alanları boş kalabilir |

Ürün oluşturma Admin işlemidir. Ürün düzenleme ekranından stok sıfırlanmaz. Onaylanan sipariş terminal olduğu için sunum provası stok tüketir. Ana örneği tekrarlarken yeni SKU ile taze bir sunum ürünü oluştur veya başlangıç değerini güncel stoğa göre açıkça değiştir. Mevcut siparişi geri açmaya çalışma. Geliştirme ortamında demo seeder kullanıyorsan onun örnek verileri ile bu özel SUNUM-A senaryosunu karıştırma.

### Uygulama hazırlığı

Gerekenler: .NET 10 SDK, çalışan SQL Server veya LocalDB, migration uygulanmış veritabanı, güvenli yapılandırmada Identity başlangıç kullanıcı ayarları ve HTTPS tarayıcı erişimi. Gerçek hesap bilgilerini slaytlara veya rehbere yazma. Kurulum için depodaki README'yi kullan. Uygulama otomatik migration çalıştırmaz.

Depodaki launch profili HTTPS için https://localhost:7141 adresini tanımlar. Kendi çalıştırma ortamında adres farklıysa o adresi kullan. Cookie Secure olarak yapılandırıldığı için demoyu HTTPS üzerinden aç. Zaten yapılandırılmış ortamda uygulamayı başlatma komutu:

```powershell
dotnet run --project StockFlow --launch-profile https
```

Handoff belgesinde 3 Eylül tarihli bir LocalDB başlatma sorunu kaydı var. Bu kayıt sunum günündeki ortamın bozuk olduğunu kanıtlamaz. Sunum öncesinde uygulamanın giriş ekranı ve domain ekranlarını açabildiğini kontrol et. Ortam açılmıyorsa toplantı sırasında altyapı onarımına girme, çevrimdışı anlatıma geç.

Admin ve Employee hesapları için iki ayrı tarayıcı profili veya normal / gizli pencere kullan. Aynı tarayıcı profili içindeki sekmeler aynı oturumu paylaşır. Adresleri önceden aç: /Products, /Orders, /StockMovements ve /. İlk ekranda filtreler SUNUM-A ürününü gösterecek şekilde hazır olabilir.

### Sahne akışı

| Süre | Eylem | Söylenecek cümle / gösterilecek sonuç |
| --- | --- | --- |
| 0:00–0:25 | Ürünler'de SUNUM-A ara, detayı aç | “Başlangıç stoğum 10, düşük stok eşiğim 3.” |
| 0:25–1:25 | Employee ile Siparişler, yeni satış. Müşteri ve SUNUM-A seç, miktar 7, kaydet | “Employee taslağı hazırlıyor. Birim fiyatı kullanıcı girmiyor.” |
| 1:25–1:50 | Sipariş detayını ve ürün sekmesini yenile | “Durum Taslak, toplam 1.750 TL. Stok hâlâ 10.” |
| 1:50–2:45 | Admin oturumunda aynı siparişi aç. Onayla ekranı ve onay işlemi | “Stok etkisini Admin onayı başlatıyor.” |
| 2:45–3:25 | Sipariş detayından Stok hareketleri bağlantısını aç | “Bu siparişe bağlı 7 adet StockOut kaydı var.” |
| 3:25–4:00 | Ürün detayını ve düşük stok filtresini aç | “Kalan 3. Eşiğe eşit olduğu için düşük stok listesinde.” |

Demo sonrası geçiş: “Şimdi bu tek onayın arkasındaki sorumluluk ayrımını göstereyim.” Ardından 7. slayta geç.

### Süre kalırsa alternatif iş kuralı örnekleri

**Yetersiz stok:** Ana satıştan sonra A stoğu 3, B stoğu 1 olsun. İkinci taslakta A'dan 1, B'den 2 iste. Onay reddedilir. Sipariş Draft kalır, A 3 ve B 1 kalır, bu sipariş için yeni hareket oluşmaz. İki ürün kullanmak, yeterli olan ilk ürünün de değişmediğini anlatmayı kolaylaştırır. Bunu canlı akışa eklemek yaklaşık 2 dakika gerektirir. Ana sunumda zorunlu değildir.

**Satın alma:** Ana satıştan sonra A için 5 adet Purchase taslağı hazırla. Draft sırasında stok 3 kalır. Admin onayı sonrasında 8 olur ve 5 adet StockIn kaydı oluşur. Geçerli Supplier seçimi gerekir. Onaylı satış toplamı bu satın alma yüzünden artmaz.

**İptal:** Yeni bir Draft açıp Admin ile iptal et. Cancelled olur, stok ve hareketler değişmez. Bu örnek onaylı sipariş iptali değildir.

**Fiyat snapshot:** Yeni A kalemini fiyat 250,00 iken iki adetle kaydet. Admin olarak Product fiyatını 300,00 yap. Var olan taslak satırın birim fiyatı 250,00 kalır. Sadece bu sunuma ayrılmış ürün üzerinde uygula. Mevcut satırı kaldırıp yeniden eklemek farklı bir fiyat alma davranışına yol açabileceğinden örneği karmaşıklaştırma.

### Çevrimdışı seçenek

Cevrimdisi_Anlatim.html dosyasını tarayıcıda aç. Ana satış örneğinde Employee olarak taslağı kaydet, Admin rolüne geçip onayla. Stok, durum ve hareket bölümündeki değişikliği göster. Üstteki “Kurgusal açıklama modeli” ifadesini koru. Bu dosya sunum için kuralları görünür kılar, gerçek HTTP yetkisini veya SQL transaction'ını çalıştırmaz. Teknik açıklamanın kanıtı 9. slayt ve kaynak koddur.

## Ekran görüntüsü / kısa kayıt çekim listesi

Gerçek uygulama görüntüsü eklemek istersen aşağıdaki altı kare aynı sunum kaydına ait olsun. Kullanıcı e-postası gibi gereksiz kişisel alanları görüntüye alma. Her karenin altına rolü ve aşamayı yaz. Paket bu kareleri üretilmiş ekran görüntüsü gibi sunmaz.

| Kare | Görünüm | Kadraj |
| --- | --- | --- |
| 01 | Ürün detayı, önce | SUNUM-A, stok 10, eşik 3 |
| 02 | Sipariş taslağı detayı | Draft, 7 adet, 250 TL birim fiyat, 1.750 TL toplam |
| 03 | Ürün detayı, taslaktan sonra | Yenileme sonrası stok hâlâ 10 |
| 04 | Sipariş detayı, onaydan sonra | Confirmed ve Stok hareketleri bağlantısı |
| 05 | Siparişe filtreli hareket listesi | Aynı sipariş numarası, StockOut, 7 adet |
| 06 | Ürün detayı, sonra | Stok 3 ve düşük stok görünümü |

Kısa ekran kaydı için aynı akışı 60–90 saniyede, yalnız kritik alanlara odaklanarak çekebilirsin. Kullanılacak kurgusal kayıtları önce oluştur, kayda parola girişini alma. Canlı demo ile kayıt arasında farklı sipariş numaralarını aynı işlemmiş gibi birleştirme.

## Hazırlık ve sunum kontrol listesi

Bir gün önce: PPTX ve PDF dosyasını sunum bilgisayarında aç, şemaların ve Türkçe karakterlerin görünümüne bak. Ekran paylaşımı / projeksiyon çözünürlüğünde küçük metinleri kontrol et. Uygulamayı HTTPS ile aç, ayrı rol oturumlarını ve demo kayıtlarını hazırla. İnternet olmasa da PDF ve çevrimdışı HTML yerel olarak açılmalı.

Toplantıdan 10 dakika önce: Bildirimleri kapat, şarjı bağla, gerekli sekmeleri aç, masaüstündeki gereksiz kişisel pencereleri kapat. PowerPoint konuşmacı notlarını izleyiciye yansıtmadan kendi ekranında gösterecek düzeni seç. Yalnız tek ekran varsa konuşmacı kartını ikinci cihazda veya kağıtta kullan.

Sunum sırasında: 6. slaytta uygulamaya geç, en fazla 4–5 dakika sonra geri dön. Bir ekran açılmazsa bir kez dene, sonra çevrimdışı örneğe geç. Beklenmedik teknik soruyu kısa cevapla, ayrıntısını 15–18. slaytlara bırak. 22. dakikada ana anlatımı kapat.

## Beklenen teknik sorular ve kısa yanıtlar
### Neden MVC ve tek uygulama?

Kapsam, sunucu tarafında form ve liste ekranlarından oluşuyor. Tek MVC uygulaması Razor UI, iş kuralları ve veritabanı erişimini aynı host içinde, ayrı sorumluluklarla sunuyor. Ayrı frontend veya servis dağıtımı gerektiren bir ihtiyaç henüz yok.

### Neden ayrıca Repository katmanı yok?

Service, EF Core ApplicationDbContext üzerinden veri erişiyor. Bu kapsamda generic Repository veya Unit of Work soyutlaması eklemedim. DbContext zaten değişiklik takibini ve kayıt sınırını sağlıyor.

### Controller ne yapıyor, Service ne yapıyor?

Controller HTTP girdisini ViewModel ile alıyor, yetki ve form davranışını yönetiyor, servis sonucunu ekrana çeviriyor. Service taraf ve durum kurallarını, fiyatı, toplamı, stok kontrolünü ve kalıcılığı yönetiyor.

### Draft stok ayırıyor mu?

Hayır. Draft oluşturma ve düzenleme stok miktarını değiştirmiyor, rezervasyon yapmıyor. Stok yeterliliği onay anında yeniden ele alınıyor.

### Sipariş onaylandıktan sonra iptal edilebilir mi?

Mevcut kapsamda hayır. Confirmed ve Cancelled terminal. İade veya reversal için yeni bir iş sözleşmesi ve karşıt stok hareketi tasarımı gerekir.

### UnitPrice neden ayrıca saklanıyor?

Ürünün bugünkü fiyatı değişebilir. Sipariş kaleminde saklanan UnitPrice, o satırın kayıt anındaki fiyatını korur. Mevcut Draft satırının düzenlenmesi fiyatı yenilemez.

### Purchase fiyatı nereden geliyor?

Mevcut kodda yeni Sale ve Purchase kalemleri Product.Price üzerinden fiyat alıyor. Ayrı alış fiyatı veya tedarikçi fiyat listesi bulunmuyor. Dolayısıyla alış maliyeti ya da kâr muhasebesi kapsamı iddia etmiyorum.

### Transaction eşzamanlı satışı da çözüyor mu?

Tek başına hayır. Mevcut yapı ilgili kayıtları atomik yazar. Aynı stoğu veya sipariş durumunu eşzamanlı okuyan istekler için ek concurrency stratejisi gerekiyor. rowversion tabanlı optimistic concurrency bugün uygulanmış değil.

### Employee neden tedarikçi seçebiliyor ama yönetemiyor?

Sipariş hazırlamak için taraf seçimi gerekiyor. OrdersController seçim verisini SupplierService üzerinden alıyor. Tedarikçi listeleme ve CRUD endpoint’leri ise yalnız Admin rolüne açık.

### Satış toplamı ciro veya tahsilat mı?

Dashboard yalnız Confirmed Sale siparişlerinin TotalAmount değerini topluyor. Tahsilat, vergi, iskonto, ödeme veya kâr hesabı anlamına gelmiyor. Ekrandaki adı onaylı satış toplamı.

### Başlangıç stoğunun hareketi nerede?

Ürün oluşturulurken verilen başlangıç stoğu ilk durum kabul ediliyor ve StockMovement üretmiyor. Sonraki sipariş etkileri hareketlere yazılıyor. Bu nedenle yalnız hareketleri toplayıp her ürünün stok miktarına ulaşacağımı söylemem.

### Soft delete kullanıyor musun?

Hayır. Geçmişsiz kayıtlarda koşullu fiziksel silme var. Bağlantılı geçmişi Service kontrolleri ve Restrict foreign key davranışı koruyor.

### CreatedByUserId nullable ise kullanıcı nasıl zorunlu?

Şema alanı nullable. Yeni sipariş oluşturma servisinde kullanıcı kimliği boş olamaz ve Identity kaydının varlığı kontrol edilir. Uygulama kuralıyla veritabanı nullability’si burada farklı seviyeler.

### OrderQueryService neden ayrı?

Liste ve detay projection’larını sipariş mutasyonlarından ayırarak sorumluluğu okunabilir tutuyor. Ayrı bir CQRS altyapısı, mesajlaşma veya MediatR kullanmıyorum.

### Filtreleme ve sayfalama nerede?

EF sorgusunda Where, izin verilen sıralama, Skip ve Take ile veritabanında uygulanıyor. AsNoTracking ve projection, salt okunur ekran için gereksiz entity grafiğini yüklememeyi sağlıyor. Sayfa boyutu üst sınırı 100.

### Veritabanı hangi kuralları koruyor?

Benzersiz SKU ve OrderNumber, siparişte ürünün tek satır olması, foreign key ilişkileri, pozitif miktar/fiyat ve tür-taraf eşleşmesi. Terminal durum, toplam hesabı ve kalemler arası stok kararı Service sorumluluğunda.

### Giriş sistemi JWT mi?

Cookie tabanlı ASP.NET Core Identity. MVC ekranları için bu oturum modeli kullanılıyor. JWT veya yazma amaçlı API bulunmuyor.

### Canlıya hazır mı?

Yerel geliştirme yapılandırması ve uygulama kodu mevcut. Üretim ortamı, eşzamanlılık ve işletim gereksinimlerini ayrı çalışmak gerekir. LocalDB geliştirme içindir. Bir production yayını yaptığımı söylemiyorum.

## İddia ve kaynak haritası

| İddia | Koddaki başlıca dayanak |
| --- | --- |
| MVC, EF Core SQL Server, cookie Identity | StockFlow/Program.cs, StockFlow/StockFlow.csproj |
| Controller / Service sınırı | StockFlow/Controllers/OrdersController.cs |
| Draft stok değiştirmez, terminal durum | StockFlow/Services/Orders/OrderService.cs |
| Atomik onay ve rollback | OrderService.cs, ConfirmDraftAsync ve RollbackFailureAsync |
| Bütün stokları entity mutation öncesi hesaplama | StockFlow/Services/Orders/OrderStockConfirmationPlanner.cs |
| Fiyat snapshot ve sunucu toplamı | OrderService.cs, ValidatePricesAndBuildDraft ve SynchronizeDraftItems |
| Fiyatın giriş modelinde bulunmaması | StockFlow/ViewModels/Orders/OrderItemInputModel.cs |
| Ürün edit girişinde stok alanı bulunmaması | StockFlow/ViewModels/Products/ProductUpdateInputModel.cs |
| Rol matrisi | StockFlow/Controllers/*.cs ve StockFlow/Views/Shared/_Layout.cshtml |
| İlişkiler, indeksler, constraint ve Restrict | StockFlow/Data/Configurations/*.cs |
| Dashboard hesaplarının anlamı | StockFlow/Services/Dashboard/DashboardService.cs |
| Filtre, projection ve sayfalama | ProductService.cs, OrderQueryService.cs, ListPagingPolicy.cs |
| Demo katalog miktarları | StockFlow/Data/DemoDataCatalog.cs ve DemoDataSeeder.cs |

Kaynak belge özeti ile kodun ayrıldığı yerde kod esas alındı. Örneğin database-schema.md içinde bazı uygulama kuralları gelecek zamanla anlatılıyor; bu kuralların bugünkü karşılıkları OrderService içinde mevcut. Sunum günündeki veritabanı satır sayısı veya gerçek performans ölçümü bu paketin iddiası değildir.

### İddia sınırları

“Her stok değişimi hareket kaydıdır” yerine “Sipariş onayının stok etkisi hareket kaydına bağlanır” de. Ürün başlangıç stoğu hareket üretmez. “Eşzamanlılık çözüldü” yerine mevcut transaction sınırını ve kalan concurrency ihtiyacını ayır. “Satış toplamı kâr” deme. “Onaylı sipariş iptal edilebilir” deme. “Clean Architecture veya CQRS kurdum” deme. “Canlıya aldım” ifadesini ancak gerçekten yaptığın bir yayın varsa kullan.

Bu sunumda ödeme, kargo, rezervasyon, depo transferi, manuel adjustment, yazma amaçlı API veya tamamlanmış concurrency altyapısı varmış gibi bir anlatı yoktur. Gelecek alanları onaylanmış yeni kapsam olarak değerlendirme.
