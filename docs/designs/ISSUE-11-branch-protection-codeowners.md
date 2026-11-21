# ISSUE-11: `main` için branch protection ve CODEOWNERS yapılandırması

- Kaynak: https://github.com/otahacakirci/StockFlow/issues/11 (yorum yok; Faz 0, iş kalemi 0.10)
- Tarih: 2026-11-21
- Paketler: Data ☐ · Service ☐ · Web ☐ · Tests ☐ · Onaylı ☑

## İstenen

`main` dalı için PR zorunlu, CI kontrolleri zorunlu, "Require review from Code Owners" açık ve doğrudan push kapalı olmalı. `.github/CODEOWNERS` korumalı yolları (`docs/product-spec.md`, `.github/**`, `scripts/**`, `.claude/**`, `*.csproj`, `Program.cs`) kapsamalı. Ayar bir repo yönetici işlemidir (GitHub API/arayüz); ajan yalnız CODEOWNERS dosyasını ve talimatı hazırlar. Doğrulama notu (`gh api` çıktısı) issue'ya eklenir; README'de akış özeti bulunur.

Yararlanan: proje sahibi; ajan guard'ı Windows'ta OS sandbox'ı olmadığı için "en iyi çaba"dır ve CODEOWNERS ile branch protection "son savunma hattı"dır (`docs/adr/0003-claude-code-agentic-layer.md:46`). Kapsam: Faz 0 durumu `Açık` (`docs/product-spec.md:587`, çıkış kanıtında "`main` branch protection etkin"); `docs/roadmap.md:154` aynı işi anlatır ama normatif değildir, dayanak spec satırıdır. Spec/FR/AC değişikliği gerekmez; kapsam değişikliği bulgusu yoktur. Bağımlılık #10 kapalıdır: `ci.yml` `origin/main` içindedir ve `main` üzerindeki son koşu yeşildir (run 44895160325).

## Bilinmesi gereken mevcut mantık

- `.github/CODEOWNERS` zaten vardır (13 satır): `/CLAUDE.md`, `/.claude/`, `/.github/`, `/docs/adr/`, `/scripts/`, `/docs/product-spec.md`, `/docs/roadmap.md` hepsi `@otahacakirci`. `gh api repos/otahacakirci/StockFlow/codeowners/errors` bugün `{"errors":[]}` döner (dosya geçerli). Issue listesinden eksik kalanlar: `*.csproj` ve `Program.cs`. `.github/**` zaten `/.github/` ile kapsanır; ajan kütüphanesi bloğu (`:1-8`) "agentic setup" işaretçileriyle sarılıdır, yeni satırlar bu bloğun dışına eklenir (`:10-12` bölümü örnek).
- `.claude/hooks/agentic/policy.json:22` `.github/CODEOWNERS`'ı guardrail yolu sayar, `:24-36` `approvalPaths` ve `:37-43` `libraryPaths` listeler; subagent'lar bu dosyaları yazamaz (`guard.mjs:103`). Dolayısıyla CODEOWNERS düzenlemesi de "Onaylı" paketindedir (ana oturum + insan); `planner`/implementer yazamaz.
- Branch protection yoktur: `gh api repos/otahacakirci/StockFlow/branches/main/protection` → 404 "Branch not protected"; `gh api repos/otahacakirci/StockFlow/rulesets` → `[]`. Repo herkese açık (`private: false`), sahibi tür `User`, çağıran hesabın `admin` izni var. PR #93 hiç review almadan (`reviews: []`) yazar tarafından merge edildi: bugün koruma yok.
- CI check adları: `ci.yml` işlerinde `name:` alanı yoktur, bu yüzden check adı iş kimliğidir: `dotnet` ve `repo-guards` (`.github/workflows/ci.yml:21`, `:86`). Doğrulandı: `gh api repos/otahacakirci/StockFlow/commits/main/check-runs` → `dotnet success`, `repo-guards success`. İş kimlikleri değişirse zorunlu kontrol ayarı bozulur.
- `docs/designs/ISSUE-10-github-actions-ci.md` hâlâ duruyor. Orada "yeşil koşudan sonra" bekleyen işler bu depoda henüz yapılmamış: `README.md` başında CI rozeti yok, "Format, build ve test" bölümünde (`README.md:145-168`) CI notu yok; `CLAUDE.md:44` hâlâ "CI henüz yok (#10)" der; `.claude/skills/review-changes/SKILL.md:17` "repoda CI yok" der; `docs/adr/0009-testcontainers-mssql-test-databases.md:91` "Faz 0 CI'ı (#10) ile kalıcı olarak doğrulanacaktır" der. Bunlar #10'un artıklarıdır, bu işin kabul ölçütü değildir; README aynı dosyada değişeceği için tek PR'da birleştirmek Açık soru 7'dedir.
- CONTRIBUTING dosyası yoktur (`ls CONTRIBUTING*` boş). README'de dal/PR akışı özeti yoktur; en yakın metin `README.md:184-186` ("main'e push yasağı" cümlesi) ve `CLAUDE.md` › Git akışı bölümüdür (PR ile teslim, push insan onayı, merge/approve insanda).
- Tek sahipli CODEOWNERS ile çelişki: PR yazarı kendi PR'ını onaylayamaz. Bütün PR'ları aynı kişi açtığı için "code owner review" tek başına yönetici atlamasız karşılanamaz (Açık soru 1; tasarımın en önemli kararı).

