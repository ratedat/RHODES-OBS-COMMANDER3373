import { execFile } from "node:child_process";
import { randomUUID } from "node:crypto";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";

const OCR_SCRIPT = String.raw`
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
Add-Type -AssemblyName System.Runtime.WindowsRuntime
Add-Type -AssemblyName System.Drawing
$null = [Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]
$null = [Windows.Storage.Streams.IRandomAccessStreamWithContentType,Windows.Storage.Streams,ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics.Imaging,ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.SoftwareBitmap,Windows.Graphics.Imaging,ContentType=WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime]
$null = [Windows.Media.Ocr.OcrResult,Windows.Foundation,ContentType=WindowsRuntime]

function Await-Op($Operation, [Type]$ResultType) {
  $methods = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq "AsTask" -and $_.IsGenericMethodDefinition -and $_.GetParameters().Count -eq 1 }
  $asyncName = "IAsyncOperation" + [char]96 + "1"
  $method = $methods | Where-Object { $_.GetParameters()[0].ParameterType.Name -eq $asyncName } | Select-Object -First 1
  $task = $method.MakeGenericMethod($ResultType).Invoke($null, @($Operation))
  $task.Wait()
  $task.Result
}

function Invoke-Ocr($ImagePath, $RegionId, $Region) {
  $file = Await-Op ([Windows.Storage.StorageFile]::GetFileFromPathAsync($ImagePath)) ([Windows.Storage.StorageFile])
  $stream = Await-Op ($file.OpenReadAsync()) ([Windows.Storage.Streams.IRandomAccessStreamWithContentType])
  $decoder = Await-Op ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
  $bitmap = Await-Op ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
  $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
  if ($null -eq $engine) { throw "Windows OCR engine is unavailable for current user profile languages." }
  $result = Await-Op ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
  $items = @()
  foreach ($line in $result.Lines) {
    $minX = 999999; $minY = 999999; $maxX = 0; $maxY = 0
    foreach ($word in $line.Words) {
      $r = $word.BoundingRect
      $minX = [Math]::Min($minX, $r.X); $minY = [Math]::Min($minY, $r.Y)
      $maxX = [Math]::Max($maxX, $r.X + $r.Width); $maxY = [Math]::Max($maxY, $r.Y + $r.Height)
    }
    $roi = $Region
    if ($maxX -gt 0 -and $maxY -gt 0) {
      if ($RegionId -eq "full" -or $null -eq $Region) {
        $roi = @{ x = [Math]::Round($minX, 1); y = [Math]::Round($minY, 1); width = [Math]::Round($maxX - $minX, 1); height = [Math]::Round($maxY - $minY, 1) }
      } else {
        $scale = [Math]::Max(1, [double](Get-RegionValue $Region "scale" 1))
        $offsetX = [double](Get-RegionValue $Region "x" 0)
        $offsetY = [double](Get-RegionValue $Region "y" 0)
        $roi = @{
          x = [Math]::Round($offsetX + ($minX / $scale), 1)
          y = [Math]::Round($offsetY + ($minY / $scale), 1)
          width = [Math]::Round(($maxX - $minX) / $scale, 1)
          height = [Math]::Round(($maxY - $minY) / $scale, 1)
        }
      }
    }
    $items += @{ text = $line.Text; regionId = $RegionId; roi = $roi; confidence = 0.7 }
  }
  @{ text = $result.Text; results = $items }
}

function Unwrap-Value($Value) {
  $current = $Value
  while ($current -is [array]) {
    if ($current.Length -eq 0) { return $null }
    $current = $current[0]
  }
  return $current
}

function Get-RegionValue($Region, $Name, $Default) {
  if ($null -eq $Region) { return $Default }
  if ($Region -is [System.Collections.IDictionary] -and $Region.Contains($Name)) {
    $value = Unwrap-Value $Region[$Name]
    if ($null -eq $value) { return $Default }
    return $value
  }
  $prop = $Region.PSObject.Properties[$Name]
  if ($null -eq $prop -or $null -eq $prop.Value) { return $Default }
  $value = Unwrap-Value $prop.Value
  if ($null -eq $value) { return $Default }
  return $value
}

function New-Crop($Image, $Region, $OutputPath) {
  $x = [Math]::Max(0, [int][double](Get-RegionValue $Region "x" 0))
  $y = [Math]::Max(0, [int][double](Get-RegionValue $Region "y" 0))
  $w = [Math]::Max(1, [int][double](Get-RegionValue $Region "width" 1))
  $h = [Math]::Max(1, [int][double](Get-RegionValue $Region "height" 1))
  if ($x + $w -gt $Image.Width) { $w = [int]($Image.Width - $x) }
  if ($y + $h -gt $Image.Height) { $h = [int]($Image.Height - $y) }
  $scaleValue = [int][double](Get-RegionValue $Region "scale" 3)
  $scale = [Math]::Max(1, $scaleValue)
  $cropWidth = [int]($w * $scale)
  $cropHeight = [int]($h * $scale)
  $crop = New-Object System.Drawing.Bitmap($cropWidth, $cropHeight)
  $graphics = [System.Drawing.Graphics]::FromImage($crop)
  $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $graphics.DrawImage($Image, (New-Object System.Drawing.Rectangle(0, 0, $cropWidth, $cropHeight)), (New-Object System.Drawing.Rectangle($x, $y, $w, $h)), [System.Drawing.GraphicsUnit]::Pixel)
  $crop.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
  $graphics.Dispose()
  $crop.Dispose()
}

$imagePath = $env:ARK_OCR_IMAGE
$regionsJson = if ($env:ARK_OCR_REGIONS_JSON) { $env:ARK_OCR_REGIONS_JSON } else { "[]" }
$regions = ConvertFrom-Json -InputObject $regionsJson
$allResults = @()
$texts = @()
$full = Invoke-Ocr $imagePath "full" $null
$texts += $full.text
$allResults += $full.results
$image = [System.Drawing.Bitmap]::FromFile($imagePath)
try {
  foreach ($region in $regions) {
    $tmp = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "rhodes-ocr-" + [Guid]::NewGuid().ToString("N") + ".png")
    New-Crop $image $region $tmp
    try {
      $regionResult = Invoke-Ocr $tmp (Get-RegionValue $region "id" "region") @{ x = (Get-RegionValue $region "x" 0); y = (Get-RegionValue $region "y" 0); width = (Get-RegionValue $region "width" 1); height = (Get-RegionValue $region "height" 1); scale = (Get-RegionValue $region "scale" 1) }
      $texts += $regionResult.text
      $allResults += $regionResult.results
    } finally {
      Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue
    }
  }
} finally {
  $image.Dispose()
}
$json = @{ text = ($texts -join " "); ocrResults = $allResults } | ConvertTo-Json -Depth 8 -Compress
[Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($json))
`;

