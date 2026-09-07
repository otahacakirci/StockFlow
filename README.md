# StockFlow

StockFlow; küçük ve orta ölçekli işletmeler için kategori, ürün, müşteri, tedarikçi, satış/satın alma siparişi ve stok hareketlerini tek ASP.NET Core MVC uygulamasında yöneten örnek bir mini ERP projesidir. Uygulamanın temel değeri, Draft siparişlerin stoğu etkilememesi ve Confirm işleminin sipariş durumu, ürün stokları ile hareket geçmişini tek SQL transaction içinde tutarlı biçimde değiştirmesidir.

## Temel özellikler

- Cookie tabanlı ASP.NET Core Identity login/logout akışı ve global kimlik doğrulama koruması.
- Admin ve Employee rolleri için hem endpoint hem kullanıcı arayüzü seviyesinde yetkilendirme.
- Category, Product, Customer ve Supplier yönetim ekranları.
- Sale ve Purchase türünde, birden fazla ürün kalemi içerebilen Draft siparişler.
- Draft düzenleme; Admin için Confirm, Cancel ve Draft silme işlemleri.
- Confirmed Purchase için StockIn, Confirmed Sale için StockOut hareketleri ve atomik stok güncellemesi.
- Ürün, sipariş ve stok hareketlerinde arama/filtreleme, güvenli sıralama ve sunucu taraflı sayfalama.
- Toplam ürün, düşük stok, müşteri, tedarikçi, sipariş, Confirmed satış toplamı ve son siparişleri gösteren dashboard.
- Güvenli ViewModel sınırları, Service katmanında iş kuralları, EF Core SQL Server kalıcılığı ve xUnit testleri.

## Roller

| İşlem | Admin | Employee |
| --- | --- | --- |
| Dashboard, ürün ve kategori görüntüleme | Evet | Evet |
| Ürün ve kategori oluşturma/düzenleme/silme | Evet | Hayır |
| Supplier yönetimi | Evet | Hayır |
| Customer listeleme/oluşturma/düzenleme | Evet | Evet |
| Sipariş geçmişi olmayan Customer silme | Evet | Hayır |
| Sipariş listeleme ve Draft oluşturma/düzenleme | Evet | Evet |
| Sipariş onaylama, iptal etme ve Draft silme | Evet | Hayır |
| Stok hareketlerini görüntüleme | Evet | Evet |

## Teknolojiler

- .NET 10 ve C#
- ASP.NET Core MVC, Razor ve Bootstrap
- ASP.NET Core Identity ve güvenli cookie oturumu
- EF Core 10 ve Microsoft SQL Server provider
- SQL Server LocalDB veya desteklenen tam SQL Server örneği
- xUnit ve `Microsoft.AspNetCore.Mvc.Testing`

Uygulama tek MVC hostu içinde çalışır. Controller sınıfları doğrudan `ApplicationDbContext` kullanmaz; HTTP giriş/çıkışları ViewModel, iş ve stok kuralları Service katmanı üzerinden yürütülür.

## Ön koşullar

