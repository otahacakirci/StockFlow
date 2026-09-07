---
title: "StockFlow Görev Handoff Kaydı"
status: rolling
authority: continuity
last_reviewed: "2026-09-03"
review_triggers:
  - meaningful-task-completed
  - blocker-discovered
  - validation-baseline-change
---

# StockFlow Görev Handoff Kaydı

## Son doğrulama

- Tarih: 3 Eylül 2026
- Kapsam: Development demo veri başlangıç yüklemesi ve README kurulum belgesi

## Son tamamlanan değişiklik

Identity seed sonrasında çağrılan, varsayılanı kapalı `DemoData:Enabled` bayrağı ve Development ortam kontrolü bulunan özel demo data seeder eklendi. Seeder yedi domain tablosundan herhangi biri doluysa mevcut kayıtlara dokunmadan güvenli logla atlar; boş domain veritabanında bütün veri grafiğini tek serializable SQL transaction ve tek `SaveChangesAsync` ile yazar.

Deterministik katalog; 6 kategori, 30 ürün, 10 müşteri, 6 tedarikçi, 24 sipariş, 54 sipariş kalemi ve 30 stok hareketi içerir. 12 Confirmed, 7 Draft ve 5 Cancelled sipariş; Sale/Purchase tarafları, fiyat snapshot'ları, toplamlar, açılış stokları ve yalnız Confirmed hareketlerin net etkisiyle sekiz düşük stoklu ürün sonucu birlikte korunur. Bütün taraf ve iletişim değerleri açıkça kurgusaldır. Veritabanı modeli, migration zinciri, public Service/ViewModel sözleşmeleri ve sipariş yaşam döngüsü değiştirilmedi.

README mevcut MVC mini ERP kapsamı, rol matrisi, teknoloji/önkoşullar, güvenli LocalDB veya SQL Server bağlantısı, Identity User Secrets/ortam anahtarları, migration/run sırası, demo seed güvenlik davranışı ve format/build/test komutlarıyla güncellendi.

## Doğrulama kanıtı

- `dotnet restore StockFlow.slnx`: sandbox dışı güvenli NuGet yapılandırma erişimiyle geçti; bütün projeler güncel.
- `dotnet format StockFlow.slnx --verify-no-changes --no-restore`: geçti.
- `dotnet build StockFlow.slnx --no-restore`: geçti; 0 hata ve 0 uyarı.
- 303 test keşfedildi.
- Deterministik katalog ile kapalı bayrak/Production erken dönüşünü kapsayan üç odaklı demo veri testi geçti.
- 155 Controller testi geçti; başarısız veya atlanan test yoktur.
- Katalog, Identity yapılandırması, ViewModel/model binding, saf stok planner ve ortak Service yardımcılarını kapsayan 47 veritabanısız test geçti; başarısız veya atlanan test yoktur.
- İlişkisel demo seed testi, `SqlServerTestDatabase.InitializeAsync` içinde LocalDB otomatik örneği oluşturulamadığı için test gövdesinden önce başarısız oldu. Tam paket de Identity DB, Service ve MVC sınıflarında aynı ortam hatasını tekrarladı ve gereksiz tekrar beklememek için durduruldu; InMemory veya geliştirme veritabanı fallback'i kullanılmadı.
- Ajan bağlamı ve 298 tracked dosyayı denetleyen repository hygiene kontrolleri geçti; yasaklı yol veya yüksek güvenli secret eşleşmesi bulunmadı.
- Değiştirilen/yeni 11 dosyanın ek hassas içerik taraması, migration diff kontrolü ve `git diff --check` geçti. Migration dosyası değişmedi.

## Açık riskler ve boşluklar

- Bu makinedeki `(localdb)\MSSQLLocalDB` örneği `Cannot create an automatic instance` / Local Database Runtime hatası veriyor. İlişkisel demo seed, Service, Identity database ve MVC kabul testleri LocalDB onarıldıktan sonra yeniden çalıştırılmalıdır.
- Uygulamanın çalışması için migration sonrasında dört `IdentitySeed` değerinin güvenli yapılandırmada bulunması gerekir.
- LocalDB geliştirme ve öğrenme ortamıdır; production veya çok kullanıcılı deployment hedefi değildir.

## Sonraki sınırlandırılmış görev

LocalDB örneğini depo dışında onarıp ilişkisel demo seed testlerini ve 303 testlik tam paketi fallback kullanmadan yeniden çalıştırmak.
