<#
  Publica una release de AnchorPS5 en GitHub · Publishes an AnchorPS5 release on GitHub.

  1. Empaqueta con empaquetar.ps1 (zip + .sha256).
  2. Saca las notas de la sección de esa versión en CHANGELOG.md y CHANGELOG.en.md.
  3. Crea la release v<versión> con gh (prerelease si la versión lleva etiqueta: alpha, beta…).

  Antes: subir la versión en Directory.Build.props, fechar su sección en los dos CHANGELOG,
  hacer commit y push. Requiere gh con sesión iniciada.

  Uso · Usage: .\scripts\publicar.ps1
#>
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "src\AnchorPS5.App\AnchorPS5.App.csproj"
$version = (& dotnet msbuild $project -getProperty:Version -p:Configuration=Release).Trim()
$tag = "v$version"

if (git -C $root status --porcelain) { throw "Hay cambios sin commit: haz commit y push antes de publicar." }
if (& gh release view $tag --repo Kuroge/AnchorPS5 2>$null) { throw "La release $tag ya existe." }

function Get-Section([string]$file) {
    $text = [IO.File]::ReadAllText((Join-Path $root $file))
    $match = [regex]::Match($text, "(?ms)^## \[$([regex]::Escape($version))\][^\n]*\n(.*?)(?=^## \[|^\[\d)")
    if (-not $match.Success) { throw "No hay sección [$version] en $file" }
    return $match.Groups[1].Value.Trim()
}
$es = Get-Section "CHANGELOG.md"
$en = Get-Section "CHANGELOG.en.md"

& (Join-Path $PSScriptRoot "empaquetar.ps1") | Out-Host
$zip = Get-ChildItem (Join-Path $root "dist") -Filter "AnchorPS5_${version}_*.zip" | Sort-Object LastWriteTime | Select-Object -Last 1
$sha = ([IO.File]::ReadAllText("$($zip.FullName).sha256")).Split(' ')[0]

$notes = @"
**Español** · English below

$es

**Instalación:** descarga ``$($zip.Name)``, descomprímelo en una carpeta y abre ``AnchorPS5.exe`` (Windows 10/11 de 64 bits). Si ya tienes AnchorPS5, la app te avisará y se actualizará sola. La app no va firmada: si Windows SmartScreen avisa, pulsa *Más información → Ejecutar de todas formas*.

---

**English**

$en

**Installation:** download ``$($zip.Name)``, extract it to a folder and open ``AnchorPS5.exe`` (64-bit Windows 10/11). If you already have AnchorPS5, the app will let you know and update itself. The app isn't signed: if Windows SmartScreen warns you, click *More info → Run anyway*.

SHA-256 (``$($zip.Name)``): ``$sha``
"@
$notesFile = Join-Path $env:TEMP "anchorps5-notas-$version.md"
[IO.File]::WriteAllText($notesFile, $notes, (New-Object Text.UTF8Encoding $false))

$prerelease = if ($version -match '-') { @("--prerelease") } else { @() }
& gh release create $tag $zip.FullName "$($zip.FullName).sha256" --repo Kuroge/AnchorPS5 --target main --title "AnchorPS5 $version" --notes-file $notesFile @prerelease
if ($LASTEXITCODE -ne 0) { throw "gh release create ha fallado" }
Remove-Item $notesFile
"Publicada · Published: https://github.com/Kuroge/AnchorPS5/releases/tag/$tag"
