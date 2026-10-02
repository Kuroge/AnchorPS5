<#
  Empaqueta una release de AnchorPS5 · Packages an AnchorPS5 release.

  1. Publica la app (Release, win-x64, autocontenida: no hace falta instalar .NET).
  2. Añade el catálogo "de fábrica" (catalog\catalog.json + catalog\releases.json) desde el
     repo del catálogo, para que el primer arranque funcione también sin conexión.
  3. Crea dist\AnchorPS5_<Versión>_<AAAA-MM-DD_HH-MM>.zip (los ficheros de la app en la
     raíz, sin config\) y su .sha256 (formato de sha256sum).

  Uso · Usage:
    .\scripts\empaquetar.ps1 [-Version <x.y.z-etiqueta>] [-CatalogRepo <carpeta>] [-Output <carpeta>]

  -Version permite generar otra versión sin tocar Directory.Build.props (p. ej. para probar
  las actualizaciones con una 0.1.0-alpha.2 de prueba).
#>
param(
    [string]$Version,
    [string]$CatalogRepo = (Join-Path $PSScriptRoot "..\..\AnchorPS5-catalog"),
    [string]$Output = (Join-Path $PSScriptRoot "..\dist")
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "src\AnchorPS5.App\AnchorPS5.App.csproj"

# Versión: la de Directory.Build.props salvo que se indique otra.
if (-not $Version) {
    $Version = (& dotnet msbuild $project -getProperty:Version -p:Configuration=Release).Trim()
}
$prefix, $suffix = $Version -split '-', 2
$versionArgs = @("-p:VersionPrefix=$prefix", "-p:VersionSuffix=$suffix")
"AnchorPS5 $Version"

# Catálogo de fábrica.
$catalogFiles = "catalog.json", "releases.json" | ForEach-Object { Join-Path $CatalogRepo $_ }
foreach ($file in $catalogFiles) {
    if (-not (Test-Path $file)) { throw "No se encuentra $file (¿-CatalogRepo?)" }
}

# 1. Publicar en una carpeta limpia.
$publish = Join-Path $root "publish\$Version"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
& dotnet publish $project -c Release -r win-x64 --self-contained -o $publish @versionArgs -nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet publish ha fallado" }

# Fuera lo que no debe viajar: símbolos de depuración y cualquier config\ de desarrollo.
Get-ChildItem $publish -Filter *.pdb -Recurse | Remove-Item -Force
if (Test-Path (Join-Path $publish "config")) { Remove-Item (Join-Path $publish "config") -Recurse -Force }

# 2. Catálogo de fábrica.
$bundle = Join-Path $publish "catalog"
New-Item -ItemType Directory -Force $bundle | Out-Null
Copy-Item $catalogFiles $bundle -Force

# 3. Zip + SHA-256.
New-Item -ItemType Directory -Force $Output | Out-Null
$name = "AnchorPS5_{0}_{1}" -f $Version, (Get-Date -Format "yyyy-MM-dd_HH-mm")
$zip = Join-Path (Resolve-Path $Output) "$name.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($publish, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zip.sha256", "$hash  $name.zip`n", (New-Object Text.UTF8Encoding $false))

$size = "{0:N1} MB" -f ((Get-Item $zip).Length / 1MB)
""
"Listo · Done:"
"  $zip ($size)"
"  $zip.sha256"
"  SHA-256: $hash"
