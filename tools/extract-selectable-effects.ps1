param(
  [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
  [string]$OutputPath = (Join-Path $ProjectRoot 'data\selectable-effects.json')
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
$ExistingLocalPathByImageKey = @{}

function As-Array($Value) {
  $items = @()
  foreach ($item in $Value) { $items += $item }
  $items
}

if (Test-Path -LiteralPath $OutputPath) {
  $existingDocument = Get-Content -LiteralPath $OutputPath -Raw -Encoding UTF8 | ConvertFrom-Json
  foreach ($effect in (As-Array $existingDocument.selectableEffects)) {
    if ([string]::IsNullOrWhiteSpace([string]$effect.image.localPath)) { continue }
    $key = '{0}|{1}|{2}' -f $effect.campaignId, $effect.slot, $effect.image.sourcePath
    $ExistingLocalPathByImageKey[$key] = [string]$effect.image.localPath
  }
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

function New-SelectableEffectImage([string]$CampaignId, [string]$Slot, [string]$SourcePath) {
  $image = [ordered]@{
    source = 'arknights.wikiru.jp'
    sourcePath = $SourcePath
    sourceUrl = Convert-SourcePathToImageUrl $SourcePath
  }

  $key = '{0}|{1}|{2}' -f $CampaignId, $Slot, $SourcePath
  $localPath = $ExistingLocalPathByImageKey[$key]
  if ([string]::IsNullOrWhiteSpace($localPath) -and -not [string]::IsNullOrWhiteSpace($SourcePath)) {
    $fileName = [System.IO.Path]::GetFileName($SourcePath)
    $candidatePath = ('assets/selectable-effects/{0}/{1}/{2}' -f $CampaignId, $Slot, $fileName).Replace('\', '/')
    $candidateFullPath = Join-Path $ProjectRoot ($candidatePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    if (Test-Path -LiteralPath $candidateFullPath) {
      $localPath = $candidatePath
    }
  }

  if (-not [string]::IsNullOrWhiteSpace($localPath)) {
    $image.localPath = $localPath
  }
  [pscustomobject]$image
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

function New-SelectableEffectId([string]$CampaignId, [string]$Slot, [string]$SourcePath, [int]$Order) {
  $base = [System.IO.Path]::GetFileNameWithoutExtension($SourcePath).ToLowerInvariant()
  $base = [regex]::Replace($base, '[^a-z0-9]+', '_').Trim('_')
  if ([string]::IsNullOrWhiteSpace($base)) { $base = 'effect_{0:D2}' -f $Order }
  '{0}_selectable_{1}_{2}' -f $CampaignId, $Slot, $base
}


function New-PairedSelectableEffectId([string]$CampaignId, [string]$Slot, [string]$SourcePath, [string]$VariantRank, [int]$Order) {
  $base = [System.IO.Path]::GetFileNameWithoutExtension($SourcePath).ToLowerInvariant()
  $base = [regex]::Replace($base, '[^a-z0-9]+', '_').Trim('_')
  if ([string]::IsNullOrWhiteSpace($base)) { $base = 'effect_{0:D2}' -f $Order }
  '{0}_selectable_{1}_{2}_{3}' -f $CampaignId, $Slot, $base, $VariantRank
}

function Get-SectionBounds([string[]]$Lines, $SectionConfig) {
  $level = [int]$SectionConfig.sectionLevel
  if ($level -le 0) { $level = 3 }
  $heading = '^' + ('\*' * $level) + [regex]::Escape([string]$SectionConfig.sectionTitle) + '\s+\[#' + [regex]::Escape([string]$SectionConfig.sectionAnchor) + '\]'
  $sectionStart = -1
  for ($i = 0; $i -lt $Lines.Count; $i++) {
    if ($Lines[$i] -match $heading) { $sectionStart = $i; break }
  }
  if ($sectionStart -lt 0) { throw "Could not find selectable effect section $($SectionConfig.sectionAnchor)" }

  $sectionEnd = $Lines.Count - 1
  for ($i = $sectionStart + 1; $i -lt $Lines.Count; $i++) {
    if ($Lines[$i] -match '^(?<stars>\*+)[^*]') {
      if ($Matches['stars'].Length -le $level) { $sectionEnd = $i - 1; break }
    }
  }
  [pscustomobject][ordered]@{ Start = $sectionStart; End = $sectionEnd }
}


function Get-SelectableEffectsFromPairedVariantSection([string[]]$Lines, $SourceConfig, $SectionConfig) {
  $bounds = Get-SectionBounds $Lines $SectionConfig
  $effects = @()
  $categoryGroup = [string]$SectionConfig.defaultGroup
  $categoryGroupLabel = [string]$SectionConfig.defaultGroupLabel
  if ([string]::IsNullOrWhiteSpace($categoryGroup)) { $categoryGroup = 'standard' }
  if ([string]::IsNullOrWhiteSpace($categoryGroupLabel)) { $categoryGroupLabel = [string]$SectionConfig.slotLabel }
  $lowerRank = if ([string]::IsNullOrWhiteSpace([string]$SectionConfig.lowerVariantRank)) { 'lower' } else { [string]$SectionConfig.lowerVariantRank }
  $upperRank = if ([string]::IsNullOrWhiteSpace([string]$SectionConfig.upperVariantRank)) { 'upper' } else { [string]$SectionConfig.upperVariantRank }
  $lowerLabel = if ([string]::IsNullOrWhiteSpace([string]$SectionConfig.lowerVariantLabel)) { $lowerRank } else { [string]$SectionConfig.lowerVariantLabel }
  $upperLabel = if ([string]::IsNullOrWhiteSpace([string]$SectionConfig.upperVariantLabel)) { $upperRank } else { [string]$SectionConfig.upperVariantLabel }
  $parentKey = $null
  $parentName = $null
  $parentSourcePath = $null
  $parentOrder = 0

  for ($i = $bounds.Start; $i -le $bounds.End; $i++) {
    $line = $Lines[$i].Trim()
    foreach ($transition in (As-Array $SectionConfig.groupTransitions)) {
      if ($line.Contains([string]$transition.match)) {
        $categoryGroup = [string]$transition.group
        $categoryGroupLabel = [string]$transition.groupLabel
      }
    }
    if (-not $line.StartsWith('|')) { continue }
    $cells = Split-WikiRow $line
    if ($cells.Count -lt 3) { continue }

    $firstCell = [string]$cells[0]
    $name = Clean-WikiText $cells[1]
    $effect = Clean-WikiText $cells[2]
    if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($effect)) { continue }

    $variantRank = $null
    $variantLabel = $null
    $sourcePath = $null
    if ($firstCell -match '&(?:attachref|ref)\((?<path>[^,\);\s]+)[^;]*\);') {
      $sourcePath = ($Matches['path'].Replace('\', '/') -replace '^\./', '')
      $parentOrder++
      $parentSourcePath = $sourcePath
      $parentKey = [System.IO.Path]::GetFileNameWithoutExtension($sourcePath).ToLowerInvariant()
      $parentKey = [regex]::Replace($parentKey, '[^a-z0-9]+', '_').Trim('_')
      if ([string]::IsNullOrWhiteSpace($parentKey)) { $parentKey = 'variant_{0:D2}' -f $parentOrder }
      $parentName = $name
      $variantRank = $lowerRank
      $variantLabel = $lowerLabel
    } elseif ($firstCell.Trim() -eq '~' -and -not [string]::IsNullOrWhiteSpace($parentKey)) {
      $sourcePath = $parentSourcePath
      $variantRank = $upperRank
      $variantLabel = $upperLabel
    } else {
      continue
    }

    $variantOrder = if ($variantRank -eq $lowerRank) { 1 } else { 2 }
    $effects += [pscustomobject][ordered]@{
      id = New-PairedSelectableEffectId ([string]$SourceConfig.campaignId) ([string]$SectionConfig.slot) $sourcePath $variantRank (($parentOrder * 10) + $variantOrder)
      campaignId = [string]$SourceConfig.campaignId
      order = (($parentOrder * 10) + $variantOrder)
      slot = [string]$SectionConfig.slot
      slotLabel = [string]$SectionConfig.slotLabel
      selectionMode = [string]$SectionConfig.selectionMode
      group = $categoryGroup
      groupLabel = $categoryGroupLabel
      parentKey = $parentKey
      parentName = $parentName
      variantRank = $variantRank
      variantLabel = $variantLabel
      name = $name
      effect = $effect
      flavorText = $null
      sourcePage = [string]$SourceConfig.page
      sourceAnchor = [string]$SectionConfig.sectionAnchor
      image = New-SelectableEffectImage ([string]$SourceConfig.campaignId) ([string]$SectionConfig.slot) $sourcePath
    }
  }
  $effects
}


function Get-SelectableEffectsFromAgeVariantSection([string[]]$Lines, $SourceConfig, $SectionConfig) {
  $bounds = Get-SectionBounds $Lines $SectionConfig
  $effects = @()
  $phaseVariants = @(As-Array $SectionConfig.phaseVariants)
  if ($phaseVariants.Count -eq 0) {
    $phaseVariants = @(
      [pscustomobject][ordered]@{ match = '形成期'; rank = 'formation'; label = '形成期'; order = 1 },
      [pscustomobject][ordered]@{ match = '拡張期'; rank = 'expansion'; label = '拡張期'; order = 2 },
      [pscustomobject][ordered]@{ match = '全盛期'; rank = 'prime'; label = '全盛期'; order = 3 }
    )
  }

  $parentOrder = 0
  $parentName = $null
  $parentKey = $null
  $parentSourcePath = $null
  $parentFlavorText = $null
  $lastVariantEffect = $null

  for ($i = $bounds.Start; $i -le $bounds.End; $i++) {
    $line = $Lines[$i].Trim()
    if (-not $line.StartsWith('|')) { continue }
    $cells = Split-WikiRow $line
    if ($cells.Count -lt 2) { continue }

    $firstCell = [string]$cells[0]
    $secondCell = [string]$cells[1]

    if ($firstCell.Trim() -eq '>' -and $secondCell -match "''(?<name>[^']+)''") {
      $name = Clean-WikiText $secondCell
      if (-not [string]::IsNullOrWhiteSpace($name) -and $name -ne '時代') {
        $parentOrder++
        $parentName = $name
        $parentKey = 'age_{0:D2}' -f $parentOrder
        $parentSourcePath = $null
        $parentFlavorText = $null
        $lastVariantEffect = $null
      }
      continue
    }

    if (-not [string]::IsNullOrWhiteSpace($parentName) -and $firstCell -match '&(?:attachref|ref)\((?<path>[^,\);\s]+)[^;]*\);') {
      $parentSourcePath = ($Matches['path'].Replace('\', '/') -replace '^\./', '')
      $base = [System.IO.Path]::GetFileNameWithoutExtension($parentSourcePath).ToLowerInvariant()
      $base = [regex]::Replace($base, '[^a-z0-9]+', '_').Trim('_')
      if (-not [string]::IsNullOrWhiteSpace($base)) { $parentKey = $base }
      $parentFlavorText = Clean-WikiText $secondCell
      continue
    }

    if ([string]::IsNullOrWhiteSpace($parentName) -or [string]::IsNullOrWhiteSpace($parentSourcePath)) { continue }

    $phaseCell = Clean-WikiText $firstCell
    $matchedPhase = $null
    foreach ($phase in $phaseVariants) {
      if ($phaseCell.Contains([string]$phase.match)) { $matchedPhase = $phase; break }
    }
    if ($null -eq $matchedPhase) { continue }

    $rawEffect = $secondCell.Trim()
    if ($rawEffect -eq '~') {
      $effect = $lastVariantEffect
    } else {
      $effect = Clean-WikiText $secondCell
      if (-not [string]::IsNullOrWhiteSpace($effect)) { $lastVariantEffect = $effect }
    }
    if ([string]::IsNullOrWhiteSpace($effect)) { continue }

    $variantRank = [string]$matchedPhase.rank
    $variantLabel = [string]$matchedPhase.label
    $variantOrder = [int]$matchedPhase.order
    if ($variantOrder -le 0) { $variantOrder = 9 }

    $effects += [pscustomobject][ordered]@{
      id = New-PairedSelectableEffectId ([string]$SourceConfig.campaignId) ([string]$SectionConfig.slot) $parentSourcePath $variantRank (($parentOrder * 10) + $variantOrder)
      campaignId = [string]$SourceConfig.campaignId
      order = (($parentOrder * 10) + $variantOrder)
      slot = [string]$SectionConfig.slot
      slotLabel = [string]$SectionConfig.slotLabel
      selectionMode = [string]$SectionConfig.selectionMode
      group = $parentKey
      groupLabel = $parentName
      parentKey = $parentKey
      parentName = $parentName
      variantRank = $variantRank
      variantLabel = $variantLabel
      name = ('{0}（{1}）' -f $parentName, $variantLabel)
      effect = $effect
      flavorText = $parentFlavorText
      sourcePage = [string]$SourceConfig.page
      sourceAnchor = [string]$SectionConfig.sectionAnchor
      image = New-SelectableEffectImage ([string]$SourceConfig.campaignId) ([string]$SectionConfig.slot) $parentSourcePath
    }
  }
  $effects
}


function Get-SelectableEffectsFromSeasonalHourVariantSection([string[]]$Lines, $SourceConfig, $SectionConfig) {
  $bounds = Get-SectionBounds $Lines $SectionConfig
  $effects = @()
  $phaseVariants = @(As-Array $SectionConfig.phaseVariants)
  if ($phaseVariants.Count -eq 0) {
    $phaseVariants = @(
      [pscustomobject][ordered]@{ match = '朦朧'; rank = 'mourou'; label = '朦朧'; order = 1 },
      [pscustomobject][ordered]@{ match = '明瞭'; rank = 'meiryou'; label = '明瞭'; order = 2 },
      [pscustomobject][ordered]@{ match = '入骨'; rank = 'nyuukotsu'; label = '入骨'; order = 3 }
    )
  }

  $categoryGroup = [string]$SectionConfig.defaultGroup
  $categoryGroupLabel = [string]$SectionConfig.defaultGroupLabel
  if ([string]::IsNullOrWhiteSpace($categoryGroup)) { $categoryGroup = 'normal' }
  if ([string]::IsNullOrWhiteSpace($categoryGroupLabel)) { $categoryGroupLabel = [string]$SectionConfig.slotLabel }

  $parentOrder = 0
  $parentName = $null
  $parentKey = $null
  $parentSourcePath = $null
  $parentFlavorText = $null
  $lastVariantEffect = $null
  $skipSupplementRegion = $false

  for ($i = $bounds.Start; $i -le $bounds.End; $i++) {
    $line = $Lines[$i].Trim()
    if ($line.Contains('#region(「戌絵」')) { $skipSupplementRegion = $true; continue }
    if ($skipSupplementRegion) {
      if ($line -eq '#endregion') { $skipSupplementRegion = $false }
      continue
    }
    foreach ($transition in (As-Array $SectionConfig.groupTransitions)) {
      if ($line.Contains([string]$transition.match)) {
        $categoryGroup = [string]$transition.group
        $categoryGroupLabel = [string]$transition.groupLabel
      }
    }
    if (-not $line.StartsWith('|')) { continue }
    $cells = Split-WikiRow $line
    if ($cells.Count -lt 2) { continue }

    $firstCell = [string]$cells[0]
    $secondCell = [string]$cells[1]

    $imageMatch = [regex]::Match($firstCell, '&(?:attachref|ref)\((?<path>[^,\);\s]+)[^;]*\);')
    $nameMatch = [regex]::Match($secondCell, '&size\(20\)\{(?<name>[^{}]+)\};')
    if ($imageMatch.Success -and $nameMatch.Success) {
      $parentOrder++
      $parentSourcePath = ($imageMatch.Groups['path'].Value.Replace('\', '/') -replace '^\./', '')
      $parentName = Clean-WikiText $nameMatch.Groups['name'].Value
      $base = [System.IO.Path]::GetFileNameWithoutExtension($parentSourcePath).ToLowerInvariant()
      $base = [regex]::Replace($base, '[^a-z0-9]+', '_').Trim('_')
      $parentKey = if ([string]::IsNullOrWhiteSpace($base)) { 'seasonal_hour_{0:D2}' -f $parentOrder } else { $base }
      $parentFlavorText = Clean-WikiText $secondCell
      $lastVariantEffect = $null
      continue
    }

    if ([string]::IsNullOrWhiteSpace($parentName) -or [string]::IsNullOrWhiteSpace($parentSourcePath)) { continue }

    $phaseCell = Clean-WikiText $firstCell
    $matchedPhase = $null
    foreach ($phase in $phaseVariants) {
      if ($phaseCell.Contains([string]$phase.match)) { $matchedPhase = $phase; break }
    }
    if ($null -eq $matchedPhase) { continue }

    $rawEffect = $secondCell.Trim()
    if ($rawEffect -eq '~') {
      $effect = $lastVariantEffect
    } else {
      $effect = Clean-WikiText $secondCell
      if (-not [string]::IsNullOrWhiteSpace($effect)) { $lastVariantEffect = $effect }
    }
    if ([string]::IsNullOrWhiteSpace($effect)) { continue }

    $variantRank = [string]$matchedPhase.rank
    $variantLabel = [string]$matchedPhase.label
    $variantOrder = [int]$matchedPhase.order
    if ($variantOrder -le 0) { $variantOrder = 9 }

    $effects += [pscustomobject][ordered]@{
      id = New-PairedSelectableEffectId ([string]$SourceConfig.campaignId) ([string]$SectionConfig.slot) $parentSourcePath $variantRank (($parentOrder * 10) + $variantOrder)
      campaignId = [string]$SourceConfig.campaignId
      order = (($parentOrder * 10) + $variantOrder)
      slot = [string]$SectionConfig.slot
      slotLabel = [string]$SectionConfig.slotLabel
      selectionMode = [string]$SectionConfig.selectionMode
      group = $categoryGroup
      groupLabel = $categoryGroupLabel
      parentKey = $parentKey
      parentName = $parentName
      variantRank = $variantRank
      variantLabel = $variantLabel
      name = ('{0}（{1}）' -f $parentName, $variantLabel)
      effect = $effect
      flavorText = $parentFlavorText
      sourcePage = [string]$SourceConfig.page
      sourceAnchor = [string]$SectionConfig.sectionAnchor
      image = New-SelectableEffectImage ([string]$SourceConfig.campaignId) ([string]$SectionConfig.slot) $parentSourcePath
    }
  }
  $effects
}

function Get-SelectableEffectsFromSection([string[]]$Lines, $SourceConfig, $SectionConfig) {
  $bounds = Get-SectionBounds $Lines $SectionConfig
  $effects = @()
  $order = 0
  $group = [string]$SectionConfig.defaultGroup
  $groupLabel = [string]$SectionConfig.defaultGroupLabel
  if ([string]::IsNullOrWhiteSpace($group)) { $group = 'standard' }
  if ([string]::IsNullOrWhiteSpace($groupLabel)) { $groupLabel = [string]$SectionConfig.slotLabel }

  for ($i = $bounds.Start; $i -le $bounds.End; $i++) {
    $line = $Lines[$i].Trim()
    foreach ($transition in (As-Array $SectionConfig.groupTransitions)) {
      if ($line.Contains([string]$transition.match)) {
        $group = [string]$transition.group
        $groupLabel = [string]$transition.groupLabel
      }
    }

    if (-not ($line -match '^\|&(?:attachref|ref)\((?<path>img/[^,\);\s]+)[^;]*\);\|(?<name>.+)\|$')) { continue }
    $sourcePath = $Matches['path'].Replace('\', '/')
    $cells = Split-WikiRow $line
    if ($cells.Count -lt 2) { continue }
    $name = Clean-WikiText $cells[1]
    if ([string]::IsNullOrWhiteSpace($name) -or $name -eq '効果') { continue }

    $effect = ''
    $flavorText = $null
    for ($j = $i + 1; $j -le [Math]::Min($bounds.End, $i + 3); $j++) {
      $row = $Lines[$j].Trim()
      if (-not $row.StartsWith('|~|')) { continue }
      $rowCells = Split-WikiRow $row
      if ($rowCells.Count -lt 2) { continue }
      $candidate = Clean-WikiText $rowCells[1]
      if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
      if ([string]::IsNullOrWhiteSpace($effect)) {
        $effect = $candidate
      } else {
        $flavorText = $candidate
        break
      }
    }
    if ([string]::IsNullOrWhiteSpace($effect)) { continue }

    $order++
    $effects += [pscustomobject][ordered]@{
      id = New-SelectableEffectId ([string]$SourceConfig.campaignId) ([string]$SectionConfig.slot) $sourcePath $order
      campaignId = [string]$SourceConfig.campaignId
      order = $order
      slot = [string]$SectionConfig.slot
      slotLabel = [string]$SectionConfig.slotLabel
      selectionMode = [string]$SectionConfig.selectionMode
      group = $group
      groupLabel = $groupLabel
      name = $name
      effect = $effect
      flavorText = $flavorText
      sourcePage = [string]$SourceConfig.page
      sourceAnchor = [string]$SectionConfig.sectionAnchor
      image = New-SelectableEffectImage ([string]$SourceConfig.campaignId) ([string]$SectionConfig.slot) $sourcePath
    }
  }
  $effects
}

$configPath = Join-Path $ProjectRoot 'data\selectable-effect-sources.json'
$config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
$sources = As-Array $config.sources
if ($sources.Count -eq 0) { throw "No selectable effect sources configured in $configPath." }

$allEffects = @()
foreach ($sourceConfig in $sources) {
  $source = Get-WikiSource $sourceConfig.page
  $lines = @($source -split "`n")
  foreach ($sectionConfig in (As-Array $sourceConfig.sections)) {
    if ([string]$sectionConfig.rowMode -eq 'pairedVariants') {
      $allEffects += Get-SelectableEffectsFromPairedVariantSection $lines $sourceConfig $sectionConfig
    } elseif ([string]$sectionConfig.rowMode -eq 'ageVariants') {
      $allEffects += Get-SelectableEffectsFromAgeVariantSection $lines $sourceConfig $sectionConfig
    } elseif ([string]$sectionConfig.rowMode -eq 'seasonalHourVariants') {
      $allEffects += Get-SelectableEffectsFromSeasonalHourVariantSection $lines $sourceConfig $sectionConfig
    } else {
      $allEffects += Get-SelectableEffectsFromSection $lines $sourceConfig $sectionConfig
    }
  }
}

$doc = [pscustomobject][ordered]@{
  version = 1
  meta = [pscustomobject][ordered]@{
    generatedAt = $GeneratedAt
    source = 'arknights.wikiru.jp PukiWiki source'
    purpose = 'Selectable Integrated Strategies special effects such as IS#3 記号認識.'
  }
  selectableEffects = @($allEffects)
}

[System.IO.File]::WriteAllText($OutputPath, (($doc | ConvertTo-Json -Depth 16).Replace("`r`n", "`n") + "`n"), $Utf8NoBom)
Write-Host "Wrote $($allEffects.Count) selectable effects to $OutputPath"
