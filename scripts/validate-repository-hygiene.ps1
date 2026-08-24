[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$gitCommand = Get-Command git -ErrorAction Stop

$resolvedGitRoot = (& $gitCommand.Source -C $repositoryRoot rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($resolvedGitRoot)) {
    throw 'Repository hygiene validation requires an initialized Git repository.'
}

$candidateFiles = @(& $gitCommand.Source -C $repositoryRoot diff --cached --name-only --diff-filter=ACMR)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to enumerate staged files.'
}

if ($candidateFiles.Count -eq 0) {
    $candidateFiles = @(& $gitCommand.Source -C $repositoryRoot ls-files)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to enumerate tracked files.'
    }
}

$candidateFiles = @($candidateFiles | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($candidateFiles.Count -eq 0) {
    throw 'No staged or tracked files were found to validate.'
}

$violations = [System.Collections.Generic.List[string]]::new()

$forbiddenPathRules = [ordered]@{
    'VisualStudioCache' = '(^|/)\.vs/'
    'BuildOutput' = '(^|/)(bin|obj)/'
    'UserSpecificFile' = '\.(user|suo|userosscache|sln\.docstates|lnk)$'
    'EnvironmentFile' = '(^|/)\.env($|\.)'
    'LocalAppSettings' = '(^|/)appsettings\..*\.local\.json$'
    'SecretStoreFile' = '(^|/)secrets\.json$'
    'CertificateOrPrivateKey' = '\.(pfx|p12|pem|key)$'
    'LocalDatabaseFile' = '\.(mdf|ldf|ndf)$'
    'GeneratedTestOrLogOutput' = '\.(trx|coverage|coveragexml|log)$'
}

foreach ($file in $candidateFiles) {
    $normalizedPath = $file.Replace('\', '/')

    foreach ($rule in $forbiddenPathRules.GetEnumerator()) {
        if ($normalizedPath -match $rule.Value) {
            if ($rule.Key -eq 'EnvironmentFile' -and $normalizedPath -match '(^|/)\.env\.example$') {
                continue
            }

            $violations.Add("[$($rule.Key)] $normalizedPath")
        }
    }
}

$textExtensions = @(
    '.cs', '.cshtml', '.csproj', '.css', '.config', '.json', '.js', '.md',
    '.props', '.ps1', '.slnx', '.targets', '.txt', '.xml', '.yaml', '.yml'
)

$sensitiveContentRules = [ordered]@{
    'PrivateKeyHeader' = '-----BEGIN (RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----'
    'PopulatedConnectionStringJson' = '"(ConnectionStrings|DefaultConnection)"\s*:\s*"(?!\s*(<|__|\$\{))[^"\r\n]+"'
    'PopulatedJwtKeyJson' = '"Key"\s*:\s*"(?!\s*(<|__|\$\{))[^"\r\n]{16,}"'
    'HardCodedCredentialAssignment' = '(?i)\b(Password|Pwd|ApiKey|Secret|Token)\s*=\s*"(?!\s*(<|__|\$\{))[^"\r\n]+"'
    'ConnectionStringSignature' = '(?i)(Server|Data Source)\s*=\s*[^;<\r\n]+;\s*(Database|Initial Catalog)\s*='
    'KnownTokenPrefix' = '(?i)\b(ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|AKIA[0-9A-Z]{16}|sk-[A-Za-z0-9]{20,})\b'
}

foreach ($file in $candidateFiles) {
    $normalizedPath = $file.Replace('\', '/')
    $extension = [System.IO.Path]::GetExtension($normalizedPath).ToLowerInvariant()
    $isNamedTextFile = [System.IO.Path]::GetFileName($normalizedPath) -in @('.gitignore', '.gitattributes')

    if ((-not $isNamedTextFile -and $extension -notin $textExtensions) -or
        $normalizedPath.StartsWith('StockFlow/wwwroot/lib/', [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalizedPath -eq 'scripts/validate-repository-hygiene.ps1') {
        continue
    }

    $contentLines = @(& $gitCommand.Source -C $repositoryRoot show ":$normalizedPath" 2>$null)
    if ($LASTEXITCODE -ne 0) {
        $violations.Add("[UnreadableStagedFile] $normalizedPath")
        continue
    }

    $content = $contentLines -join "`n"
    foreach ($rule in $sensitiveContentRules.GetEnumerator()) {
        if ($content -match $rule.Value) {
            $violations.Add("[$($rule.Key)] $normalizedPath")
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Error ("Repository hygiene validation FAILED.`n - " + (($violations | Sort-Object -Unique) -join "`n - "))
    exit 1
}

Write-Host 'Repository hygiene validation PASSED.'
Write-Host " - Files checked: $($candidateFiles.Count)"
Write-Host ' - Forbidden generated/local paths: none'
Write-Host ' - High-confidence secret patterns: none'
