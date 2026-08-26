# StockFlow

StockFlow; kategori, ürün, tedarikçi, müşteri, satın alma/satış siparişi ve stok hareketlerini yönetecek ASP.NET Core MVC tabanlı bir mini ERP projesidir.

## Mevcut durum

Depodaki uygulama erken aşamadadır. Şu anda .NET 10 MVC/Razor temeli, EF Core domain modeli, LocalDB kalıcılığı, cookie tabanlı ASP.NET Core Identity ve sipariş/stok Service katmanı bulunur. Uygulama özel MVC login/logout akışı kullanır; Admin ve Employee başlangıç kullanıcıları güvenli yapılandırmadan idempotent seed edilir. `OrderService`; Sale/Purchase Draft oluşturma ve düzenleme, sunucu fiyat snapshot'ı, toplam hesaplama, atomik confirm, cancel ve yalnız Draft silme kurallarını yönetir. Yönetim Controller ve ekranları henüz uygulanmamıştır.

Ayrıntılı mevcut-hedef farkları için [mevcut durum belgesine](docs/ai/current-state.md), normatif kapsam için [ürün spesifikasyonuna](docs/product-spec.md) bakın.

## Gereksinimler

- .NET SDK 10.x
- Microsoft SQL Server LocalDB (yerel öğrenme/geliştirme için önerilen) veya tam SQL Server örneği
- PowerShell 5.1 veya PowerShell 7 (bağlam doğrulama betiği için)
- HTTPS geliştirme sertifikası (`dotnet dev-certs https --trust`)

## Kurulum ve çalıştırma

```powershell
dotnet restore StockFlow.slnx
dotnet tool restore
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<yerel geliştirme bağlantı dizeniz>" --project StockFlow/StockFlow.csproj
dotnet user-secrets set "IdentitySeed:Admin:Email" "<yerel admin e-posta adresiniz>" --project StockFlow/StockFlow.csproj
dotnet user-secrets set "IdentitySeed:Admin:Password" "<güçlü yerel admin parolanız>" --project StockFlow/StockFlow.csproj
dotnet user-secrets set "IdentitySeed:Employee:Email" "<yerel employee e-posta adresiniz>" --project StockFlow/StockFlow.csproj
dotnet user-secrets set "IdentitySeed:Employee:Password" "<güçlü yerel employee parolanız>" --project StockFlow/StockFlow.csproj
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project StockFlow/StockFlow.csproj --startup-project StockFlow/StockFlow.csproj
dotnet build StockFlow.slnx --no-restore
dotnet run --project StockFlow/StockFlow.csproj --launch-profile https
```

Varsayılan geliştirme profili `https://localhost:7141` ve HTTPS'e yönlendirilen `http://localhost:5117` adreslerini birlikte kullanır. Kimlik cookie'si yalnız güvenli bağlantıda gönderildiği için uygulamayı `https` profiliyle başlatın. Yerel secret değerlerini kaynak kontrollü ayar dosyalarına yazmayın; User Secrets veya ortam değişkenlerini kullanın.

`InitialDomainSchema` yedi domain tablosunu, ikinci migration olan `AddIdentitySchema` ise `ApplicationUser`, yedi standart Identity tablosu ve `Orders.CreatedByUserId` foreign key'ini oluşturur. Bağlantı dizesi ve dört başlangıç kullanıcı değeri yalnız güvenli yapılandırmada tutulur. Değerlerden biri eksik veya iki kullanıcı aynı e-postayı kullanıyorsa uygulama hassas değer göstermeden başlangıçta durur. Migration uygulama başlangıcında otomatik çalıştırılmaz; önce `database update`, sonra uygulama başlatma sırası izlenmelidir.

Seed işlemi her uygulama başlangıcında rolleri, kullanıcıları ve üyelikleri kontrol eder. Mevcut kayıtlar tekrar oluşturulmaz, mevcut kullanıcının parolası değiştirilmez ve hesaplar otomatik silinmez. Başlangıç e-posta yapılandırmasını değiştirmek eski hesabı kaldırmaz; bu tür hesap bakımı açık yönetim işlemi olarak yapılmalıdır.

