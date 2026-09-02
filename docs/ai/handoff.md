---
title: "StockFlow Görev Handoff Kaydı"
status: rolling
authority: continuity
last_reviewed: "2026-09-02"
review_triggers:
  - meaningful-task-completed
  - blocker-discovered
  - validation-baseline-change
---

# StockFlow Görev Handoff Kaydı

## Son doğrulama

- Tarih: 2 Eylül 2026
- Kapsam: Rol ayrımlı Customer MVC Controller ve Razor yönetim akışı

## Son tamamlanan değişiklik

`CustomersController`, mevcut `ICustomerService` sözleşmesini liste/detay/create/edit/delete Razor akışlarına bağlar. Admin ve Employee listeleme, görüntüleme, oluşturma ve düzenleme endpoint'lerini kullanabilir; Delete GET/POST yalnız Admin rolüne açıktır. Employee UI silme bağlantısı veya bölümü üretmez. Admin için de sipariş geçmişi bulunan Customer'da silme formu gösterilmez; yarış sırasında oluşan Service `BusinessRule` sonucu güncel onay görünümüyle HTTP 409 döner.

Customer listesi ad/e-posta/telefon araması, enum tabanlı ad sıralaması, normalize sayfalama ve filtre korumalı gezinme sunar. Formlar yalnız güvenli ViewModel kullanır; Türkçe DataAnnotations mesajları mevcut uzunluk, biçim ve opsiyonellik sözleşmesini korur. Validation alan bazlı forma, NotFound HTTP 404'e çevrilir; gerçek exception'lar Controller'da yakalanmadan merkezi hata yaklaşımına yayılır. Normatif ürün rol matrisi ve domain rol özeti Admin-only Customer silme ayrımını açıkça taşır.

## Doğrulama kanıtı

- `dotnet format StockFlow.slnx --verify-no-changes --no-restore`: geçti.
- `dotnet build StockFlow.slnx --no-restore`: geçti; 0 hata ve 0 uyarı.
- Veritabanısız hedefli 26 `CustomersController` ve beş `CustomerInputModel` testi geçti; başarısız veya atlanan test yoktur.
- Kullanıcıya bağlı gerçek LocalDB bağlamında hedefli on üç `CustomerService` testi ve 188 testlik tam paket geçti; başarısız veya atlanan test yoktur.
- Ajan bağlamı, repository hygiene, değişen/yeni dosyalarda hassas içerik ve whitespace ile `git diff --check` doğrulamaları geçti.

## Açık riskler ve boşluklar

- Supplier, sipariş mutation/query ve StockMovement query Service'leri henüz Controller ve Razor akışlarına bağlanmamıştır.
- Uygulamanın çalışması için migration sonrasında dört `IdentitySeed` değerinin güvenli yapılandırmada bulunması gerekir.
- İlişkisel testler Windows ve çalışan SQL Server LocalDB gerektirir; CI ve çapraz platform hedefi sonraki kapsamdadır.
- LocalDB geliştirme ve öğrenme ortamıdır; production veya çok kullanıcılı deployment için tam SQL Server hedefi ayrıca yapılandırılmalıdır.

## Sonraki sınırlandırılmış görev

Belirlenmedi.
