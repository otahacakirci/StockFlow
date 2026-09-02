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
- Kapsam: Admin/Employee salt-okunur Order MVC listeleme ve detay akışı

## Son tamamlanan değişiklik

`OrdersController`, mevcut `IOrderQueryService` sözleşmesini yalnız Index ve Details GET akışlarına bağlar. Controller Admin ve Employee rollerine açıktır; gerçek ASP.NET Core authorization policy iki rolü kabul edip izin verilmeyen rolü reddeder. Controller `IOrderService`, DbContext, POST veya başka mutasyon endpoint'i taşımaz.

Sipariş listesi OrderType/OrderStatus filtreleri, enum tabanlı tarih sıralaması, normalize sayfalama ve filtre korumalı gezinme sunar. Sale için Customer, Purchase için Supplier gösterilir; detay ekranı Product adı/SKU'su, miktar, fiyat snapshot'ı ve satır toplamlarını güvenli çıkış modelleriyle taşır. Sipariş NotFound HTTP 404'e, tutarsız tipli sonuç güvenli HTTP 500'e çevrilir; gerçek exception'lar Controller'da yakalanmaz.

## Doğrulama kanıtı

- `dotnet format StockFlow.slnx --verify-no-changes --no-restore`: geçti.
- `dotnet build StockFlow.slnx --no-restore`: geçti; 0 hata ve 0 uyarı.
- Veritabanısız hedefli on iki `OrdersController` senaryosu geçti; başarısız veya atlanan test yoktur.
- Kullanıcıya bağlı gerçek LocalDB bağlamında sekiz `OrderQueryService`, on dört `OrderService`/planner regresyonu ve 232 testlik tam paket geçti; başarısız veya atlanan test yoktur.
- Ajan bağlamı, repository hygiene, on bir değişen/yeni dosyada hassas içerik, salt-okunur Razor eylemleri, sayfalama route'ları ve `git diff --check` doğrulamaları geçti; 266 dosyada yasaklı yerel/üretilmiş yol veya yüksek güvenli secret eşleşmesi bulunmadı.

## Açık riskler ve boşluklar

- Sipariş mutation ve StockMovement query Service'leri henüz Controller ve Razor akışlarına bağlanmamıştır.
- Uygulamanın çalışması için migration sonrasında dört `IdentitySeed` değerinin güvenli yapılandırmada bulunması gerekir.
- İlişkisel testler Windows ve çalışan SQL Server LocalDB gerektirir; CI ve çapraz platform hedefi sonraki kapsamdadır.
- LocalDB geliştirme ve öğrenme ortamıdır; production veya çok kullanıcılı deployment için tam SQL Server hedefi ayrıca yapılandırılmalıdır.

## Sonraki sınırlandırılmış görev

Sipariş Draft oluşturma/düzenleme mutasyon akışlarından önce StockMovement salt-okunur MVC listeleme ve detay ekranlarını mevcut query Service'e bağlamak.
