param(
  [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
  [string]$OutputPath = (Join-Path $ProjectRoot 'data\performances.json')
)

chcp 65001 | Out-Null
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$env:PYTHONUTF8 = '1'
$env:PYTHONIOENCODING = 'utf-8'
$ErrorActionPreference = 'Stop'

$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$GeneratedAt = (Get-Date -Format 'yyyy-MM-dd')
$BaseImageUrl = 'https://arknights.wikiru.jp/'

function As-Array($Value) {
  $items = @()
  foreach ($item in $Value) { $items += $item }
  $items
}

function Convert-SourcePathToImageUrl([string]$SourcePath) {
  $parts = $SourcePath -split '/'
  $encodedParts = @()
  foreach ($part in $parts) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($part)
    $encodedParts += (-join ($bytes | ForEach-Object { $_.ToString('X2') }))
  }
  $extension = [System.IO.Path]::GetExtension($SourcePath)
  if ([string]::IsNullOrWhiteSpace($extension)) { $extension = '.png' }
  $BaseImageUrl + 'attach2/' + ($encodedParts -join '_') + $extension
}

function Get-WikiSource([string]$Page) {
  $url = 'https://arknights.wikiru.jp/?cmd=source&page=' + [uri]::EscapeDataString($Page)
  $html = (curl.exe -sS -L $url 2>$null) -join "`n"
  $match = [regex]::Match($html, '<pre id="source">(?<source>[\s\S]*?)</pre>')
  if (-not $match.Success) { throw "Could not find source pre for $Page" }
  [System.Net.WebUtility]::HtmlDecode($match.Groups['source'].Value)
}

function Split-WikiRow([string]$Line) {
  $trim = $Line.Trim()
  if (-not $trim.StartsWith('|')) { return @() }
  $parts = $trim.Split('|')
  if ($parts.Count -le 2) { return @() }
  $cells = @()
  for ($i = 1; $i -lt $parts.Count - 1; $i++) { $cells += $parts[$i] }
  $cells
}

function Clean-WikiText([string]$Text) {
  if ($null -eq $Text) { return '' }
  $t = [System.Net.WebUtility]::HtmlDecode($Text)
  $t = $t -replace '^BGCOLOR\([^\)]*\):', ''
  $t = $t -replace '^(?:CENTER|LEFT|RIGHT):', ''
  $t = $t -replace '^~', ''
  $t = $t -replace '^&nobold\{(?<inner>[\s\S]*)\};$', '${inner}'
  $guard = 0
  while ($t -match '&nobold\{([^{}]*)\};' -and $guard -lt 20) {
    $t = [regex]::Replace($t, '&nobold\{(?<inner>[^{}]*)\};', '${inner}')
    $guard++
  }
  $guard = 0
  while ($t -match '&color\([^\)]*\)\{([^{}]*)\};' -and $guard -lt 20) {
    $t = [regex]::Replace($t, '&color\([^\)]*\)\{(?<inner>[^{}]*)\};', '${inner}')
    $guard++
  }
  $t = $t -replace '&br\s*/?;', ' '
  $t = $t -replace '&ensp;|&thinsp;|&nbsp;', ' '
  $t = $t -replace '\[\[([^\]>]+)>[^\]]+\]\]', '$1'
  $t = $t -replace '\[\[([^\]]+)\]\]', '$1'
  $t = $t -replace '&tooltip\(([^)]*)\)(?:\{[^{}]*\})?;', '$1'
  $t = $t -replace '&(?:attachref|ref)\([^;]*\);', ''
  $t = $t -replace "''", ''
  $t = $t -replace "'", ''
  $t = $t -replace '~', ''
  $t = $t -replace '<([^<>]*[\u3040-\u30ff\u3400-\u9fff][^<>]*)>', '＜$1＞'
  $t = $t -replace '<[^>]+>', ''
  $t = $t -replace '\(\([^\)]*\)\)', ''
  $t = $t -replace '\s+', ' '
  $t.Trim()
}

function New-PerformanceId([string]$CampaignId, [string]$SourcePath, [int]$Order) {
  $base = [System.IO.Path]::GetFileNameWithoutExtension($SourcePath).ToLowerInvariant()
  $base = [regex]::Replace($base, '[^a-z0-9]+', '_').Trim('_')
  if ([string]::IsNullOrWhiteSpace($base)) { $base = 'performance_{0:D2}' -f $Order }
  '{0}_performance_{1}' -f $CampaignId, $base
}

