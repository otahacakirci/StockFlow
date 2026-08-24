# StockFlow

StockFlow; kategori, ürün, tedarikçi, müşteri, satın alma/satış siparişi ve stok hareketlerini yönetecek ASP.NET Core MVC tabanlı bir mini ERP projesidir.

## Mevcut durum

Depodaki uygulama erken aşamadadır. Şu anda .NET 10 MVC/Razor temeli, EF Core domain modeli, migration ve LocalDB kalıcılığı bulunur. Geçici JWT, yazma API'si ve bellek içi demo kullanıcı/ürün prototipi GitHub öncesi güvenlik temizliğinde kaldırılmıştır. Cookie tabanlı ASP.NET Core Identity, Service katmanı, yönetim ekranları ve xUnit kanıtları henüz uygulanmamıştır.

Ayrıntılı mevcut-hedef farkları için [mevcut durum belgesine](docs/ai/current-state.md), normatif kapsam için [ürün spesifikasyonuna](docs/product-spec.md) bakın.

## Gereksinimler

- .NET SDK 10.x
- Microsoft SQL Server LocalDB (yerel öğrenme/geliştirme için önerilen) veya tam SQL Server örneği
- PowerShell 5.1 veya PowerShell 7 (bağlam doğrulama betiği için)

## Kurulum ve çalıştırma

```powershell
dotnet restore StockFlow.slnx
dotnet tool restore
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<yerel geliştirme bağlantı dizeniz>" --project StockFlow/StockFlow.csproj
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project StockFlow/StockFlow.csproj --startup-project StockFlow/StockFlow.csproj
dotnet build StockFlow.slnx --no-restore
dotnet run --project StockFlow/StockFlow.csproj
```

Varsayılan geliştirme profilleri `http://localhost:5117` ve `https://localhost:7141` adreslerini kullanır. Yerel secret değerlerini kaynak kontrollü ayar dosyalarına yazmayın; user secrets veya ortam değişkenlerini kullanın.

İlk migration yedi domain tablosunu oluşturur; sekizinci çekirdek entity olan `ApplicationUser` ve Identity tabloları sonraki kimlik aşamasındadır. Bağlantı dizesi `DefaultConnection` adıyla güvenli yapılandırmada tutulur; uygulama başlangıcında otomatik migration veya `EnsureCreated` çalıştırılmaz. Şemanın ayrıntıları ve ERD için [veritabanı şeması belgesine](docs/database-schema.md) bakın.

### Yerel veritabanı seçimi

Bu depo için doğrulanan geliştirme hedefi `(localdb)\MSSQLLocalDB` üzerindeki `StockFlow` veritabanıdır. LocalDB, Windows tümleşik kimlik doğrulamasıyla kullanıcı hesabı altında çalışır ve yönetici düzeyinde `CREATE DATABASE` yetkisi gerektirmeden migration pratiği yapmayı sağlar. User Secrets değerinde sunucu, veritabanı ve tümleşik kimlik doğrulama bileşenlerini tanımlayın; gerçek bağlantı dizesini kaynak kontrollü dosyalara eklemeyin.

Tam SQL Server veya `SQLEXPRESS` kullanılması da mümkündür. Bu seçenekte ilk `database update` için hesabın veritabanı oluşturma yetkisi olmalı ya da yönetici `StockFlow` veritabanını önceden oluşturup yalnız bu veritabanında migration yetkisi vermelidir. LocalDB geliştirme içindir; deployment veya çok kullanıcılı çalışma ortamı olarak değerlendirilmemelidir.

## Test ve bağlam doğrulama

```powershell
dotnet test StockFlow.slnx --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-agent-context.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-repository-hygiene.ps1
```

Solution içinde henüz bir xUnit test projesi yoktur; `dotnet test` komutu test projesi eklendiğinde aynı giriş noktası üzerinden çalışacaktır.

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
