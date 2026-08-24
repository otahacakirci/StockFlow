---
title: "StockFlow Geliştirme ve Dokümantasyon İş Akışı"
status: active
authority: operational
last_reviewed: "2026-08-24"
review_triggers:
  - validation-command-change
  - delivery-process-change
  - documentation-policy-change
---

# StockFlow Geliştirme ve Dokümantasyon İş Akışı

## Görev başlangıcı

1. Kök `AGENTS.md`, [bağlam merkezi](README.md), [mevcut durum](current-state.md) ve [handoff](handoff.md) dosyalarını oku.
2. Manifestte görevle eşleşen `loadWhen` belgelerini yükle.
3. Değiştirilecek kodu, çağıranları, yapılandırmayı ve en yakın testleri doğrudan incele.
4. Mevcut davranış, hedef sözleşme ve istenen değişikliği ayrı ayrı tanımla.
5. Kapsam belirsiz değilse en küçük doğru değişikliği uygula; kapsamı bonuslarla genişletme.

## Uygulama sırasında

- Controller, Service, ViewModel, Entity ve DbContext sınırlarını [hedef mimariye](architecture.md) göre koru.
- Domain kuralını mümkün olan en dar Service testiyle kanıtla.
- Entity veya form sözleşmesini değiştirirken doğrulama, mapping, persistence ve UI etkilerini birlikte ele al.
- Hata mesajlarını güvenli; log alanlarını yapılandırılmış ve sorgulanabilir tut.
- Secret veya gerçek başlangıç parolasını örnek dosyalara ekleme.
- Commit veya push öncesinde staged dosyaları repo hijyeni betiğiyle doğrula; eşleşen hassas değeri terminale yazdırma.

## Doğrulama sırası

```powershell
# Bağımlılıklar yalnızca gerektiğinde
dotnet restore StockFlow.slnx

# Uygulama doğrulaması
dotnet build StockFlow.slnx --no-restore
dotnet test StockFlow.slnx --no-restore

# Ajan bağlamı doğrulaması
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-agent-context.ps1

# Staged/tracked repo hijyeni doğrulaması
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-repository-hygiene.ps1
```

Önce değiştirilen davranışa en yakın testleri çalıştır; ardından riskle orantılı geniş doğrulama yap. Test projesi yoksa bunu başarı gibi sunma, mevcut boşluğu açıkça bildir.

## Dokümantasyon güncelleme matrisi

| Değişiklik | Aynı değişiklikte gözden geçir |
| --- | --- |
| Paket, proje, startup veya endpoint | `current-state.md`, gerekirse `architecture.md`, README |
| Entity, enum, ilişki veya invariant | `product-spec.md`, `domain-rules.md`, ilgili ADR |
| Authentication/authorization | `product-spec.md`, `architecture.md`, `current-state.md` |
| Sipariş/stok davranışı | `product-spec.md`, `domain-rules.md`, testler |
| Kalıcı mimari karar | Yeni ADR, etkilenen bağlam belgeleri |
| Build/test/run komutu | README, bu dosya, manifest `validationCommands` |
| Git ignore, staged içerik veya secret politikası | README, bu dosya, `current-state.md`, `handoff.md` |
| Tamamlanan anlamlı görev | `current-state.md` ve/veya `handoff.md` |

Ürün kapsamı yalnızca kullanıcı tarafından yetkilendirilen değişiklikte güncellenir. Teknik uygulama farkı ürün gereksinimini sessizce yeniden yazmaz.

## Handoff kuralları

`handoff.md` kısa ve olgusal tutulur. Sohbet dökümü, spekülatif backlog veya ürün kararı eklenmez. Şu alanlar güncellenir:

- son doğrulama tarihi
- tamamlanan son anlamlı değişiklik
- çalıştırılan doğrulamalar ve sonuçları
- açık risk/bloker
- tek bir sonraki sınırlandırılmış görev veya `Belirlenmedi`

## Definition of Done

- İstenen davranış uygulanmış ve kapsam dışı ekleme yapılmamıştır.
- İlgili testler eklenmiş/güncellenmiş ve çalıştırılmıştır.
- Build/test sonuçları, çalıştırılmayan kontroller dahil, doğru raporlanmıştır.
- Güvenlik ve atomiklik kuralları korunmuştur.
- Güncelleme matrisi uygulanmış, handoff gerekiyorsa yenilenmiştir.
- Ajan bağlam doğrulama betiği geçmiştir.
- Repo başlatılmışsa staged/tracked içerik hijyeni doğrulaması geçmiştir.
