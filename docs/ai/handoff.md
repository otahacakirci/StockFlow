---
title: "StockFlow Görev Handoff Kaydı"
status: rolling
authority: continuity
last_reviewed: "2026-09-01"
review_triggers:
  - meaningful-task-completed
  - blocker-discovered
  - validation-baseline-change
---

# StockFlow Görev Handoff Kaydı

## Son doğrulama

- Tarih: 1 Eylül 2026
- Kapsam: Product Türkçe fiyat giriş zinciri ve Category/Product bağlam koruyan güvenli Edit İptal akışı

## Son tamamlanan değişiklik

Uygulama istek ve UI kültürünü açıkça `tr-TR` olarak sabitler. Product Create/Edit fiyat alanları katı Türkçe decimal binder ve yalnız işaretli input'a uygulanan istemci doğrulaması kullanır: `19,34`, `19,3` ve tam sayılar aynı decimal anlamıyla kabul edilir; `19.34`, binlik ayırıcı ve ikiden fazla ondalık alan hatası üretir. Ham başarısız değer ModelState'te kalır ve Product Service'e ulaşmaz; mevcut DataAnnotations, Service doğrulaması ve `decimal(18,2)` kalıcılık sözleşmesi korunur.

Category ve Product Edit bağlantıları liste ekranında normalize edilmiş arama/filtre/sıralama/sayfa bağlamını, Details ekranında ilgili detay hedefini taşır. Controller GET ve POST'ta dönüş hedefini `Url.IsLocalUrl` ile yeniden doğrular; eksik veya dış hedef ilgili filtresiz listeye düşer. İptal bu güvenli hedefe dönerken başarılı Edit mevcut PRG davranışıyla Details'a yönlenir. Rol sınırları, antiforgery, hata yaklaşımı, Service sözleşmeleri ve veritabanı yapısı değişmemiştir.

## Doğrulama kanıtı

- `dotnet format StockFlow.slnx --verify-no-changes --no-restore`: geçti.
- `dotnet build StockFlow.slnx --no-restore`: geçti; 0 hata ve 0 uyarı.
- Veritabanısız hedefli 63 binder/Product/Category Controller ve Product input testi geçti; başarısız veya atlanan test yoktur.
- İstemci asset'i sözdizimi ve davranış kontrolünde virgüllü/tam sayı girişlerini kabul etti, nokta/binlik/fazla hassasiyeti reddetti ve işaretlenmemiş integer doğrulamasını değiştirmedi.
- Kullanıcıya bağlı gerçek LocalDB bağlamında hedefli on iki ProductService testi ve yüz elli yedi testlik tam paket geçti; başarısız veya atlanan test yoktur.
- Ajan bağlamı, repository hygiene, değişen/yeni dosyalarda hassas içerik ve whitespace ile `git diff --check` doğrulamaları geçti.

## Açık riskler ve boşluklar

- Customer, Supplier, sipariş mutation/query ve StockMovement query Service'leri henüz Controller ve Razor akışlarına bağlanmamıştır.
- Uygulamanın çalışması için migration sonrasında dört `IdentitySeed` değerinin güvenli yapılandırmada bulunması gerekir.
- İlişkisel testler Windows ve çalışan SQL Server LocalDB gerektirir; CI ve çapraz platform hedefi sonraki kapsamdadır.
- LocalDB geliştirme ve öğrenme ortamıdır; production veya çok kullanıcılı deployment için tam SQL Server hedefi ayrıca yapılandırılmalıdır.

## Sonraki sınırlandırılmış görev

Belirlenmedi.