- [.NET SDK 10.x](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows geliştirme ortamında SQL Server LocalDB veya erişilebilir bir SQL Server örneği
- PowerShell 5.1 ya da PowerShell 7
- HTTPS geliştirme sertifikası

LocalDB, yerel geliştirme ve test için önerilir. Tam SQL Server kullanılırken bağlantı hesabının mevcut `StockFlow` veritabanında migration uygulama yetkisi bulunmalıdır. LocalDB production veya çok kullanıcılı deployment hedefi değildir.

## İlk kurulum

Depo kökünde aşağıdaki komutları çalıştırın:

```powershell
dotnet restore StockFlow.slnx
dotnet tool restore
dotnet dev-certs https --trust
```

### 1. Güvenli bağlantı yapılandırması

Doğrulanmış LocalDB hedefi `(localdb)\MSSQLLocalDB` üzerindeki `StockFlow` veritabanıdır. Aşağıdaki komut bağlantı değerini kaynak kontrollü dosyaya yazmadan User Secrets içinde saklar:

```powershell
$stockFlowConnection = 'Server=(localdb)\MSSQLLocalDB;' + 'Database=StockFlow;Trusted_Connection=True;TrustServerCertificate=True'
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "$stockFlowConnection" --project StockFlow/StockFlow.csproj
Remove-Variable stockFlowConnection
```

Tam SQL Server kullanıyorsanız aynı anahtara yöneticinizin sağladığı güvenli bağlantı değerini yazın. Ortam değişkeni kullanan hostlarda karşılığı `ConnectionStrings__DefaultConnection` anahtarıdır. Gerçek bağlantı dizesini README, `appsettings*.json`, kaynak kod veya log içine eklemeyin.

### 2. Admin ve Employee hesapları

Identity başlangıç kullanıcıları sabit parola içermez. Aşağıdaki örnek e-postalar güvenli örnek domainini kullanır; parolalar çalıştırma sırasında sizden alınır ve yalnız User Secrets içine yazılır:

```powershell
dotnet user-secrets set "IdentitySeed:Admin:Email" "admin@example.com" --project StockFlow/StockFlow.csproj
$adminSeedPassword = Read-Host "Yerel Admin için güçlü parola"
dotnet user-secrets set "IdentitySeed:Admin:Password" "$adminSeedPassword" --project StockFlow/StockFlow.csproj
Remove-Variable adminSeedPassword

dotnet user-secrets set "IdentitySeed:Employee:Email" "employee@example.com" --project StockFlow/StockFlow.csproj
$employeeSeedPassword = Read-Host "Yerel Employee için güçlü parola"
dotnet user-secrets set "IdentitySeed:Employee:Password" "$employeeSeedPassword" --project StockFlow/StockFlow.csproj
Remove-Variable employeeSeedPassword
```

Güvenli ortam değişkeni kullanan bir hostta karşılık gelen anahtarlar şunlardır:

- `IdentitySeed__Admin__Email`
- `IdentitySeed__Admin__Password`
- `IdentitySeed__Employee__Email`
- `IdentitySeed__Employee__Password`

Dört Identity değeri de zorunludur ve iki hesap farklı e-posta kullanmalıdır. Eksik veya geçersiz yapılandırma uygulamayı hassas değer göstermeden başlangıçta durdurur. Seeder mevcut hesabın parolasını değiştirmez veya eski hesabı silmez; eksik rol üyeliklerini idempotent biçimde tamamlar.

### 3. Migration ve çalıştırma

Uygulama migration'ları otomatik uygulamaz. Önce mevcut migration zincirini veritabanına uygulayın, sonra HTTPS profiliyle uygulamayı başlatın:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project StockFlow/StockFlow.csproj --startup-project StockFlow/StockFlow.csproj
dotnet build StockFlow.slnx --no-restore
dotnet run --project StockFlow/StockFlow.csproj --launch-profile https
```

Varsayılan profil `https://localhost:7141` ve HTTPS'e yönlendirilen `http://localhost:5117` adreslerini kullanır. Güvenli Identity cookie'si nedeniyle tarayıcıda HTTPS adresini açın.

## Development demo verileri

Demo veriler varsayılan olarak kapalıdır. Yalnız `Development` ortamında ve `DemoData:Enabled=true` olduğunda, Identity seed tamamlandıktan sonra yüklenir:

```powershell
dotnet user-secrets set "DemoData:Enabled" "true" --project StockFlow/StockFlow.csproj
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project StockFlow/StockFlow.csproj --launch-profile https
```

Ortam değişkeni kullanılıyorsa eşdeğer anahtar şudur:

```powershell
$env:DemoData__Enabled = "true"
```

Demo kümesi 6 kategori, 30 ürün, 10 müşteri, 6 tedarikçi, 24 sipariş, 54 sipariş kalemi ve 30 stok hareketi oluşturur. Sabit UTC tarihler, farklı fiyat/stok seviyeleri, Draft/Confirmed/Cancelled durumları ve çok kalemli Sale/Purchase siparişleri dashboard, filtreleme, sıralama ve sayfalama akışlarını görünür kılar. Bütün kişi/şirket adları kurgusal; iletişim bilgileri güvenli örnek değerleridir.

Güvenlik davranışı:

- Production ortamında anahtar açık olsa bile demo seed çalışmaz.
- Yedi domain tablosundan herhangi birinde kayıt varsa seed bütünüyle atlanır; mevcut kayıt silinmez, sıfırlanmaz veya güncellenmez.
- Identity kullanıcıları ve rolleri domain boşluk kontrolünü engellemez.
- Başarılı ilk yüklemeden sonra yeniden çalıştırma kayıtları çoğaltmaz.
- Draft ve Cancelled siparişler stok hareketi üretmez. Confirmed hareketler ve güncel stoklar tek transaction içinde matematiksel olarak tutarlı yazılır.

Demo seed'i sonraki başlangıçlar için kapatmak isterseniz:

```powershell
dotnet user-secrets set "DemoData:Enabled" "false" --project StockFlow/StockFlow.csproj
```

Bu komut daha önce oluşturulmuş demo verileri silmez.

## Format, build ve test

Kod biçimini uygulamak, ardından değişiklik olmadığını doğrulamak için:

```powershell
dotnet format StockFlow.slnx --no-restore
dotnet format StockFlow.slnx --verify-no-changes --no-restore
```

Tam doğrulama akışı:

```powershell
dotnet restore StockFlow.slnx
dotnet build StockFlow.slnx --no-restore
dotnet test StockFlow.slnx --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-agent-context.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-repository-hygiene.ps1
git diff --check
git status --short
```

Veritabanına dokunan testler yalnız `(localdb)\MSSQLLocalDB` üzerinde, test başına benzersiz `StockFlow_Tests_<guid>` veritabanı oluşturur; migration uygular ve test sonunda yalnız bu geçici veritabanını siler. Test altyapısı uygulamanın bağlantı ayarlarını veya User Secrets değerlerini okumaz. LocalDB kullanılamıyorsa testler geliştirme veritabanına ya da EF Core InMemory sağlayıcısına geri dönmeden açıkça başarısız olur.

## Veri ve migration notları

- `InitialDomainSchema` migration'ı yedi domain tablosunu oluşturur.
- `AddIdentitySchema` migration'ı Identity tablolarını ve `Orders.CreatedByUserId` ilişkisini ekler.
- Demo veri eklenmesi şemayı değiştirmez ve yeni migration gerektirmez.
- Confirmed ve Cancelled siparişler terminaldir; yeniden açılmaz, düzenlenmez veya fiziksel olarak silinmez.

## Dokümantasyon haritası

- [Ajan bağlam merkezi](docs/ai/README.md)
- [Mevcut teknik durum](docs/ai/current-state.md)
- [Kanonik ürün spesifikasyonu](docs/product-spec.md)
- [Hedef mimari](docs/ai/architecture.md)
- [Domain kuralları](docs/ai/domain-rules.md)
- [Geliştirme iş akışı](docs/ai/development-workflow.md)
- [Mimari karar kayıtları](docs/adr/README.md)

Yapay zekâ ajanları çalışmaya kök [AGENTS.md](AGENTS.md) dosyasından başlamalıdır.
