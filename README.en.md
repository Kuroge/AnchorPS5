<div align="center">

<img src="docs/logo.png" width="128" alt="AnchorPS5 logo" />

# AnchorPS5

### The homebrew store for your PS5

[![Version](https://img.shields.io/badge/version-0.1.1--alpha.1-F2C14E?style=for-the-badge)](CHANGELOG.en.md)
[![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0070D1?style=for-the-badge&logo=windows11&logoColor=white)](#requirements)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License GPL v3](https://img.shields.io/badge/license-GPL%20v3-0070D1?style=for-the-badge)](LICENSE)

**[⬇️ Download](https://github.com/Kuroge/AnchorPS5/releases)** ·
**[📋 Changelog](CHANGELOG.en.md)** ·
**[📚 Catalog](https://github.com/Kuroge/AnchorPS5-catalog)** ·
**[🌍 Translate](#translating-anchorps5)**

[Español](README.md) · **English**

</div>

---

AnchorPS5 is a Windows desktop app that gathers PS5 homebrew into a catalog, downloads
it already verified and lets you know when new versions are out. Like F-Droid or the Wii
Homebrew Browser, but for PS5.

> ⚠️ **Alpha version.** It works, but it's still in development and may change a lot
> from one version to the next. See the [changelog](CHANGELOG.en.md).

![Catalog](docs/screenshots/catalogo.png)

| App page | Betas |
|---|---|
| ![ftpsrv page with an update and several files](docs/screenshots/ficha.png) | ![ProsperoLight page with a beta available](docs/screenshots/beta.png) |

## What it does

- 📚 **Homebrew catalog** with search, sorting and sections: Downloaded, Updates and New.
  It updates itself.
- 🏷️ **Origin of each app:** from the official catalog or added by you.
- ⬇️ **Verified downloads:** every file comes from its author's official GitHub release
  and is checked against its SHA-256. `.zip`, `.7z` and `.rar` files are extracted
  automatically.
  **Download all** gets every file of an app you don't have yet in one go.
- 🔄 **Per-file updates** (`0.1 → 0.2`, with each version's date) and **Update all**, per
  app or for all of them at once. When updating you can delete the previous versions: they
  are only deleted once the new one has been downloaded and checked.
- 🗂️ **Version history:** keeps previous versions so you can go back.
- ⓘ **File details:** version, channel, release and download dates, size, SHA-256, source
  and location.
- 🧪 **Optional betas:** try a file's beta and go back to stable whenever you want.
- 🏷️ **PS4** tag on files that are not for PS5.
- ➕ **Your own apps:** add apps to your copy of the catalog; they're kept when the
  official catalog is updated.
- 🔁 **Reload button** to see a version that has just been published right away.
- 🔑 **Optional GitHub sign-in** (see below).
- 🌍 **Translatable:** all texts live in JSON files.

## Requirements

- Windows 10 (version 1809 or later) or Windows 11, 64-bit.
- Nothing else: the app ships everything it needs (no need to install .NET).

## Installation

1. Download the `.zip` of the latest version from [Releases](https://github.com/Kuroge/AnchorPS5/releases).
2. Extract it to a folder (e.g. `C:\AnchorPS5`).
3. Open `AnchorPS5.exe`.

> **Windows SmartScreen:** the app isn't signed (a code-signing certificate costs money),
> so the first time Windows may warn that it's an unknown app. Click **More info → Run
> anyway**. If you prefer, check the `.zip`'s SHA-256 first against the `.sha256` file
> that comes with every version.

The first time, it asks for the language, the download folder (by default
`Downloads\AnchorPS5_Downloads`) and, if you want, your GitHub account. It's a
**portable** app: its settings live in the `config` folder next to the `.exe`.

**Updates:** AnchorPS5 tells you on startup when there's a new version and updates itself
with one click (**Update now**), keeping your settings and downloads. You can also check
from **About → Check for updates**.

## Do I need to sign in to GitHub?

No. AnchorPS5 works the same without a session:

- **Without a session:** the official catalog publishes an index several times a day with the files of
  all its apps and the app downloads it in one go, so GitHub's limit (60 requests per
  hour) doesn't affect you. A just-published version may take a few hours to
  show up; if you don't want to wait, press **Reload** (it warns you that this reload does
  use requests).
- **Signed in:** the app asks GitHub directly, with fresher data (half an hour at most)
  and a limit of 5000 requests per hour. Signing in doesn't ask for any permission on your
  account: it only raises that limit. You can sign out whenever you want from your
  account button, at the top right.

## The catalog

The app list lives in a separate repo,
[AnchorPS5-catalog](https://github.com/Kuroge/AnchorPS5-catalog). Is something missing?
You can propose it there from the GitHub website, no coding needed.

### Adding your own apps

Edit `config\catalog.json` and add the app to the `apps` list with the same format as
the others (it's explained in the catalog repo). It will show up marked as
**Added by you**.

## Translating AnchorPS5

The app comes in **Spanish** and **English**: you choose the language in the first-run
setup (your Windows language comes preselected).
Texts live in `lang\<language>.json`; to add another language:

1. Copy `lang\en.json` (or `es.json`) as `lang\<code>.json` (e.g. `fr.json`).
2. Change `_meta` (`"code": "fr"`, `"name": "Français"`) and translate the texts (not the keys).
3. The language shows up in the first-run setup, or set it in `config\config.json`
   (`"language": "fr"`).

Translations are welcome as pull requests!

## Building from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet build                      # build
dotnet test                       # tests
dotnet publish src/AnchorPS5.App -c Release -r win-x64 --self-contained
.\scripts\empaquetar.ps1          # release zip + .sha256 in dist\
```

It's built with .NET 10 and WinUI 3 (Windows App SDK). The core (`AnchorPS5.Core`)
doesn't depend on the UI and has its own tests.

## Credits

- Author: **cheyen2008** ([Kuroge](https://github.com/Kuroge)).
- [7-Zip](https://www.7-zip.org/) by Igor Pavlov (GNU LGPL), bundled to extract
  packages. Its license is in `tools\7zip\License.txt`.
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK) and
  [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (MIT license).
- **Huertas34**, from [elotrolado.net](https://www.elotrolado.net), for the initial catalog suggestion.
- Thanks to the authors of the homebrew in the catalog: AnchorPS5 only links to their
  releases.

## License

AnchorPS5 is free software under the [GNU GPL v3](LICENSE): you can use, study, modify
and share it, as long as anything you distribute based on it is also published under the
same license.
