# StockFlow Agent Guide

Bu dosya depo genelindeki yapay zekâ ajanları için kısa giriş noktasıdır. Ayrıntıyı burada çoğaltma; ilgili kanonik belgeyi oku ve güncelle.

## Başlangıç sırası

1. `docs/ai/README.md` içindeki bağlam haritasını oku.
2. Her görevde `docs/ai/current-state.md` ve `docs/ai/handoff.md` dosyalarını oku; ardından kodu doğrudan inceleyerek güncelliği doğrula.
3. Özellik, kapsam, mimari, güvenlik veya iş kuralı değişikliğinde `docs/product-spec.md` dosyasını oku.
4. Kalıcı bir tasarım kararında `docs/adr/README.md` ve geçerli ADR'leri oku.
5. `docs/ai/context-manifest.json` içindeki `loadWhen` ve `reviewTriggers` alanlarını görev yönlendirmesi olarak kullan.

## Kaynakların yetkisi

- **Hedef davranış ve kapsam:** `docs/product-spec.md` normatiftir.
- **Bugünkü çalışan gerçek:** Kod, yapılandırma, migration ve testlerdir. `current-state.md` yalnızca bunların açıklayıcı özetidir.
- **Kalıcı karar:** Kabul edilmiş ADR'dir; ürün sözleşmesini sessizce zayıflatamaz veya genişletemez.
- **Görev devamlılığı:** `handoff.md` bağlam taşır; gereksinim ya da mimari otorite değildir.
- Kod ile ürün sözleşmesi çelişiyorsa bunu mevcut-hedef farkı olarak ele al; mevcut prototipi onaylanmış hedef sayma.
- Kullanıcı ürün sözleşmesini değiştirirse aynı değişiklik kümesinde spesifikasyonu, ilgili ADR'yi ve etkilenen bağlam belgelerini güncelle.

## Değişmez proje sınırları

- Zorunlu MVP tek ASP.NET Core MVC uygulamasıdır: Razor/Bootstrap UI, Service iş kuralları, EF Core `ApplicationDbContext`, SQL Server ve cookie tabanlı ASP.NET Core Identity.
- Controller doğrudan `ApplicationDbContext` kullanmaz; Entity nesneleri kullanıcı girdisi olarak kabul edilmez.
- Draft sipariş stok değiştirmez. Confirm işlemi stok, `StockMovement` ve sipariş durumunu tek atomik sınırda yazar.
- Confirmed ve Cancelled terminal durumlardır.
- JWT, yazma amaçlı Web API, Clean Architecture, CQRS, MediatR, generic Repository/Unit of Work, AutoMapper, FluentValidation, mikroservis ve benzeri kapsam dışı bileşenleri açık kapsam değişikliği olmadan ekleme.
- Bonusları ancak `docs/product-spec.md` içindeki öncelik kapıları sağlandığında ele al.

## Çalışma döngüsü

1. İlgili belgeleri ve kodu incele; varsayımı gerçek gibi sunma.
2. Değişikliği küçük, izlenebilir ve mevcut kapsama bağlı tut.
3. İş kurallarını Service katmanında, giriş/çıktı sözleşmelerini ViewModel'lerde uygula.
4. En dar ilgili testleri, ardından uygun olduğunda solution build/test komutlarını çalıştır.
5. Davranış veya yapı değiştiyse `current-state.md`; görev devri gerekiyorsa `handoff.md`; kalıcı karar varsa ADR; kapsam değiştiyse ürün spesifikasyonunu güncelle.
6. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-agent-context.ps1` komutunu çalıştır.

## Güvenlik ve kalite

- Secret, parola, connection string, token veya gereksiz kişisel veriyi kaynak koda, belgeye, örnek çıktıya ya da loga yazma.
- Mevcut hassas değerleri cevaplarda tekrarlama; yalnızca konum ve risk türünü belirt.
- Kullanıcıya güvenli mesaj, loga sorgulanabilir teknik bağlam üret; production'da stack trace sızdırma.
- Belgeler Türkçe, kod sembolleri ve dosya adları İngilizce kalır.
- Alakasız kullanıcı değişikliklerini geri alma ve uygulama davranışını istek kapsamı dışında düzeltme.

## Doğrulama komutları

```powershell
dotnet restore StockFlow.slnx
dotnet build StockFlow.slnx --no-restore
dotnet test StockFlow.slnx --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-agent-context.ps1
```
