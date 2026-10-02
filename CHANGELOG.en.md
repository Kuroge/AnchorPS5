# Changelog

[Español](CHANGELOG.md) · **English**

All notable changes to AnchorPS5. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow
[SemVer](https://semver.org/). While the app is in **alpha**, things may change a lot
between versions.

## [0.1.0-alpha.2] — 2026-10-02

### Changed

- The title bar's Back button becomes **Home** (🏠): it goes back to the catalog from any
  screen (About, an app page, another section) and only shows up when needed.
- In the first-run setup, **changing the language translates the screen right away**.
- **Browse…** (download folder) opens straight in the Downloads folder.

## [0.1.0-alpha.1] — 2026-10-02

First test version.

### Added

- **Homebrew catalog** as cards, with search, sorting (by name or what's new) and
  sections: Catalog, Downloaded, Updates and New (apps you hadn't seen yet).
- **Official catalog** downloaded from the `Kuroge/AnchorPS5-catalog` repo, working
  offline with the last copy. You can **add your own apps**: when a new official version
  arrives, the app asks whether to keep them or replace everything.
- **Origin of each app:** official catalog, added by you or another catalog.
- **Files always up to date from GitHub:** each app shows the files of its latest
  release (with their SHA-256), with a **PS4** tag when it applies.
- **Downloads** with progress, queue, SHA-256 verification and automatic extraction with
  bundled 7-Zip. Version history per file, with open folder and delete.
- **Updates** per file (`0.1 → 0.2`) and an **Update all** button.
- **Betas** marked with a flask 🧪: try them per file and go back to stable whenever you
  want (keeping or deleting the beta).
- **Optional GitHub sign-in** to raise the request limit from 60 to 5000 per hour, with
  your picture and profile in the title bar.
- **No more GitHub limit trouble:** the official catalog publishes an hourly index with
  the files of all its apps; without a session the app downloads it in one go instead of
  asking app by app. Signed in, it asks GitHub directly (fresher data) and the index is
  the fallback. For your own apps: 30-minute cache, automatic background refresh and automatic
  waiting when the limit is reached.
- **Reload button** to see a just-published version right away (without a session, it
  warns first that it may use up the GitHub limit).
- **App updates:** on startup (and from **About → Check for updates**) it lets you know
  about new versions with their changes; **Update now** downloads it, checks its SHA-256
  and installs it restarting the app, without touching your settings or downloads.
- **About:** version, license, links, third-party components, credits and access to the
  app log (`config/logs`) to report bugs.
- **Sturdier:** a badly written `config.json` or language file no longer closes the app
  (you're warned and a copy of the file is kept aside).
- **Back** button inside each app's page.
- **First-run setup:** language, download folder and GitHub account.
- **Translatable texts** from JSON files (`lang/`), catalog descriptions included.
  **Spanish and English** out of the box; the language is chosen in the first-run setup,
  with the Windows language preselected.
- **Design** with PlayStation colors and the PS5 boot gold, Mica background and light or
  dark theme following Windows.

[0.1.0-alpha.2]: https://github.com/Kuroge/AnchorPS5/releases/tag/v0.1.0-alpha.2
[0.1.0-alpha.1]: https://github.com/Kuroge/AnchorPS5/releases/tag/v0.1.0-alpha.1