### Yerel veritabanı seçimi

Bu depo için doğrulanan geliştirme hedefi `(localdb)\MSSQLLocalDB` üzerindeki `StockFlow` veritabanıdır. LocalDB, Windows tümleşik kimlik doğrulamasıyla kullanıcı hesabı altında çalışır ve yönetici düzeyinde `CREATE DATABASE` yetkisi gerektirmeden migration pratiği yapmayı sağlar. User Secrets değerinde sunucu, veritabanı ve tümleşik kimlik doğrulama bileşenlerini tanımlayın; gerçek bağlantı dizesini kaynak kontrollü dosyalara eklemeyin.

Tam SQL Server veya `SQLEXPRESS` kullanılması da mümkündür. Bu seçenekte ilk `database update` için hesabın veritabanı oluşturma yetkisi olmalı ya da yönetici `StockFlow` veritabanını önceden oluşturup yalnız bu veritabanında migration yetkisi vermelidir. LocalDB geliştirme içindir; deployment veya çok kullanıcılı çalışma ortamı olarak değerlendirilmemelidir.

## Test ve bağlam doğrulama

```powershell
dotnet test StockFlow.slnx --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-agent-context.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-repository-hygiene.ps1
```

`StockFlow.Tests`, veritabanına dokunan her test için `(localdb)\MSSQLLocalDB` üzerinde `StockFlow_Tests_<guid>` adlı benzersiz bir veritabanı oluşturur, gerçek migration zincirini uygular ve test sonunda veritabanını siler. Test altyapısı uygulama ayarlarını, User Secrets değerlerini veya ortam connection string'lerini okumaz; yalnız sabit LocalDB örneğini ve güvenli test veritabanı adı biçimini kabul eder. LocalDB kullanılamıyorsa testler geliştirme veritabanına veya InMemory sağlayıcısına geri dönmeden açıklayıcı bir önkoşul hatasıyla başarısız olur.

On sekiz xUnit testi bulunur. Mevcut altyapı ve Identity testlerine eklenen on `OrderService` testi; Sale/Purchase Draft akışlarını, fiyat snapshot'ını, toplamı, stok değişmezliğini, iki yönlü confirm hareketlerini, yetersiz stok ve kalıcılaştırma hatasında rollback'i, cancel/silme davranışını, terminal durumları ve hata kategorilerini kapsar. İlk test altyapısı Windows ve SQL Server LocalDB gerektirir; CI ve çapraz platform çalışması sonraki kapsamdadır.

## GitHub öncesi repo hijyeni

`.gitignore`, Visual Studio kullanıcı dosyalarını, build/test çıktılarını, yerel ortam dosyalarını, sertifika/anahtarları ve SQL veri dosyalarını dışlar. `scripts/validate-repository-hygiene.ps1`, staged veya tracked dosyalarda yasaklı yolları ve yüksek güvenli secret kalıplarını değerleri göstermeden denetler.

GitHub'a göndermeden önce en az şu kontrolleri çalıştırın:

```powershell
git diff --cached --check
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-repository-hygiene.ps1
git status --short
```

Gerçek connection string, parola, token veya sertifika kaynak kontrollü dosyalara eklenmemeli; User Secrets veya ortam değişkenlerinde tutulmalıdır.

## Dokümantasyon haritası

- [Ajan bağlam merkezi](docs/ai/README.md)
- [Kanonik ürün spesifikasyonu](docs/product-spec.md)
- [Hedef mimari](docs/ai/architecture.md)
- [Yüksek frekanslı domain kuralları](docs/ai/domain-rules.md)
- [Geliştirme ve dokümantasyon iş akışı](docs/ai/development-workflow.md)
- [Mimari karar kayıtları](docs/adr/README.md)

Yapay zekâ ajanları çalışmaya kök [AGENTS.md](AGENTS.md) dosyasından başlamalıdır.
