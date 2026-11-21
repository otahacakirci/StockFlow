# ISSUE-10: GitHub Actions CI workflow'u (build, test, format, guard testleri, repo hijyeni)

- Kaynak: https://github.com/otahacakirci/StockFlow/issues/10 (yorum yok; etiketler `faz-0`, `area:devops`, `area:testing`)
- Tarih: 2026-11-21
- Paketler: Data ☐ · Service ☐ · Web ☐ · Tests ☐ · Onaylı ☑

## İstenen

Her push ve pull request'te Ubuntu runner üzerinde şu kontroller çalışır: `dotnet restore` + `dotnet tool restore`, uyarıları hata sayan `dotnet build StockFlow.slnx --no-restore`, `dotnet test` (Testcontainers MSSQL dahil), `dotnet format StockFlow.slnx --verify-no-changes --no-restore`, `node .claude/hooks/agentic/test-guard.mjs` ve repo hijyeni betiği. NuGet önbelleği ve test sonucu raporu eklenir; her kontrol ayrı adım olarak görünür, başarısız adım PR'ı kırmızı yapar. README'ye CI rozeti girer. `policy.json` `approvalPaths` listesinin workflow yollarını kapsaması istenir.

Yararlanan: proje sahibi ve ajanlar; teslim akışındaki done gate (build + test + format + hijyen) insan makinesine bağlı kalmadan her PR'da bağımsız doğrulanır. Kapsam: `docs/product-spec.md:134` (Tablo 1a, GitHub Actions, Faz 0) ve `:587` (Faz 0, durum `Açık`) bu işi açıkça kapsar. Spec/FR/AC değişikliği gerekmez; kapsam değişikliği bulgusu yoktur. Bağımlılıklar #7, #8, #9 kapalıdır ve `origin/main` içindedir (merge'ler #87, #88, #89); çalışma dalı `chore/github-actions-ci` `origin/main` ile aynıdır (`git log origin/main..HEAD` boş).

## Bilinmesi gereken mevcut mantık