## Geçerli kurallar

- `.claude/rules/security.md` › Commit öncesi: hijyen betiği `.md` dosyalarını da tarar; belgelere token/parola yazma. Bu tasarımdaki `gh api` yükünde secret yoktur.
- `.claude/rules/editing-discipline.md` › Dokümantasyon: "Git ignore, staged içerik veya secret politikası" ve "Kalıcı mimari karar" satırları; ayrıca › Dosyalar (açık onay olmadan silme yok). README, insan onaylıdır.
- `CLAUDE.md` › Ajan sistemi: `.github/**`, README insan onayı; `CLAUDE.md`/`.claude/**` değişikliği `/curate-knowledge` akışıyla. › Git akışı: `main`'e push, approve ve merge insandadır; bu iş o kuralı GitHub tarafında zorlar, değiştirmez.
- `CLAUDE.md` › Öncelik sırası: oturumdaki insan talimatı kazanır; ama "Git akışı" ve "Asla yapma" bu işle gevşemez.
- Yeni ADR gerekmez (Açık soru 6): karar `ADR-0003:46` ile uyumludur, ürün sözleşmesini daraltmaz/genişletmez.

## Bölüm

| # | Gereksinim / kabul ölçütü | Paket (sahip rol) | Neden orada |
| --- | --- | --- | --- |
| 1 | `.github/CODEOWNERS` korumalı yolları kapsıyor (`docs/product-spec.md`, `.github/**`, `scripts/**`, `.claude/**`, `*.csproj`, `Program.cs`) | Onaylı (ana oturum + insan): `.github/CODEOWNERS` | Guardrail yolu (`policy.json:22`); subagent yazamaz. Eksik olanlar `*.csproj`, `Program.cs` (+ Açık soru 3'teki ek yollar) |
| 2 | Branch protection: PR + zorunlu CI check (`dotnet`, `repo-guards`) + code owner review etkin | Onaylı (insan, GitHub repo yöneticisi): ruleset, aşağıdaki Bölüm A.2 | Repo ayarıdır; ajan `gh api` ile değiştiremez (kabuk salt-okunur) |
| 3 | Doğrudan `main` push'unun engellendiğini gösteren doğrulama notu issue'ya ekli | Onaylı (insan): `gh api` çıktısı + reddedilen push kanıtı, A.3 | Push ve issue yorumu ajan yetkisi dışındadır |
| 4 | README'de akış özeti | Onaylı (ana oturum + insan): `README.md` (A.4) | README insan onaylı; CONTRIBUTING dosyası açılmaz (Açık soru 5) |

Data, Service, Web ve Tests paketlerinde iş yoktur; hiçbir `StockFlow/**` veya `StockFlow.Tests/**` dosyası değişmez.

## Data işi

Yok.

## Service işi

Yok.

## Web işi

Yok.

## Onaylı değişiklikler

Hepsi ana oturumda insan onayıyla yapılır; implementer rolü yoktur. Dal: `chore/branch-protection-codeowners` (bu tasarım yazılırken `origin/main` ile aynı, çalışma ağacı temiz; uygulamadan önce `git branch --show-current` ile doğrula).

### A.1 `.github/CODEOWNERS` (PR #1 içinde)

Mevcut içeriğe dokunmadan, "agentic setup" bloğunun dışına şu bölüm eklenir (son eşleşen desen kazanır; hepsi aynı sahip olduğundan çakışma yoktur). Kök-bağlı desenler kullanılır; `Program.cs` yalın yazılırsa ağaçtaki her `Program.cs` eşleşirdi:

```text
# Derleme, başlangıç ve yapılandırma sınırı: insan onayı isteyen yollar (CLAUDE.md › Ajan sistemi, policy.json approvalPaths).
*.csproj                                   @otahacakirci
/StockFlow/Program.cs                      @otahacakirci
/StockFlow.slnx                            @otahacakirci
/.config/dotnet-tools.json                 @otahacakirci
/StockFlow/appsettings*.json               @otahacakirci
/StockFlow/Properties/launchSettings.json  @otahacakirci
/.gitignore                                @otahacakirci
/.gitattributes                            @otahacakirci
```

İlk iki satır issue'da açıkça vardır (zorunlu); kalanlar `policy.json:24-36` `approvalPaths` ile hizalamak için varsayımdır (Açık soru 3). `.github/**` (workflow'lar dahil), `scripts/**`, `.claude/**`, `docs/product-spec.md` ve `CLAUDE.md` mevcut satırlarla kapsanır; yeni satır gerekmez. Dosya değiştikten sonra `gh api repos/otahacakirci/StockFlow/codeowners/errors` yine `{"errors":[]}` dönmelidir (dosya `main`'e indikten sonra kontrol edilir; API varsayılan daldaki dosyayı okur).

### A.2 Ruleset (insan, GitHub; PR #1 `main`'e merge edildikten sonra)

Sıralama zorunludur: GitHub code owner gereksinimini PR'ın hedef dalındaki CODEOWNERS'a göre uygular; yeni satırlar `main`'e inmeden etkin olmaz. Ayrıca koruma PR #1'den önce açılırsa README ve CODEOWNERS PR'ı kendi kuralına takılır.

Varsayılan tasarım klasik branch protection yerine **ruleset** kullanır, çünkü tek sahipli repoda ("PR zorunlu + code owner review") ile ("yönetici dahil doğrudan push yasak") birlikte sağlanabilen tek biçim, atlama yetkisini yalnız "PR üzerinden" vermektir (`bypass_mode: pull_request`). Klasik korumada "yöneticiler için de uygula" açılırsa sahip hiçbir PR'ı merge edemez; kapatılırsa sahip doğrudan push yapabilir ve 3. kabul ölçütü sağlanmaz. Payload (secret yok; `actor_id: 5` GitHub'ın sabit "Admin" repo rol kimliğidir):

