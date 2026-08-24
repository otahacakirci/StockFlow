[CmdletBinding()]
param(
    [string]$ManifestPath = "docs/ai/context-manifest.json",
    [string]$RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

$root = [System.IO.Path]::GetFullPath($RepositoryRoot)
$rootPrefix = $root.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$errors = New-Object System.Collections.Generic.List[string]

function Add-ValidationError {
    param([string]$Message)
    $errors.Add($Message)
}

function Resolve-RepositoryPath {
    param(
        [string]$Path,
        [string]$BaseDirectory = $root
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        Add-ValidationError "Boş dosya yolu bulundu."
        return $null
    }

    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) {
        [System.IO.Path]::GetFullPath($Path)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $BaseDirectory $Path))
    }

    if (($candidate -ne $root) -and -not $candidate.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        Add-ValidationError "Depo kökü dışına çıkan yol reddedildi: $Path"
        return $null
    }

    return $candidate
}

function Get-FrontMatter {
    param(
        [string]$Content,
        [string]$DisplayPath
    )

    $match = [regex]::Match($Content, "\A---\s*\r?\n(?<body>.*?)\r?\n---\s*(?:\r?\n|\z)", [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $match.Success) {
        Add-ValidationError "Markdown üst verisi bulunamadı: $DisplayPath"
        return $null
    }

    return $match.Groups["body"].Value
}

function Get-FrontMatterValue {
    param(
        [string]$FrontMatter,
        [string]$Key
    )

    $pattern = '(?m)^' + [regex]::Escape($Key) + ':\s*["'']?(?<value>[^\r\n"'']*)'
    $match = [regex]::Match($FrontMatter, $pattern)
    if (-not $match.Success) {
        return $null
    }

    return $match.Groups["value"].Value.Trim()
}

$manifestFullPath = Resolve-RepositoryPath -Path $ManifestPath
if (($null -eq $manifestFullPath) -or -not (Test-Path -LiteralPath $manifestFullPath -PathType Leaf)) {
    Add-ValidationError "Manifest bulunamadı: $ManifestPath"
}

$manifest = $null
if (($null -ne $manifestFullPath) -and (Test-Path -LiteralPath $manifestFullPath -PathType Leaf)) {
    try {
        $manifest = Get-Content -LiteralPath $manifestFullPath -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        Add-ValidationError "Manifest geçerli JSON değil: $($_.Exception.Message)"
    }
}

if ($null -ne $manifest) {
    if ($manifest.schemaVersion -ne 1) {
        Add-ValidationError "Desteklenmeyen schemaVersion: $($manifest.schemaVersion). Beklenen: 1"
    }

    foreach ($property in @("entrypoint", "alwaysRead", "documents", "validationCommands")) {
        if ($null -eq $manifest.PSObject.Properties[$property]) {
            Add-ValidationError "Manifest zorunlu alanı eksik: $property"
        }
    }

    if ($null -ne $manifest.PSObject.Properties["entrypoint"]) {
        $entrypointFullPath = Resolve-RepositoryPath -Path ([string]$manifest.entrypoint)
        if (($null -eq $entrypointFullPath) -or -not (Test-Path -LiteralPath $entrypointFullPath -PathType Leaf)) {
            Add-ValidationError "Manifest entrypoint dosyası bulunamadı: $($manifest.entrypoint)"
        }
    }

    $documents = @($manifest.documents)
    if ($documents.Count -eq 0) {
        Add-ValidationError "Manifest documents listesi boş."
    }

    $seenPaths = @{}
    foreach ($document in $documents) {
        foreach ($property in @("path", "authority", "loadWhen", "reviewTriggers")) {
            if ($null -eq $document.PSObject.Properties[$property]) {
                Add-ValidationError "Manifest document kaydında zorunlu alan eksik: $property"
            }
        }

        $documentPath = [string]$document.path
        if ([string]::IsNullOrWhiteSpace($documentPath)) {
            Add-ValidationError "Manifest document kaydında boş path bulundu."
            continue
        }

        $pathKey = $documentPath.Replace("\", "/").ToLowerInvariant()
        if ($seenPaths.ContainsKey($pathKey)) {
            Add-ValidationError "Manifestte yinelenen document yolu: $documentPath"
        }
        else {
            $seenPaths[$pathKey] = $true
        }

        if ([string]::IsNullOrWhiteSpace([string]$document.authority)) {
            Add-ValidationError "Document authority boş: $documentPath"
        }
        if (@($document.loadWhen).Count -eq 0) {
            Add-ValidationError "Document loadWhen listesi boş: $documentPath"
        }
        if (@($document.reviewTriggers).Count -eq 0) {
            Add-ValidationError "Document reviewTriggers listesi boş: $documentPath"
        }

        $documentFullPath = Resolve-RepositoryPath -Path $documentPath
        if (($null -eq $documentFullPath) -or -not (Test-Path -LiteralPath $documentFullPath -PathType Leaf)) {
            Add-ValidationError "Manifestte listelenen belge bulunamadı: $documentPath"
            continue
        }

        $content = Get-Content -LiteralPath $documentFullPath -Raw -Encoding UTF8
        $frontMatter = Get-FrontMatter -Content $content -DisplayPath $documentPath
        if ($null -eq $frontMatter) {
            continue
        }

        foreach ($key in @("title", "status", "authority", "last_reviewed", "review_triggers")) {
            if (-not [regex]::IsMatch($frontMatter, "(?m)^" + [regex]::Escape($key) + ":")) {
                Add-ValidationError "Üst veri alanı eksik ($key): $documentPath"
            }
        }

        if (-not [regex]::IsMatch($frontMatter, "(?ms)^review_triggers:\s*\r?\n(?:\s+-\s+\S+\s*(?:\r?\n|\z))+")) {
            Add-ValidationError "Üst veri review_triggers listesi boş veya geçersiz: $documentPath"
        }

        $frontMatterAuthority = Get-FrontMatterValue -FrontMatter $frontMatter -Key "authority"
        if ($frontMatterAuthority -ne [string]$document.authority) {
            Add-ValidationError "Authority uyuşmazlığı: $documentPath (manifest=$($document.authority), belge=$frontMatterAuthority)"
        }

        $lastReviewed = Get-FrontMatterValue -FrontMatter $frontMatter -Key "last_reviewed"
        if (($lastReviewed -ne "YYYY-MM-DD") -and ($lastReviewed -notmatch "^\d{4}-\d{2}-\d{2}$")) {
            Add-ValidationError "last_reviewed ISO tarih değil: $documentPath"
        }

        if ($documentPath -match "^docs/adr/\d{4}-.+\.md$") {
            $adrStatus = Get-FrontMatterValue -FrontMatter $frontMatter -Key "status"
            if ($adrStatus -notin @("Proposed", "Accepted", "Superseded")) {
                Add-ValidationError "Geçersiz ADR durumu: $documentPath ($adrStatus)"
            }
        }
    }

    foreach ($alwaysReadPath in @($manifest.alwaysRead)) {
        $normalized = ([string]$alwaysReadPath).Replace("\", "/").ToLowerInvariant()
        if (-not $seenPaths.ContainsKey($normalized)) {
            Add-ValidationError "alwaysRead yolu documents listesinde yok: $alwaysReadPath"
        }

        $alwaysReadFullPath = Resolve-RepositoryPath -Path ([string]$alwaysReadPath)
        if (($null -eq $alwaysReadFullPath) -or -not (Test-Path -LiteralPath $alwaysReadFullPath -PathType Leaf)) {
            Add-ValidationError "alwaysRead dosyası bulunamadı: $alwaysReadPath"
        }
    }

    if (@($manifest.validationCommands).Count -eq 0) {
        Add-ValidationError "Manifest validationCommands listesi boş."
    }
}

$agentsPath = Resolve-RepositoryPath -Path "AGENTS.md"
if (($null -eq $agentsPath) -or -not (Test-Path -LiteralPath $agentsPath -PathType Leaf)) {
    Add-ValidationError "Kök AGENTS.md bulunamadı."
}
else {
    $agentsInfo = Get-Item -LiteralPath $agentsPath
    if ($agentsInfo.Length -ge 32768) {
        Add-ValidationError "AGENTS.md 32 KiB sınırını aşıyor: $($agentsInfo.Length) byte"
    }

    $agentsContent = Get-Content -LiteralPath $agentsPath -Raw -Encoding UTF8
    foreach ($requiredReference in @("docs/ai/README.md", "docs/ai/current-state.md", "docs/ai/handoff.md", "docs/product-spec.md", "docs/ai/context-manifest.json")) {
        if (-not $agentsContent.Contains($requiredReference)) {
            Add-ValidationError "AGENTS.md zorunlu yönlendirmeyi içermiyor: $requiredReference"
        }
    }
}

$markdownFiles = @()
foreach ($relativeRoot in @("AGENTS.md", "README.md", "docs")) {
    $candidate = Resolve-RepositoryPath -Path $relativeRoot
    if (($null -eq $candidate) -or -not (Test-Path -LiteralPath $candidate)) {
        continue
    }

    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        $markdownFiles += Get-Item -LiteralPath $candidate
    }
    else {
        $markdownFiles += Get-ChildItem -LiteralPath $candidate -Recurse -File -Filter "*.md"
    }
}

$linkPattern = [regex]'!?(?:\[[^\]]*\])\((?<target>[^)]+)\)'
foreach ($markdownFile in $markdownFiles | Sort-Object FullName -Unique) {
    $content = Get-Content -LiteralPath $markdownFile.FullName -Raw -Encoding UTF8
    foreach ($match in $linkPattern.Matches($content)) {
        $target = $match.Groups["target"].Value.Trim()
        if ($target.StartsWith("<") -and $target.EndsWith(">")) {
            $target = $target.Substring(1, $target.Length - 2)
        }
        if ($target -match "^(?:https?://|mailto:)") {
            continue
        }

        $filePart = ($target -split "#", 2)[0]
        if ([string]::IsNullOrWhiteSpace($filePart)) {
            continue
        }

        try {
            $filePart = [System.Uri]::UnescapeDataString($filePart)
            $linkFullPath = Resolve-RepositoryPath -Path $filePart -BaseDirectory $markdownFile.DirectoryName
            if (($null -eq $linkFullPath) -or -not (Test-Path -LiteralPath $linkFullPath)) {
                $displaySource = $markdownFile.FullName.Substring($rootPrefix.Length).Replace("\", "/")
                Add-ValidationError "Bozuk yerel Markdown bağlantısı: $displaySource -> $target"
            }
        }
        catch {
            $displaySource = $markdownFile.FullName.Substring($rootPrefix.Length).Replace("\", "/")
            Add-ValidationError "Geçersiz yerel Markdown bağlantısı: $displaySource -> $target"
        }
    }
}

$specPath = Resolve-RepositoryPath -Path "docs/product-spec.md"
if (($null -eq $specPath) -or -not (Test-Path -LiteralPath $specPath -PathType Leaf)) {
    Add-ValidationError "Kanonik ürün spesifikasyonu bulunamadı."
}
else {
    $specContent = Get-Content -LiteralPath $specPath -Raw -Encoding UTF8
    foreach ($number in 1..10) {
        $id = "AC-{0:D2}" -f $number
        $count = [regex]::Matches($specContent, "(?m)^" + [regex]::Escape($id) + "\b").Count
        if ($count -ne 1) {
            Add-ValidationError "$id spesifikasyonda tam bir kez bulunmalı; bulunan: $count"
        }
    }

    $tableCount = [regex]::Matches($specContent, "(?m)^_Tablo\s+[1-9]\. ").Count
    if ($tableCount -ne 9) {
        Add-ValidationError "Spesifikasyonda dokuz numaralı tablo bekleniyor; bulunan: $tableCount"
    }

    $diagramCount = [regex]::Matches($specContent, '(?m)^```mermaid\s*$').Count
    if ($diagramCount -ne 4) {
        Add-ValidationError "Spesifikasyonda dört Mermaid diyagramı bekleniyor; bulunan: $diagramCount"
    }

    if ($specContent.Contains("[IMAGE ")) {
        Add-ValidationError "Spesifikasyonda dönüştürülmemiş görsel yer tutucusu bulundu."
    }
}

if ($errors.Count -gt 0) {
    Write-Host "Agent context validation FAILED ($($errors.Count) issue(s)):" -ForegroundColor Red
    foreach ($validationError in $errors) {
        Write-Host " - $validationError" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Agent context validation PASSED." -ForegroundColor Green
Write-Host " - Manifest schema: 1"
Write-Host " - Managed documents: $(@($manifest.documents).Count)"
Write-Host " - Markdown files checked: $(@($markdownFiles | Sort-Object FullName -Unique).Count)"
Write-Host " - Product spec: 9 tables, 4 Mermaid diagrams, AC-01..AC-10"