function Get-PerformanceNameParts([string]$DisplayName) {
  $title = $DisplayName
  $subtitle = $null
  if ($DisplayName -match '^(?<title>.*?』)\s+(?<subtitle>.+)$') {
    $title = $Matches['title'].Trim()
    $subtitle = $Matches['subtitle'].Trim()
  }
  [pscustomobject][ordered]@{ title = $title; subtitle = $subtitle }
}

function Get-PerformancesFromSource($SourceConfig) {
  $source = Get-WikiSource $SourceConfig.page
  $lines = @($source -split "`n")
  $sectionStart = -1
  for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match ('^\*\*' + [regex]::Escape([string]$SourceConfig.sectionTitle) + '\s+\[#' + [regex]::Escape([string]$SourceConfig.sectionAnchor) + '\]')) {
      $sectionStart = $i
      break
    }
  }
  if ($sectionStart -lt 0) { throw "Could not find performance section $($SourceConfig.sectionAnchor) on $($SourceConfig.page)" }

  $sectionEnd = $lines.Count - 1
  for ($i = $sectionStart + 1; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^\*\*[^*]') { $sectionEnd = $i - 1; break }
  }

  $performances = @()
  $group = 'standard'
  $order = 0
  for ($i = $sectionStart; $i -le $sectionEnd; $i++) {
    $line = $lines[$i].Trim()
    if ($line -match '#region\(「緋染め」の演目\)') { $group = 'crimson' }
    if (-not ($line -match '^\|&(?:attachref|ref)\((?<path>img/[^,\);\s]+)[^;]*\);\|~(?<name>.+)\|$')) { continue }

    $sourcePath = $Matches['path'].Replace('\', '/')
    $cells = Split-WikiRow $line
    if ($cells.Count -lt 2) { continue }
    $displayName = Clean-WikiText $cells[1]
    if ([string]::IsNullOrWhiteSpace($displayName) -or $displayName -eq '演目') { continue }

    $effect = ''
    $flavorText = $null
    $effectLineIndex = -1
    for ($j = $i + 1; $j -le [Math]::Min($sectionEnd, $i + 3); $j++) {
      $row = $lines[$j].Trim()
      if (-not $row.StartsWith('|~|')) { continue }
      $rowCells = Split-WikiRow $row
      if ($rowCells.Count -lt 2) { continue }
      $candidate = Clean-WikiText $rowCells[1]
      if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
      if ([string]::IsNullOrWhiteSpace($effect)) {
        $effect = $candidate
        $effectLineIndex = $j
      } elseif ($group -eq 'standard') {
        $flavorText = $candidate
        break
      }
    }
    if ([string]::IsNullOrWhiteSpace($effect)) { continue }

    $order++
    $parts = Get-PerformanceNameParts $displayName
    $performances += [pscustomobject][ordered]@{
      id = New-PerformanceId $SourceConfig.campaignId $sourcePath $order
      campaignId = [string]$SourceConfig.campaignId
      order = $order
      group = $group
      title = $parts.title
      subtitle = $parts.subtitle
      name = $displayName
      effect = $effect
      flavorText = $flavorText
      sourcePage = [string]$SourceConfig.page
      sourceAnchor = [string]$SourceConfig.sectionAnchor
      image = [pscustomobject][ordered]@{
        source = 'arknights.wikiru.jp'
        sourcePath = $sourcePath
        sourceUrl = Convert-SourcePathToImageUrl $sourcePath
      }
    }
  }
  $performances
}

$configPath = Join-Path $ProjectRoot 'data\performance-sources.json'
$config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
$sources = As-Array $config.sources
if ($sources.Count -eq 0) { throw "No performance sources configured in $configPath." }

$allPerformances = @()
foreach ($sourceConfig in $sources) {
  $allPerformances += Get-PerformancesFromSource $sourceConfig
}

$doc = [pscustomobject][ordered]@{
  version = 1
  meta = [pscustomobject][ordered]@{
    generatedAt = $GeneratedAt
    source = 'arknights.wikiru.jp PukiWiki source'
    purpose = 'Selectable Integrated Strategies performance buffs such as IS#2 演目.'
  }
  performances = @($allPerformances)
}

[System.IO.File]::WriteAllText($OutputPath, (($doc | ConvertTo-Json -Depth 16).Replace("`r`n", "`n") + "`n"), $Utf8NoBom)
Write-Host "Wrote $($allPerformances.Count) performances to $OutputPath"