```bash
gh api --method POST repos/otahacakirci/StockFlow/rulesets --input - <<'JSON'
{
  "name": "main-protection",
  "target": "branch",
  "enforcement": "active",
  "conditions": { "ref_name": { "include": ["~DEFAULT_BRANCH"], "exclude": [] } },
  "bypass_actors": [
    { "actor_id": 5, "actor_type": "RepositoryRole", "bypass_mode": "pull_request" }
  ],
  "rules": [
    { "type": "deletion" },
    { "type": "non_fast_forward" },
    { "type": "pull_request", "parameters": {
        "required_approving_review_count": 1,
        "dismiss_stale_reviews_on_push": true,
        "require_code_owner_review": true,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false
    } },
    { "type": "required_status_checks", "parameters": {
        "strict_required_status_checks_policy": true,
        "required_status_checks": [ { "context": "dotnet" }, { "context": "repo-guards" } ]
    } }
  ]
}
JSON
```

Uygulayıcı payload alanlarını güncel şemaya (`gh api repos/otahacakirci/StockFlow/rulesets` yanıtı / GitHub REST "rulesets" belgesi) karşı doğrular; ruleset önce `"enforcement": "evaluate"` ile denenip `active`e çevrilebilir. Ayarlar kod değildir; geri alma `gh api --method DELETE repos/otahacakirci/StockFlow/rulesets/<id>` (insan işi).