- `.github/` altında yalnız `.github/CODEOWNERS` vardır (`/.github/` `@otahacakirci` sahipliğinde). `.github/workflows/` yok: Yeni alan.
- `.claude/hooks/agentic/policy.json:35` `approvalPaths` içinde `.github/workflows/**` zaten vardır; `test-vectors.json:94-97` motor fikstüründe aynı girdi ve `:1261-1277` workflow yolunun onay istediği bir vektör bulunur. Altıncı kabul ölçütü bugün zaten karşılanır; değişiklik gerekmez, yalnız doğrulanır. Sonuç: subagent'lar `.github/workflows/**` dosyasına yazamaz (`guard.mjs:106-108`), bu yüzden bütün iş "Onaylı" pakettedir.
- `scripts/validate-repository-hygiene.ps1`: Windows'ta `powershell` ile belgelenmiştir (`CLAUDE.md` › Komutlar). Staged dosya yoksa `git ls-files` ile bütün takipli dosyaları tarar (`:15-25`), takipli dosyaları `git show :<yol>` ile okur (`:88`), ihlalde `Write-Error` + `exit 1` verir (`:102-105`). CI'da staged içerik olmayacağından tüm takipli dosyalar taranır. Linux'ta yürütücü `pwsh`'dir (ubuntu runner'da ön kurulu); betik çapraz platformdur görünür, Linux'ta henüz koşulmamıştır (risk).
- `.claude/hooks/agentic/test-guard.mjs`: yalnız Node yerleşik modüllerini kullanır (`:7-12`), bağımlılık/`package.json` yoktur; `guard.mjs`'i süreç olarak çalıştırır (`:74`).
- Testler: `StockFlow.Tests/StockFlow.Tests.csproj` Testcontainers.MsSql 4.16.0 (`:15`) ve `coverlet.collector` kullanır. `StockFlow.Tests/Infrastructure/MsSqlTestServer.cs:8` imajı sabitler (`mcr.microsoft.com/mssql/server:2022-CU28-ubuntu-22.04`), konteyner ilk DB testinde açılır, Docker yoksa DB testleri ortam hatasıyla düşer (`:22-29`). Kodda atlanan test yoktur (`Skip` araması 0). ADR-0009 › Doğrulama (`docs/adr/0009-testcontainers-mssql-test-databases.md:91`) Ubuntu'da 311 test, 0 başarısız, 0 atlanan kanıtlar ve yerel Docker Engine koşusunun "Faz 0 CI'ı (#10) ile kalıcı olarak doğrulanacağını" söyler: bu iş o doğrulamanın kapanışıdır.
- Build: `StockFlow/StockFlow.csproj` ve test csproj'unda `TreatWarningsAsErrors` yoktur; `CLAUDE.md` › Bilinen ortam gerçekleri build'in 0 hata 0 uyarı verdiğini söyler. `global.json` yoktur. `.config/dotnet-tools.json` yalnız `dotnet-ef` 10.0.11 (`rollForward: false`) içerir.
- Biçim: `.editorconfig` yoktur (`CLAUDE.md`); `.gitattributes` `* text=auto`. İndekste dosyalar LF'dir (`git ls-files --eol`: `i/lf`, çalışma ağacı Windows'ta CRLF), yani Linux checkout LF olur. `dotnet format` Linux'ta ilk kez koşacaktır (risk).
- Dokümanda eskiyen ifadeler: `CLAUDE.md:44` ("CI henüz yok (#10)"), `.claude/skills/review-changes/SKILL.md:17` ("repoda CI yok; checks boş dönebilir"), `docs/adr/0009-testcontainers-mssql-test-databases.md:91`.
- Repo herkese açıktır (`gh api repos/otahacakirci/StockFlow --jq .private` → `false`): standart runner, fork PR'larında salt-okunur token; workflow secret kullanmaz.

## Geçerli kurallar

- `.claude/rules/editing-discipline.md` › Dokümantasyon: "Build/test/run komutu" satırı (README, `CLAUDE.md` › Komutlar, `policy.json`); "Git ignore, staged içerik veya secret politikası" satırı.
- `.claude/rules/editing-discipline.md` › Dosyalar: yeni paket/sürüm önerisinde 14 günden yeni sürüm kullanma; bu iş paket eklemez, yalnız Action sürümleri seçer (aşağıda yayın tarihleri).
- `.claude/rules/security.md` › Secret ve yapılandırma ve › Commit öncesi: workflow'a secret, connection string veya parola yazma; belgelerde `<...>` yer tutucusu. Testcontainers parolası koşu başına üretilir (`MsSqlTestServer.cs:71-75`), CI'a taşınmaz.
- `.claude/rules/testing.md` › Veritabanı ve › Çalıştırma ve raporlama: Docker yoksa "geçti" sayma; xUnit paralelliğini kapatma; InMemory/SQLite/LocalDB'ye dönüş yok. CI bu kurallara uyar: ilişkisel testler runner'ın Docker Engine'inde koşar, atlanan test kabul edilmez.
- `CLAUDE.md` › Ajan sistemi: `.github/workflows/**`, `README`, `scripts/*.ps1` insan onayı ister; subagent'lar yazamaz, bağımlılık olarak raporlar. `CLAUDE.md` ve `.claude/rules/**`, `.claude/skills/**` güncellemesi `/curate-knowledge` ile yapılır.
- `CLAUDE.md` › Değişmez proje sınırları: Faz 0 durumu `Açık` (`docs/product-spec.md:587`); GitHub Actions Faz 0 kapılı bileşenidir ve bu iş onu açıkça kapsar. Docker/compose, Serilog, health check (Faz 6) eklenmez.
- Kapsam dışı (kalıcı): üçüncü taraf test raporlayıcı Action'ları ve deploy/publish adımları eklenmez (aşağıda Açık sorular 3-4).

## Bölüm

| # | Gereksinim / kabul ölçütü | Paket (sahip rol) | Neden orada |
| --- | --- | --- | --- |
| 1 | Workflow push ve PR'da tetikleniyor | Onaylı (ana oturum + insan): `.github/workflows/ci.yml` (`on`, `concurrency`, `permissions`) | `.github/workflows/**` `approvalPaths` içinde; subagent yazamaz |
| 2 | Build 0 uyarı ile geçiyor (uyarı = hata) | Onaylı: `ci.yml` build adımı `-p:TreatWarningsAsErrors=true` (csproj değişmez) | csproj `approvalPaths` altında; CLI bayrağı "eşdeğeri" kabul edilir (Açık soru 2) |
| 3 | Testcontainers tabanlı testler runner'da yeşil | Onaylı: `ci.yml` test adımı (Docker Engine ön kurulu, ek ayar yok) | Test kodu değişmez; ADR-0009 kanıtı CI ile kapanır |
| 4 | Format, guard testleri ve hijyen ayrı adım olarak raporlanıyor | Onaylı: `ci.yml` adım/iş düzeni, `if: ${{ !cancelled() }}` | Biri kırılınca diğerleri gizlenmesin |
| 5 | Başarısız adım PR'ı kırmızı yapıyor; README'de CI rozeti | Onaylı: `ci.yml` (hiçbir adımda `continue-on-error` yok); `README.md` rozet satırı | README insan onaylı; "required check" ayarı GitHub'da insan işidir (Açık soru 6) |
| 6 | `policy.json` `approvalPaths` workflow yollarını kapsıyor | Onaylı: değişiklik yok, doğrulama (`policy.json:35`, `test-vectors.json:94-97`, `:1261-1277`) | Zaten karşılanıyor; ihtiyaç duyulursa `test-guard.mjs` smoke kontrolü insan eliyle eklenir (Açık soru 9) |
| 7 | NuGet önbelleği ve test sonucu raporu (açıklama metni) | Onaylı: `ci.yml` (`setup-dotnet` cache, `.trx` artifact + job summary) | Yalnız birinci taraf Action'lar; csproj/paket eklenmez |
| 8 | Eskiyen belge ifadeleri ve README kullanım notu | Onaylı: `README.md`; `CLAUDE.md:44`, `.claude/skills/review-changes/SKILL.md:17`, `.claude/rules/testing.md` için `knowledge-curator` (`/curate-knowledge`) taslağı; `docs/adr/0009-…md:91` insan | Kütüphane ve ADR yolları ajan yazamayacağı yerlerdir |

Data, Service, Web ve Tests paketlerinde iş yoktur; bu sürümde hiçbir `StockFlow/**` veya `StockFlow.Tests/**` dosyası değişmez.

## Data işi

Yok.

## Service işi

Yok.

## Web işi

Yok.

## Onaylı değişiklikler

Hepsi ana oturumda, insan onayıyla yapılır; implementer rolü yoktur.

### A. `.github/workflows/ci.yml` (yeni)

Aşağıdaki iskelet bağlayıcıdır; yalnız `<...>` yer tutucuları uygulayıcı tarafından doldurulur.

```yaml
name: CI

on:
  push:
    branches: ['**']
  pull_request:
    branches: [main]

permissions:
  contents: read

concurrency:
  group: ci-${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: ${{ github.ref != 'refs/heads/main' }}

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

jobs:
  dotnet:
    name: Build, test, format
    runs-on: ubuntu-latest
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@<sha> # <tag>
        with:
          persist-credentials: false

      - uses: actions/setup-dotnet@<sha> # <tag>
        with:
          dotnet-version: 10.0.x
          cache: true
          cache-dependency-path: |
            **/*.csproj
            .config/dotnet-tools.json

      - name: Restore
        id: restore
        run: dotnet restore StockFlow.slnx

      - name: Tool restore
        run: dotnet tool restore

      - name: Build (uyarı = hata)
        run: dotnet build StockFlow.slnx --no-restore -p:TreatWarningsAsErrors=true

      - name: Docker ortamı
        run: docker version

      - name: Test
        run: >
          dotnet test StockFlow.slnx --no-build --no-restore
          --logger "trx;LogFileName=test-results.trx"
          --results-directory TestResults

      - name: Test özeti
        if: ${{ !cancelled() }}
        shell: bash
        run: |
          {
            echo '### Test sonucu'
            if ls TestResults/*.trx >/dev/null 2>&1; then
              echo '```'
              grep -ho '<Counters [^>]*>' TestResults/*.trx
              echo '```'
            else
              echo 'Test sonuç dosyası üretilmedi.'
            fi
          } >> "$GITHUB_STEP_SUMMARY"

      - name: Test sonuçlarını yükle
        if: ${{ !cancelled() }}
        uses: actions/upload-artifact@<sha> # <tag>
        with:
          name: test-results
          path: TestResults/*.trx
          if-no-files-found: ignore
          retention-days: 14

      - name: Format
        if: ${{ !cancelled() && steps.restore.outcome == 'success' }}
        run: dotnet format StockFlow.slnx --verify-no-changes --no-restore

  repo-guards:
    name: Guard testleri ve repo hijyeni
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - uses: actions/checkout@<sha> # <tag>
        with:
          persist-credentials: false

      - uses: actions/setup-node@<sha> # <tag>
        with:
          node-version: <yerelde test-guard'ı geçiren Node majörü>

      - name: Guard testleri
        run: node .claude/hooks/agentic/test-guard.mjs

      - name: Repo hijyeni
        if: ${{ !cancelled() }}
        run: pwsh -NoProfile -File scripts/validate-repository-hygiene.ps1
```

Bağlayıcı ayrıntılar:

- Adım adları Türkçe, iş adları sabit: `dotnet` ve `repo-guards` (branch protection'da "required check" adı olur; sonradan değiştirmek ayarı bozar).
- Hiçbir adımda `continue-on-error: true` yok; hiçbir adımda `|| true` yok.
- `dotnet test ... --no-build` kullanılır; derleme yalnız uyarı=hata build adımında olur. Yerel done gate (`dotnet test StockFlow.slnx --no-restore`) değişmez.
- `-p:TreatWarningsAsErrors=true` NuGet uyarılarını (NU1902/NU1903) da hata yapar: restore uyarıları `project.assets.json`'dan build'de yeniden yayınlanır. Uygulayıcı bunu yerelde doğrular (aşağıda manuel doğrulama 1).
- `permissions: contents: read`; secret kullanılmaz, `pull_request_target` kullanılmaz.
- Docker için ek ayar (`TESTCONTAINERS_HOST_OVERRIDE`, imaj pre-pull) yok. Imaj etiketi iki yerde tekrarlanıp sürüklenmesin diye pre-pull eklenmez (etiketin tek kaynağı `MsSqlTestServer.cs:8`). İlk koşu imajı indirir ve yavaştır (ADR-0009 › Sonuçlar); bu yüzden `timeout-minutes: 30`.
- Üçüncü taraf Action yok: yalnız `actions/checkout`, `actions/setup-dotnet`, `actions/setup-node`, `actions/upload-artifact`.
- Action sürümleri: tasarım anında `gh api repos/actions/<ad>/releases/latest` ile görülen son sürümler: `actions/setup-dotnet` v6.1.0 (2026-11-03), `actions/checkout` v7.0.1 (2026-07-20), `actions/upload-artifact` v7.0.2 (2026-10-07); hepsi 14 günden eskidir. `actions/setup-node` sürümü ve hepsinin tam commit SHA'sı uygulama sırasında doğrulanır; iş akışında SHA + yorumda etiket yazılır (Açık soru 3). Seçilen sürüm 14 günden yeniyse bir önceki kararlı sürüm seçilir ve yayın tarihi insana gösterilir.

### B. `README.md` (insan onayı)

- Başlığın altına rozet: `[![CI](https://github.com/otahacakirci/StockFlow/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/otahacakirci/StockFlow/actions/workflows/ci.yml)`.
- "Format, build ve test" bölümüne kısa not: CI aynı kontrolleri Ubuntu'da koşar; farklar: build `-p:TreatWarningsAsErrors=true` ile koşar, hijyen betiği Linux'ta `pwsh -NoProfile -File scripts/validate-repository-hygiene.ps1` ile çalışır, ilk koşu SQL Server imajını indirir.

### C. Kütüphane güncellemesi (`/curate-knowledge`, taslak)

- `CLAUDE.md:44`: "CI henüz yok (#10)" cümlesi CI'ın varlığını ve `.github/workflows/ci.yml` yolunu söyleyecek şekilde güncellenir; `CLAUDE.md` › Komutlar tablosuna CI'ın hangi komutları koştuğu (ve Linux hijyen komutunun `pwsh` olduğu) eklenebilir.
- `.claude/skills/review-changes/SKILL.md:17`: "(repoda CI yok; checks boş dönebilir)" ifadesi `gh pr checks` çıktısının artık `dotnet` ve `repo-guards` işlerini içerdiğini söyler; reviewer CI sonucunu raporlar.
- `.claude/rules/testing.md` › Çalıştırma ve raporlama: CI'da DB testlerinin runner Docker'ında koştuğu, ortam hatasının CI'da kırmızı olduğu tek cümleyle eklenir.

### D. ADR-0009 doğrulama notu (insan; ilk yeşil koşudan sonra)

`docs/adr/0009-testcontainers-mssql-test-databases.md:91` "kalıcı olarak doğrulanacaktır" cümlesine ilk yeşil CI koşusunun (run adresi, test sayısı, 0 başarısız, 0 atlanan) bağlantısı eklenir. Karar bölümüne dokunulmaz.

### E. Ayarlar (GitHub, insan)

Branch protection (`main`: PR zorunlu, `dotnet` ve `repo-guards` zorunlu kontrol, "Require review from Code Owners") bu işin kabul ölçütlerinde yok, Faz 0 çıkış kanıtında var (`docs/product-spec.md:587`). Workflow'un ilk başarılı koşusundan sonra yapılır (Açık soru 6).

### F. Değişmeyenler

`policy.json`, `*.csproj`, `Program.cs`, `appsettings*.json`, `scripts/*.ps1`, `docs/product-spec.md` değişmez. Faz 0 durumu `Açık` kalır (branch protection ve diğer Faz 0 kalemleri sürüyor olabilir; spec güncellemesi bu işin kapsamı değildir).

## Test planı

xUnit testi eklenmez; bu iş CI yapılandırmasıdır ve kanıtı CI'ın kendi koşusudur. Her ölçüt açık bir manuel kontrole bağlıdır. Push insan onayı ister; "kırmızı kanıtı" geçici dalda alınır ve dal işten sonra silinmez, insana bırakılır (açık onay olmadan silme yok).

| Kabul ölçütü | Test / kanıt | Senaryo(lar) |
| --- | --- | --- |
| 1 Tetikleme | İlk push ve PR açılışı | `chore/github-actions-ci` push'unda `push` olayıyla bir koşu; PR açılınca `pull_request` olayıyla ikinci koşu görünür; aynı ref'e art arda push önceki koşuyu iptal eder (`main` hariç) |
| 2 Build 0 uyarı | Yerel + CI | Yerelde `dotnet build StockFlow.slnx --no-restore -p:TreatWarningsAsErrors=true` 0 hata/0 uyarı (yeşil kanıtı); geçici dalda bilerek eklenen bir derleyici uyarısı build adımını kırmızı yapar (kırmızı kanıtı), uyarı geri alınınca yeşil |
| 3 Testcontainers yeşil | CI test adımı | `Test` adımı yeşil; job summary sayaçları `failed="0"` ve `notExecuted="0"`; `total` yerel Docker koşusuyla aynı (ADR-0009'daki 311 başlangıç referansıdır; güncel sayıyı yerelde `dotnet test StockFlow.slnx --no-restore` ile al); `docker version` adımı yeşil |
| 4 Ayrı adımlar | Actions arayüzü | `Restore`, `Tool restore`, `Build (uyarı = hata)`, `Test`, `Format`, `Guard testleri`, `Repo hijyeni` adları ayrı satırlar olarak görünür; geçici dalda sırasıyla bir format ihlali (fazladan boşluk), bir guard ihlali (yalnız geçici dalda `policy.json`'u geçersiz JSON yapmak; `test-guard.mjs` `JSON.parse` ile düşer. Listeyi boşaltmak ihlal sayılmaz, çünkü denetimler listeyi gezer) ve takipli bir `.log` dosyası (hijyen ihlali, `git add -f`) yalnızca ilgili adımı kırmızı yapar, diğer adımlar yine koşar |
| 5 PR kırmızı, rozet | PR sayfası + README | Kırmızı adım PR'da başarısız check olarak görünür; rozet `passing`/`failing` durumunu gösterir ve bağlantı Actions sayfasını açar; zorunlu kontrol ayarı manuel (Açık soru 6) |
| 6 `approvalPaths` | Yerel komut | `node .claude/hooks/agentic/test-guard.mjs` yeşil; `policy.json:35` ve `test-vectors.json:1261-1277` okunur; ek değişiklik yok |
| 7 Önbellek ve rapor | Actions arayüzü | İkinci koşuda `setup-dotnet` önbellek isabeti (`Cache restored`) loglanır; `test-results` artifact'i indirilebilir; job summary'de sayaçlar görünür |

Statik doğrulama: `actionlint` repoda yoktur ve eklenmez; YAML sözdizimini ilk koşu doğrular. Yerelde `pwsh`/`node`/Docker ile adımların karşılığı komut komut çalıştırılır (Docker Desktop, Linux konteyner modu). Yerel koşu kanıtı CI kanıtının yerine geçmez.

## İş sırası

1. Ana oturum: `chore/github-actions-ci` dalında (`git branch --show-current` ile doğrula; `origin/main` ile aynıdır) `.github/workflows/ci.yml` yaz; yerelde her adımın komutunu çalıştır (özellikle `-p:TreatWarningsAsErrors=true` build ve `test-guard.mjs`); Action sürüm/SHA'larını doğrula.
2. İnsan onayıyla push; PR aç; ilk koşuyu izle. Linux'a özgü riskler (hijyen betiği `pwsh`, `dotnet format` LF, Docker) burada ortaya çıkabilir; düzeltmeler aynı PR'da yeni commit olur.
3. Kırmızı kanıtları (Test planı 2 ve 4) geçici dallarda al.
4. Yeşil koşudan sonra: README rozeti ve notu, `/curate-knowledge` taslağı (CLAUDE.md, review-changes skill, testing rule), ADR-0009 doğrulama notu aynı PR'da; branch protection ayarı insanda.
5. Paralel iş yok; Data/Service/Web paketleri yok. Done gate (`build`, `test`, `format`, hijyen) değişiklik kümesi için de çalıştırılır; hijyen `.yml` dosyasını tarar (`.yml`, `.yaml` uzantıları `:63-66` listesinde) — workflow'da secret, parola veya connection string deseni olmamalıdır.

## Açık sorular

1. Tetikleyici ve tekrar: Issue "her push/PR'da" der; `push` (tüm dallar) + `pull_request` (hedef `main`) aynı commit için iki koşu üretir. Varsayım: ikisi de açık, `concurrency` ile aynı ref'te eski koşu iptal edilir (`main` hariç). Yanıtlayacak: proje sahibi. Alternatif: `push` yalnız `main`.
2. "Uyarı = hata" nasıl: Varsayım: CI'a özgü `-p:TreatWarningsAsErrors=true` (csproj değişmez, yerel geliştirme etkilenmez). Alternatif: her iki csproj'a `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (yerel = CI, ama csproj insan onayı ister ve IDE uyarılarını yerelde hataya çevirir). Yanıtlayacak: proje sahibi.
3. Action sürüm sabitleme: Varsayım: tam commit SHA + yorumda etiket, yalnız birinci taraf `actions/*`. Alternatif: `@v6`/`@v7` hareketli majör etiketler. Sürümler 14 gün kuralına uymalı (`setup-node` sürümü ve SHA'lar uygulamada doğrulanır). Yanıtlayacak: proje sahibi.
4. Test raporu biçimi: Varsayım: `.trx` artifact + job summary (sayaçlar). Üçüncü taraf raporlayıcı Action'lar ve `GitHubActionsTestLogger` paketi (csproj + paket onayı) alınmaz. Yanıtlayacak: proje sahibi.
5. İş düzeni: Varsayım: iki iş (`dotnet`, `repo-guards`); tek iş veya dört iş de mümkün, fark yalnız zorunlu kontrol adlarıdır. Yanıtlayacak: proje sahibi (branch protection ayarlayacak kişi).
6. Branch protection: Issue kabul ölçütlerinde yok; `docs/roadmap.md:154` ve Faz 0 çıkış kanıtında var. Varsayım: bu işin PR'ı kapsamında değil, ilk yeşil koşudan sonra insan GitHub ayarlarında yapar (kontrol adları `dotnet`, `repo-guards`). Yanıtlayacak: proje sahibi.
7. .NET SDK sürümü: `global.json` yok. Varsayım: `dotnet-version: 10.0.x` (en son 10.0 yaması); `global.json` eklenmez. Yanıtlayacak: proje sahibi.
8. `dotnet format` Linux'ta: `.editorconfig` yok, indeks LF. Varsayım: ek ayar gerekmeden temiz çıkar. Çıkmazsa çözüm `.editorconfig`/`.gitattributes` kararı olur (bugünkü "`.editorconfig` yok" kuralını değiştirir) ve ayrı onay ister; uygulayıcı bu durumda durup raporlar. Yanıtlayacak: proje sahibi (yalnız sorun çıkarsa).
9. Guard smoke kontrolü: `approvalPaths`'in workflow yolunu kapsadığını `test-guard.mjs` proje bölümünde de denetlemek (bugün yalnız motor vektörü var) kabul ölçütünü kalıcı korur, ama `.claude/hooks/**` guardrail yoludur ve yalnız insan değiştirebilir. Varsayım: eklenmez, ölçüt mevcut durumla karşılanmış sayılır. Yanıtlayacak: proje sahibi.
10. Yeni ADR: Roadmap yalnız test altyapısı için ADR ister (ADR-0009 var). Varsayım: CI için ayrı ADR yazılmaz; kalıcı bilgi `.claude/rules/testing.md` ve `CLAUDE.md` güncellemesine gider. Yanıtlayacak: proje sahibi.

## Manuel doğrulama

1. Yerelde, depo kökünde: `dotnet restore StockFlow.slnx` ve `dotnet tool restore`, ardından `dotnet build StockFlow.slnx --no-restore -p:TreatWarningsAsErrors=true` (0 hata, 0 uyarı); `dotnet test StockFlow.slnx --no-build --no-restore` (Docker Desktop açık, Linux konteyner modu); `dotnet format StockFlow.slnx --verify-no-changes --no-restore`; `node .claude/hooks/agentic/test-guard.mjs`; hijyen betiği.
2. İnsan onayıyla `chore/github-actions-ci` dalını push et, PR aç: GitHub › Actions sayfasında `CI` iş akışının `dotnet` ve `repo-guards` işlerini ve ayrı adımları gör; ilk koşuda SQL Server imajı indirildiği için `Test` adımının uzun sürdüğünü not et.
3. İkinci commit'te `setup-dotnet` önbellek isabetini, `test-results` artifact'ini ve job summary sayaçlarını kontrol et.
4. Geçici dalda sırasıyla bir uyarı, bir format ihlali, bir guard ihlali ve takipli `.log` dosyası ekleyip ilgili adımın tek başına kırmızı olduğunu ve PR'ın başarısız check gösterdiğini doğrula; geçici dalı insan siler.
5. README'deki rozetin `main` dalı durumunu gösterdiğini ve bağlantısının Actions sayfasını açtığını doğrula.
6. Yeşil koşudan sonra GitHub › Settings › Branches altında `main` için `dotnet` ve `repo-guards` kontrollerini zorunlu yap, Code Owners incelemesini aç (insan).
