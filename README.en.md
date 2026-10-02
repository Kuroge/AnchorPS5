# AnchorPS5

[Español](README.md) · **English**

**The homebrew store for your PS5.** AnchorPS5 is a Windows desktop app that gathers PS5
homebrew into a catalog, downloads it already verified and lets you know when new
versions are out. Like F-Droid or the Wii Homebrew Browser, but for PS5.

> ⚠️ **Alpha version.** It works, but it's still in development and may change a lot
> from one version to the next. See the [changelog](CHANGELOG.en.md).

![Catalog](docs/screenshots/catalogo.png)

| App page | Betas |
|---|---|
| ![ftpsrv page with an update and several files](docs/screenshots/ficha.png) | ![DLNAPlay page with a beta available](docs/screenshots/beta.png) |

## What it does

- 📚 **Homebrew catalog** with search, sorting and sections: Downloaded, Updates and New.
- ⬇️ **Verified downloads:** every file comes from its author's official GitHub release
  and is checked against its SHA-256. `.zip`, `.7z` and `.rar` files are extracted
  automatically.
- 🔄 **Per-file updates** (`0.1 → 0.2`) and an **Update all** button.
- 🗂️ **Version history:** keeps previous versions so you can go back.
- 🧪 **Optional betas:** try a file's beta and go back to stable whenever you want.
- 🏷️ **PS4** tag on files that are not for PS5.
- ➕ **Your own apps:** add apps to your copy of the catalog; they're kept when the
  official catalog is updated.
- 🔑 **Optional GitHub sign-in** to raise the request limit from 60 to 5000 per hour.
- 🌍 **Translatable:** all texts live in JSON files.

## Requirements

- Windows 10 (version 1809 or later) or Windows 11, 64-bit.
- Nothing else: the app ships everything it needs (no need to install .NET).

## Installation

1. Download the `.zip` of the latest version from [Releases](https://github.com/Kuroge/AnchorPS5/releases).
2. Extract it to a folder (e.g. `C:\AnchorPS5`).
3. Open `AnchorPS5.exe`.

The first time, it asks for the language, the download folder (by default
`Downloads\AnchorPS5_Downloads`) and, if you want, your GitHub account. It's a
**portable** app: its settings live in the `config` folder next to the `.exe`.

## The catalog

The app list lives in a separate repo,
[AnchorPS5-catalog](https://github.com/Kuroge/AnchorPS5-catalog). Is something missing?
You can propose it there from the GitHub website, no coding needed.

### Adding your own apps

Edit `config\catalog.json` and add the app to the `apps` list with the same format as
the others (it's explained in the catalog repo). It will show up marked as
**Added by you**.

## Translating AnchorPS5

Texts live in `lang\<language>.json` (`es.json` out of the box):

1. Copy `lang\es.json` as `lang\<code>.json` (e.g. `en.json`).
2. Change `_meta` (`"code": "en"`, `"name": "English"`) and translate the texts (not the keys).
3. The language shows up in the first-run setup, or set it in `config\config.json`
   (`"language": "en"`).

Translations are welcome as pull requests!

## Building from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet build                      # build
dotnet test                       # tests
dotnet publish src/AnchorPS5.App -c Release -r win-x64 --self-contained
```

It's built with .NET 10 and WinUI 3 (Windows App SDK). The core (`AnchorPS5.Core`)
doesn't depend on the UI and has its own tests.

## Credits

- Author: **cheyen2008**.
- [7-Zip](https://www.7-zip.org/) by Igor Pavlov (GNU LGPL), bundled to extract
  packages. Its license is in `tools\7zip\License.txt`.
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK) and
  [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (MIT license).
- Thanks to the authors of the homebrew in the catalog: AnchorPS5 only links to their
  releases.

## License

AnchorPS5 is free software under the [GNU GPL v3](LICENSE): you can use, study, modify
and share it, as long as anything you distribute based on it is also published under the
same license.
