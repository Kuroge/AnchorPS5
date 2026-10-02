<div align="center">

<img src="docs/logo.png" width="128" alt="Logo de AnchorPS5" />

# AnchorPS5

### La tienda de homebrew para tu PS5

[![Versión](https://img.shields.io/badge/versi%C3%B3n-0.1.0--alpha.1-F2C14E?style=for-the-badge)](CHANGELOG.md)
[![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0070D1?style=for-the-badge&logo=windows11&logoColor=white)](#requisitos)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Licencia GPL v3](https://img.shields.io/badge/licencia-GPL%20v3-0070D1?style=for-the-badge)](LICENSE)

**[⬇️ Descargar](https://github.com/Kuroge/AnchorPS5/releases)** ·
**[📋 Novedades](CHANGELOG.md)** ·
**[📚 Catálogo](https://github.com/Kuroge/AnchorPS5-catalog)** ·
**[🌍 Traducir](#traducir-anchorps5)**

**Español** · [English](README.en.md)

</div>

---

AnchorPS5 es una app de escritorio para Windows que reúne el homebrew de PS5 en un
catálogo, lo descarga ya verificado y te avisa cuando hay versiones nuevas. Como F-Droid
o el Homebrew Browser de Wii, pero para PS5.

> ⚠️ **Versión alpha.** Funciona, pero aún está en desarrollo y puede cambiar mucho de
> una versión a otra. Mira las [novedades](CHANGELOG.md).

![Catálogo](docs/screenshots/catalogo.png)

| Ficha de una app | Betas |
|---|---|
| ![Ficha de ftpsrv con una actualización y varios ficheros](docs/screenshots/ficha.png) | ![Ficha de ProsperoLight con una beta disponible](docs/screenshots/beta.png) |

## Qué hace

- 📚 **Catálogo de homebrew** con búsqueda, orden y secciones: Descargadas,
  Actualizaciones y Nuevas. Se actualiza solo.
- 🏷️ **Origen de cada app:** del catálogo oficial o añadida por ti.
- ⬇️ **Descargas verificadas:** cada fichero sale de la release oficial de su autor en
  GitHub y se comprueba con su SHA-256. Los `.zip`, `.7z` y `.rar` se extraen solos.
- 🔄 **Actualizaciones por fichero** (`0.1 → 0.2`) y botón **Actualizar todo**.
- 🗂️ **Historial de versiones:** guarda las versiones anteriores para volver atrás.
- 🧪 **Betas opcionales:** prueba la beta de un fichero y vuelve a la estable cuando
  quieras.
- 🏷️ Etiqueta **PS4** en los ficheros que no son para PS5.
- ➕ **Tus propias apps:** añade apps a tu copia del catálogo; se conservan cuando el
  catálogo oficial se actualiza.
- 🔁 **Botón Recargar** para ver al momento una versión que se acaba de publicar.
- 🔑 **Inicio de sesión con GitHub opcional** (ver abajo).
- 🌍 **Traducible:** todos los textos están en ficheros JSON.

## Requisitos

- Windows 10 (versión 1809 o posterior) o Windows 11, de 64 bits.
- Nada más: la app lleva todo lo que necesita (no hace falta instalar .NET).

## Instalación

1. Descarga el `.zip` de la última versión en [Releases](https://github.com/Kuroge/AnchorPS5/releases).
2. Descomprímelo en una carpeta (p. ej. `C:\AnchorPS5`).
3. Abre `AnchorPS5.exe`.

> **Windows SmartScreen:** la app no va firmada (un certificado de firma de código es de
> pago), así que la primera vez Windows puede avisar de que es una app desconocida. Pulsa
> **Más información → Ejecutar de todas formas**. Si lo prefieres, comprueba antes el
> SHA-256 del `.zip` con el `.sha256` que acompaña a cada versión.

La primera vez te pide el idioma, la carpeta de descargas (por defecto
`Descargas\AnchorPS5_Downloads`) y, si quieres, tu cuenta de GitHub. Es una app
**portable**: su configuración vive en la carpeta `config` junto al `.exe`.

**Actualizaciones:** AnchorPS5 avisa al arrancar cuando hay una versión nueva y se
actualiza con un clic (**Actualizar ahora**), conservando tu configuración y tus
descargas. También puedes buscarlas en **Acerca de → Buscar actualizaciones**.

## ¿Hace falta iniciar sesión en GitHub?

No. AnchorPS5 funciona igual sin sesión:

- **Sin sesión:** el catálogo oficial publica cada hora un índice con los ficheros de
  todas sus apps y la app lo descarga de una vez, así que no te afecta el límite de
  GitHub (60 consultas por hora). Una versión recién publicada puede tardar hasta una
  hora y media en aparecer; si no quieres esperar, pulsa **Recargar** (te avisará de que
  esa recarga sí gasta consultas).
- **Con sesión:** la app pregunta a GitHub directamente, con datos más al día (como
  mucho media hora) y un límite de 5000 consultas por hora. El inicio de sesión no pide
  ningún permiso sobre tu cuenta: solo sirve para subir ese límite. Puedes cerrarla
  cuando quieras desde el botón de tu cuenta, arriba a la derecha.

## El catálogo

La lista de apps está en un repo aparte,
[AnchorPS5-catalog](https://github.com/Kuroge/AnchorPS5-catalog). ¿Falta alguna? Allí
puedes proponerla desde la web de GitHub, sin saber programar.

### Añadir tus propias apps

Edita `config\catalog.json` y añade la app a la lista `apps` con el mismo formato que
las demás (está explicado en el repo del catálogo). Aparecerá marcada como
**Añadida por ti**.

## Traducir AnchorPS5

La app viene en **español** e **inglés**: el idioma se elige en la configuración inicial
(viene preseleccionado el de tu Windows). Los textos están en
`lang\<idioma>.json`; para añadir otro idioma:

1. Copia `lang\en.json` (o `es.json`) como `lang\<código>.json` (p. ej. `fr.json`).
2. Cambia `_meta` (`"code": "fr"`, `"name": "Français"`) y traduce los textos (no las claves).
3. El idioma aparece en la configuración inicial, o ponlo en `config\config.json`
   (`"language": "fr"`).

¡Las traducciones son bienvenidas como pull request!

## Compilar desde el código

Necesitas el [SDK de .NET 10](https://dotnet.microsoft.com/download) en Windows.

```powershell
dotnet build                      # compilar
dotnet test                       # tests
dotnet publish src/AnchorPS5.App -c Release -r win-x64 --self-contained
.\scripts\empaquetar.ps1          # zip de release + .sha256 en dist\
```

Está hecho con .NET 10 y WinUI 3 (Windows App SDK). El núcleo (`AnchorPS5.Core`) no
depende de la interfaz y tiene sus propios tests.

## Créditos

- Autor: **cheyen2008** ([Kuroge](https://github.com/Kuroge)).
- [7-Zip](https://www.7-zip.org/) de Igor Pavlov (GNU LGPL), incluido para extraer los
  paquetes. Su licencia está en `tools\7zip\License.txt`.
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK) y
  [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (licencia MIT).
- **Huertas34**, de [elotrolado.net](https://www.elotrolado.net), por la sugerencia inicial del catálogo.
- Gracias a los autores del homebrew del catálogo: AnchorPS5 solo enlaza sus releases.

## Licencia

AnchorPS5 es software libre bajo la licencia [GNU GPL v3](LICENSE): puedes usarlo,
estudiarlo, modificarlo y compartirlo, siempre que lo que distribuyas basado en él se
publique también con la misma licencia.
