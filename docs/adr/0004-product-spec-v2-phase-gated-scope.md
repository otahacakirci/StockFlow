---
title: "ADR-0004: Ürün Sözleşmesi v2 ve Faz Kapılı Kapsam Yönetimi"
status: Accepted
authority: decision
last_reviewed: "2026-11-20"
review_triggers:
  - product-scope-change
  - phase-gate-change
  - authority-model-change
---

# ADR-0004: Ürün Sözleşmesi v2 ve Faz Kapılı Kapsam Yönetimi

- Tarih: 2026-11-20
- Durum: Accepted
- Karar sahipleri: Proje sahibi ([issue #2](https://github.com/otahacakirci/StockFlow/issues/2))
- Etkilenen gereksinimler: OKUMA-01..04, SCOPE-01..04, [product-spec.md](../product-spec.md) § 3.1 Tablo 1a, § 10.1 (Faz kapıları, Bonus eşlemesi), § 10.2, § 10.3, § 11.2
- Yerine geçtiği ADR: Yok
- Yerine geçen ADR: Yok

## Bağlam

Ürün sözleşmesi v1.1 yalnız zorunlu MVP'yi, bonus backlog'unu ve kapsam dışı bileşenleri tanımlıyordu. [roadmap.md](../roadmap.md) MVP'yi portföy projesine dönüştüren 7 fazlı bir yol önerir ve bu fazlar v1.1'in kapsam dışı saydığı bileşenleri açar: 4 projeli modüler monolit, yazma amaçlı REST API, JWT, Docker, Testcontainers/CI. `CLAUDE.md` › Değişmez proje sınırları ve Asla yapma listesi bu bileşenleri yasaklıyordu, roadmap ise normatif değildir. "Bu kapsam şu an açık mı?" sorusuna ajanların ve inceleyicilerin denetleyebileceği tek bir kaynak yoktu. Roadmap'i doğrudan uygulamak ürün sözleşmesini sessizce genişletirdi.

## Karar

- `docs/product-spec.md` sürüm 2.0'a yükseltilir. § 4–9 MVP davranış gereksinimleri değişmeden bağlayıcı kalır. Bölüm numaraları korunur.
- Kapsam, § 10.1 Faz kapıları tablosundaki `Durum` sütunuyla (`Kapalı` / `Açık` / `Tamamlandı`) açılır. Bu sütun tek normatif sinyaldir ve yalnız insan onaylı pull request ile, SCOPE-03 koşulları sağlandığında değişir. v2 birleştiğinde Faz 0 `Açık`, Faz 1–6 ve opsiyonel Faz 3.5 `Kapalı` durumdadır.
- Yeni normatif etiket OKUMA-04 `[FAZ n]` eklenir. SCOPE-03 faz açma koşullarını tanımlar. SCOPE-04 roadmap'in tek başına kapsam gerekçesi olmadığını söyler. SCOPE-01'deki "Docker" uygulama konteynerleştirmesi olarak netleşir. Test altyapısında Docker kullanımı (Testcontainers) Faz 0 kapsamıdır.
- Bir fazın ayrıntılı FR/AC maddeleri ve değiştireceği mevcut gereksinimlerin yeni metni, fazı açan PR'da veya faz içindeki ilk PR'da spec'e işlenir. O zamana kadar v1.1 maddeleri bağlayıcıdır.
- § 10.3 iki listeye ayrılır:
  - Kalıcı kapsam dışı: CQRS, MediatR, generic Repository/Unit of Work, AutoMapper, FluentValidation, mikroservisler, mesajlaşma, Redis, caching, background service, SPA, Blazor, SignalR, gRPC, CORS, ayrı frontend, Minimal API, Kubernetes, ödeme, kargo. Bunlar hiçbir faz kapısıyla açılmaz.
  - Faz kapılı: optimistic concurrency (Faz 1); depo transferi, reversal ve Manual Adjustment (Faz 3); rezervasyon (Faz 3.5); JWT/API Identity, yazma amaçlı Web API ve rate limiting (Faz 4); zorunlu Docker/deployment altyapısı (Faz 6). Bunlar yalnız ilgili faz `Açık` iken eklenir.
  - "Clean Architecture" terimi 4 projeli hedefle karıştığı için kalıcı listeden çıkar; somut yasaklı bileşenler kalır.
- v1.1 § 10.2 salt-okunur API bonusu ve Swagger/OpenAPI Faz 4'e eşlenir. Faz 4 açılana kadar § 10.2 metni yalnız referans olarak korunur, uygulanmaz.
- `CLAUDE.md` › Değişmez proje sınırları / Asla yapma, `.claude/rules/*` ve ilgili skill'ler kalıcı ve faz kapılı ayrımını izler. Bugünkü davranış değişmezleri bu kararla gevşetilmez: stok/StockMovement yalnız confirm'de yazılır, InMemory yasağı (ADR-0002) sürer. Bunlar ilgili faz kapısını açan değişiklikte spec § 7 ve yeni ADR ile birlikte güncellenir.
- Deployment hiçbir faza eşlenmez. Bonus olarak kalır ve ancak insan kararıyla, ayrı kapsam değişikliğiyle ele alınır.

## Değerlendirilen alternatifler

- Bütün fazların FR/AC maddelerini v2'ye şimdi yazmak: henüz tasarlanmamış gereksinimleri uydurur ve fazlar ilerledikçe sürekli düzeltme ister.
- Kapsamı yalnız `CLAUDE.md` içinde açmak: ürün sözleşmesini sessizce genişletir. Normatif kaynak ile ajan kuralları ayrışır.
- v2 ile bütün fazları açık saymak, kapıyı yalnız sıra koşulu olarak tutmak: `CLAUDE.md` yasaklarını fiilen kaldırır, denetlenebilir bir sinyal bırakmaz.
- Her faz için ayrı spec sürümü ve bölüm numarası: mevcut kural/skill atıflarını kırar.

## Sonuçlar

- Olumlu: Kapsamın açık olup olmadığı tek tablodan denetlenir. Roadmap öneri olarak kalır. Ajanlar kapalı faz maddesini kapsam bulgusu olarak işaretler.
- Maliyet: Her faz kapısı PR'ı spec, ilgili ADR ve kütüphane güncellemesi ister. Faz 2 proje ayrımından sonra ajan rolleri ve `policy.json` yazma kapsamları yeniden kurulur ([ADR-0003](0003-claude-code-agentic-layer.md) › Sonuçlar).
- Faz 0'ın 4 mühendislik ADR'si (4 projeli mimari, stok eşzamanlılık stratejisi, Sale/Purchase model kararı, hibrit Cookie + JWT) ADR-0005 ve sonrası numaraları alır.
- Kabul edilen risk: § 10.1 ile roadmap zamanla ayrışabilir. Çelişkide § 10.1 geçerlidir.

## Doğrulama

- `docs/product-spec.md` sürüm 2.0, OKUMA-04, SCOPE-03/04, § 10.1 tablosu (Faz 0 `Açık`, diğerleri `Kapalı`) ve § 10.3 iki listeyi içerir. § 4–9 diff'te değişmemiştir.
- `CLAUDE.md` içinde 4 proje, REST API, JWT ve Docker yalnız faz kapılı maddede geçer, kalıcı listede geçmez.
- `node .claude/hooks/agentic/test-guard.mjs` yeşildir.