function encodedPowerShell(script) {
  return Buffer.from(script, "utf16le").toString("base64");
}

function runPowerShellOcr({ imagePath, regions = [], timeoutMs = 30000 }) {
  return new Promise((resolve, reject) => {
    execFile("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encodedPowerShell(OCR_SCRIPT)], {
      encoding: "utf8",
      windowsHide: true,
      timeout: timeoutMs,
      maxBuffer: 16 * 1024 * 1024,
      env: {
        ...process.env,
        ARK_OCR_IMAGE: imagePath,
        ARK_OCR_REGIONS_JSON: JSON.stringify(regions),
      },
    }, (error, stdout, stderr) => {
      if (error) {
        error.stderr = stderr;
        reject(error);
        return;
      }
      resolve(stdout);
    });
  });
}

export function normalizeWindowsOcrPayload(payload = {}) {
  const ocrResults = Array.isArray(payload.ocrResults) ? payload.ocrResults : [];
  return {
    text: String(payload.text || ocrResults.map((item) => item.text).join(" ")),
    ocrResults: ocrResults
      .filter((item) => item && typeof item.text === "string" && item.text.trim())
      .map((item) => ({
        text: item.text,
        regionId: item.regionId || null,
        roi: item.roi || null,
        confidence: item.confidence ?? 0.7,
      })),
  };
}

export function parseWindowsOcrStdout(stdout) {
  const lines = String(stdout || "").split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
  const encoded = lines.at(-1) || "";
  const json = Buffer.from(encoded, "base64").toString("utf8");
  return JSON.parse(json);
}

export function createWindowsOcrTextExtractor({ enabled = process.platform === "win32", timeoutMs = 30000 } = {}) {
  return {
    async extract(frame, { regions = [] } = {}) {
      if (!enabled || !Buffer.isBuffer(frame?.bytes)) return frame;
      const dir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-ocr-"));
      const imagePath = path.join(dir, `${randomUUID()}.png`);
      try {
        await fs.writeFile(imagePath, frame.bytes);
        const stdout = await runPowerShellOcr({ imagePath, regions, timeoutMs });
        const payload = normalizeWindowsOcrPayload(parseWindowsOcrStdout(stdout));
        return {
          ...frame,
          text: payload.text,
          ocrResults: payload.ocrResults,
        };
      } finally {
        await fs.rm(dir, { recursive: true, force: true }).catch(() => {});
      }
    },
  };
}
