# Düzenleme disiplini

Değişiklikleri küçük, izlenebilir ve istenen kapsamla sınırlı tutar; incelemeyi yapan kişi ilgisiz farkları reddeder.

## Kapsam

- Yalnız görevin gerektirdiği dosyaları değiştir; ilgisiz yeniden biçimlendirme, yeniden adlandırma veya refactor yapma; başkasının alakasız değişikliklerini geri alma — `CLAUDE.md` › Asla yapma.
- Gördüğün fakat görev dışında kalan borç veya hatayı düzeltme; raporunda **Gözlemler** başlığı altında `path:line` ile bildir. Örnek bilinen borç: `StockFlow/Services/Orders/OrderService.cs` 950 satırdır; kapsam dışında bölme.
- Çevredeki kodu, aynı klasördeki dosyaları örnek alarak taklit et; tek bir stil kuralını bütün repoya genelleme. Gözlenen kalıplar: Controller ve Service'ler primary constructor ile DI alan `sealed` sınıflardır (`StockFlow/Controllers/CategoriesController.cs:13`), entity ve EF configuration sınıfları `sealed` değildir; Türkçe XML doc yorumları Service katmanında kullanılır (`StockFlow/Services/Common/ServiceResult.cs:3`), Controller/Entity/ViewModel dosyalarında kullanılmaz; küçük yardımcı tipler aynı dosyada durabilir (`StockFlow/Services/Common/ServiceResult.cs:6`, `:39`).
- Kod sembolleri ve dosya adları İngilizce, belgeler ve kullanıcıya görünen metin Türkçe — `CLAUDE.md`.
- Biçim `dotnet format` ile doğrulanır (`dotnet format StockFlow.slnx --verify-no-changes --no-restore`); repoda `.editorconfig` yoktur, stil kuralı uydurma.

## Dosyalar

- Açık onay olmadan dosya silme.
- Elle düzenlenmeyen dosyalar: `StockFlow/Data/Migrations/*.Designer.cs`, `StockFlow/Data/Migrations/ApplicationDbContextModelSnapshot.cs` (`dotnet ef` üretir) ve `StockFlow/wwwroot/lib/**` (üçüncü taraf; `.gitattributes`).
- `docs/roadmap.md` önerilen yönü anlatır; bir roadmap maddesini yalnız `docs/product-spec.md` › 10.1'de fazı `Açık` ise ve fazın gereksinimleri spec'e işlendiyse uygula.

## Dokümantasyon

Davranış veya yapı değiştiğinde aynı değişiklik kümesinde gözden geçir:

| Değişiklik | Gözden geçir |
| --- | --- |
| Paket, proje, startup veya endpoint | README; etkilenen `.claude/rules/*` |
| Entity, enum, ilişki, invariant veya migration | `docs/product-spec.md` › 6, `docs/database-schema.md`, ilgili ADR, `.claude/rules/data.md` |
| Authentication/authorization veya rol matrisi | `docs/product-spec.md` › 4, README › Roller, `.claude/rules/web-layer.md` |
| Sipariş/stok davranışı | `docs/product-spec.md` › 7, `.claude/rules/orders-stock.md`, testler |
| Kalıcı mimari karar | Yeni ADR (`docs/adr/0000-template.md`) ve `docs/adr/README.md` indeksi |
| Build/test/run komutu | README, `CLAUDE.md` › Komutlar, `.claude/hooks/agentic/policy.json` (`verifyCommand`, insan onayıyla) |
| Git ignore, staged içerik veya secret politikası | README, `scripts/validate-repository-hygiene.ps1`, `.claude/rules/security.md` |

- Ürün kapsamı (`docs/product-spec.md`) yalnız kullanıcı yetkisiyle değişir; teknik fark gereksinimi sessizce yeniden yazmaz.
- `.claude/` kütüphanesi değişiklikleri `/curate-knowledge` akışıyla yapılır.
- Belgelerde yerel Markdown bağlantısı eklerken hedefin var olduğunu kontrol et; artık otomatik bağlantı doğrulayıcısı yoktur.