Bilinçli seçimler (hepsi Açık sorularda): `required_approving_review_count: 1`, `strict` açık (dal güncel olmadan merge yok), `dismiss_stale_reviews_on_push` açık, yalnız yönetici rolü PR üzerinden atlayabilir (sahibin kendi PR'ları için, atlama PR sayfasında görünür ve denetlenir), force-push ve silme kapalı. Issue'da bu dördü sayılmaz; yalnız "PR + CI + code owner + direct push yasağı" istenir.

### A.3 Doğrulama notu (insan, ruleset'ten sonra)

Aşağıdaki çıktılar issue yorumuna eklenir (token/başlık içermeyen çıktılar; ekran görüntüsü yerine metin yeterli):

1. `gh api repos/otahacakirci/StockFlow/rules/branches/main` — dört kuralı (`deletion`, `non_fast_forward`, `pull_request` içinde `require_code_owner_review: true`, `required_status_checks` içinde `dotnet` ve `repo-guards`) gösterir.
2. `gh api repos/otahacakirci/StockFlow/rulesets` — `main-protection` kaydı, `enforcement: active`, `bypass_actors` yalnız `pull_request` modunda.
3. Doğrudan push reddi: insan, yerelde `main`'e değil, `origin/main`'den türeyen boş bir commit ile `git push origin HEAD:main` dener; beklenen sonuç `GH013: Repository rule violations found` (veya "Changes must be made through a pull request") reddidir. Bu adım yalnız adım 1-2 `active` gösterdikten sonra yapılır; kural gerçekte etkin değilse boş commit `main`'e iner ve geri alması force-push gerektirir, bu yüzden adım 1-2 atlanmaz. Push insan onayı ister; ajan bunu yapamaz.
4. Test PR'ı: `.csproj` dokunan geçici bir PR (ör. yorum satırı eklenmiş, merge edilmeyecek) "Code owner review required" ve iki zorunlu kontrolün "Expected"/yeşil durumunu gösterir; PR kapatılır, dalı insan siler.

### A.4 `README.md` akış özeti (PR #1 içinde)

"Dokümantasyon haritası"ndan (`README.md:177`) önce, `README.md:145-168` "Format, build ve test" bölümünün ardından yeni `## Katkı akışı ve dal koruması` bölümü (3 madde, kısa):

- `main` korumalıdır: doğrudan push yok; her değişiklik `<tür>/<kısa-konu>` dalından, Conventional Commits mesajlı PR ile gelir (`CLAUDE.md` › Git akışı'na bağlantı).
- Birleştirmek için `dotnet` ve `repo-guards` CI kontrolleri yeşil olmalı; korumalı yollar (`.github/`, `.claude/`, `scripts/`, `docs/product-spec.md`, `docs/adr/`, `*.csproj`, `Program.cs` ve CODEOWNERS'taki diğerleri) code owner incelemesi ister (`.github/CODEOWNERS`).
- Ajanlar PR açar; approve ve merge insandadır. Tek sahipli repoda sahip kendi PR'ında yönetici atlamasını yalnız PR sayfasından kullanır; doğrudan push herkese kapalıdır.

Aynı PR'da isteğe bağlı (Açık soru 7): #10 artıkları, `ISSUE-10` tasarımının "Onaylı değişiklikler" B-D bölümleri (rozet, README CI notu, `CLAUDE.md:44`, `.claude/skills/review-changes/SKILL.md:17`, `docs/adr/0009-…md:91`). `CLAUDE.md` ve `.claude/**` kısmı `/curate-knowledge` ile (knowledge-curator taslağı); ADR cümlesi insan işidir.

### A.5 Değişmeyenler

`policy.json`, csproj, `Program.cs`, `appsettings*.json`, `scripts/*.ps1`, `ci.yml`, `docs/product-spec.md` değişmez. Faz 0 durumu `Açık` kalır (diğer Faz 0 kalemleri sürüyor olabilir).

## Test planı

xUnit testi yoktur; iş depo yapılandırmasıdır ve kanıtı GitHub'ın kendi durumudur. Her ölçüt açık bir manuel kontrole bağlıdır.

| Kabul ölçütü | Test / kanıt | Senaryo(lar) |
| --- | --- | --- |
| 1 CODEOWNERS kapsamı | `gh api repos/otahacakirci/StockFlow/codeowners/errors` + test PR'ı | `errors` boş; geçici PR'da `StockFlow/StockFlow.csproj`, `StockFlow/Program.cs`, `docs/product-spec.md`, `scripts/validate-repository-hygiene.ps1`, `.claude/rules/security.md`, `.github/workflows/ci.yml` dosyalarına dokunan değişiklikte her biri için PR "Files changed" ekranında `@otahacakirci` code owner olarak görünür (6 yolun hepsi) |
| 2 Branch protection etkin | A.3 adım 1-2 çıktıları + test PR'ı | Ruleset `active`; test PR'ında `dotnet` ve `repo-guards` "Required" etiketli; code owner onayı olmadan "Merging is blocked" görünür; yöneticiye yalnız "Merge without waiting for requirements (bypass)" seçeneği çıkar |
| 3 Doğrudan push engelli | A.3 adım 3 | `git push origin HEAD:main` reddedilir; reddin metni issue yorumuna eklenir |
| 4 README akışı | README önizleme | Yeni bölüm görünür; yerel bağlantıların (`CLAUDE.md`, `.github/CODEOWNERS`) hedefi vardır (`editing-discipline.md` › Dokümantasyon); hijyen betiği temiz |

Done gate (`dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`) kod değişmediği için sonuçları değişmez ama CLAUDE.md gereği raporlanır; hijyen betiği commit öncesi çalışır. PR'ın kendi CI koşusu (`dotnet`, `repo-guards`) yeşil olmalıdır.

## İş sırası

1. Ana oturum: `chore/branch-protection-codeowners` dalında A.1 ve A.4'ü yaz; `scripts/validate-repository-hygiene.ps1`; PR (insan push'u onaylar).
2. İnsan: PR #1'i inceler ve `main`'e merge eder (koruma henüz yok; self-merge geçerlidir).
3. İnsan: A.2 ruleset'i oluşturur (önce `evaluate`, sonra `active`).
4. İnsan: A.3 kanıtlarını toplar ve issue'ya ekler; test PR'ını kapatır.
5. Paralel iş yok; Data/Service/Web/Tests paketleri yok. Kabul ölçütü 2-3 yalnız 1. ve 4. adımlardan sonra kapanır; issue kapatma insan kararıdır.

## Açık sorular

1. Tek sahip kilitlenmesi (yanıtlayacak: proje sahibi). `@otahacakirci` hem tek code owner hem tüm PR'ların yazarı; GitHub kendi PR'ını onaylatmaz. Varsayım: ruleset'te yönetici rolüne yalnız "PR üzerinden atlama" yetkisi (A.2). Sonuç: sahip kendi PR'larını her seferinde bilinçli "bypass" ile merge eder; kurallar korumalı yollarda yalnız bu bilinçli adımı zorunlu kılar, ikinci göz sağlamaz. Alternatifler: ikinci bir collaborator/code owner eklemek (gerçek ikinci göz; kişi ve hesap bilgisi gerekir); klasik protection + "yöneticiler için de uygula" (sahip merge edemez, işe yaramaz); klasik protection yönetici atlamasız (doğrudan push açık kalır, 3. ölçüt karşılanmaz).
2. Ek korumalar (yanıtlayacak: proje sahibi). Issue sadece PR + CI + code owner + doğrudan push yasağı ister. Varsayım: ek olarak `required_approving_review_count: 1`, `strict` (dal güncel), `dismiss_stale_reviews_on_push`, force-push ve silme yasağı. Gerek yoksa A.2 yükünden bu satırlar çıkarılır; `count: 0` seçilirse korumalı olmayan yollarda inceleme gerekmez ve sahip her kaynak PR'ında bypass kullanmak zorunda kalmaz (bu seçenekte code owner gereksinimin yine işlediği ruleset sonrası A.3 test PR'ıyla doğrulanmalı).
3. CODEOWNERS genişliği (yanıtlayacak: proje sahibi). Issue yalnız `*.csproj` ve `Program.cs`'yi sayar. Varsayım: `policy.json` `approvalPaths` ile hizalamak için `StockFlow.slnx`, `.config/dotnet-tools.json`, `appsettings*.json`, `launchSettings.json`, `.gitignore`, `.gitattributes` de eklenir. Dar tutmak için A.1'deki son altı satır silinir. `docs/roadmap.md` ve `docs/adr/` zaten sahipli; `docs/designs/`, `StockFlow/**` kaynak kodu sahipsiz kalır (bilinçli: kaynak kodu inceleyici PR sürecine bırakılır).
4. Zorunlu check adları (yanıtlayacak: proje sahibi, yalnız ad değişecekse). Varsayım: `dotnet`, `repo-guards`, `ci.yml:21`/`:86` iş kimlikleri; `integration_id` sabitlenmez. Workflow'a `name:` eklenirse veya iş yeniden adlandırılırsa ruleset aynı değişiklikte güncellenir, aksi halde merge kilitlenir.
5. CONTRIBUTING (yanıtlayacak: proje sahibi). Issue "CONTRIBUTING/README" der. Varsayım: ayrı CONTRIBUTING.md açılmaz; README'de kısa bölüm yeterlidir (tek kaynak, ek dosya bakımı yok).
6. ADR (yanıtlayacak: proje sahibi). Varsayım: yeni ADR yok; karar `ADR-0003` "son savunma hattı" beyanını gerçekleştirir ve roadmap yalnız test altyapısı için ADR ister. Bir ADR istenirse insan kararıdır ve `docs/adr/README.md` indeksi güncellenir.
7. #10 artıkları (yanıtlayacak: proje sahibi). `ISSUE-10` tasarımındaki README rozeti/notu, `CLAUDE.md:44`, `review-changes/SKILL.md:17` ve ADR-0009 doğrulama cümlesi hâlâ bekliyor. Varsayım: README parçası (rozet + CI notu) bu işin README değişikliğiyle aynı PR'a girer (aynı dosya, çakışmayı önler); `CLAUDE.md` ve skill güncellemesi ayrı `/curate-knowledge` PR'ı, ADR-0009 cümlesi insan eliyle kalır. Bu işin kabul ölçütleri bunlara bağlı değildir.
8. Fork/dış katkı (yanıtlayacak: proje sahibi, bilgi). Repo herkese açıktır; ruleset hedefi `main` olduğu için fork PR'ları da aynı zorunlu kontrollere tabidir. Ek ayar (Actions fork onayı) kapsam dışıdır.

## Manuel doğrulama

1. Depo kökünde `git branch --show-current` ile dalı doğrula; A.1 ve A.4 sonrası `gh api repos/otahacakirci/StockFlow/codeowners/errors` (merge sonrası) ve `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-repository-hygiene.ps1`.
2. PR'ın Actions sayfasında `dotnet` ve `repo-guards` yeşil olduğunu, PR'ın "Files changed" ekranında `.github/CODEOWNERS`, `README.md` için code owner (`.github/` sahipli) istendiğini gör; insan PR'ı `main`'e merge eder.
3. A.2 komutuyla ruleset'i oluştur; `gh api repos/otahacakirci/StockFlow/rules/branches/main` ile dört kuralı ve iki check adını gör.
4. Geçici dalda `StockFlow/StockFlow.csproj`'a yorum satırı ekleyip PR aç: code owner isteğini ve zorunlu kontrolleri gör, "merge engellendi" durumunu not et; PR'ı kapat.
5. İnsan, A.3 adım 3'teki doğrudan push'u dener, reddi kaydeder; üç çıktıyı (kurallar, ruleset, reddedilen push) issue'ya yorum olarak ekler.
6. Sahibin kendi PR'ını "bypass" ile merge edebildiğini ve bunun PR sayfasında görünür olduğunu bir sonraki kendi PR'ında teyit et.
