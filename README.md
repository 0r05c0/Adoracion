[English] | [Español](README.es.md)

# Adoracion Media Player

**Adoracion** is a open-source multimedia player designed specifically for multi-screen presentations on worship environments. Built with C# and WPF, it leverages the powerful LibVLCSharp engine to provide high-performance playback of video, audio, and high-resolution images.

![App Screenshot](Screenshots/Adoracion_2026-04-16_232153.png)
![Settings Screenshot](Screenshots/Adoracion_2026-04-16_233034.png)

## Key Features
- **Multi-Screen Support:** Dedicated media output for secondary displays/projectors while maintaining a control interface for the operator.
- **Seamless Transitions:** Built-in crossfading for audio and smooth transitions for visual media.
- **Playlist Management:** Easily save, open, and reorder playlists with drag-and-drop support.
- **Library Integration:** Quick access to local hymn folders, favorite tracks, and removable drives (USB).
- **Customizable Themes:** Support for both Light and Dark modes with custom accent themes.
- **Native Image Rendering:** High-efficiency image display to minimize memory usage during presentations.

## Keyboard Shortcuts (Hotkeys)

| Key | Action |
|-----|--------|
| `Space` | Play / Pause |
| `Esc` | Stop playback / Clear search focus |
| `Ctrl + F` | Focus the Library Search box |
| `Ctrl + S` | Save the current playlist |
| `Ctrl + O` | Open a saved playlist |
| `Up Arrow` | Increase Volume |
| `Down Arrow` | Decrease Volume |
| `Left Arrow` | Seek backward 5 seconds |
| `Right Arrow` | Seek forward 5 seconds |

## Configuration

The application persists preferences in a `UserSettings.json` file located in the root directory.

- **Language:** Stores the current UI language code (e.g., `en`, `es`, `it`).
- **EnableLogging:** A boolean flag to enable or disable debug logging.
- **Crossfade:** Toggles the smooth audio transition feature between tracks.
- **ThemeName:** The name of the selected custom accent theme (defaults to `default`).
- **ThemeMode:** Defines the base visual style, either `Light` or `Dark`.

## Releases

You can download the latest stable binaries and view the changelog here:

- **Latest Stable:** Download x86 & x64 *[Adoracion zip](https://github.com/0r05c0/Adoracion/releases/latest)*

- **All Releases:** GitHub *[Releases Page](https://github.com/0r05c0/Adoracion/releases)*

## Installation
1. Ensure you have the .NET Runtime installed.
2. Download the latest release from the link above.
3. Extract the files and run `Adoracion.exe`.
4. Place your media files in the `Hymns` folder or add your own directories in the **Settings** menu.

---

## Third-Party Dependencies and Licenses

This project uses the following NuGet packages. Each is compatible with the GNU GPL v3. Their licenses are summarized below:

| Package                        | Version   | License         | License URL                                                                 |
|---------------------------------|-----------|-----------------|-----------------------------------------------------------------------------|
| LibVLCSharp                     | 3.9.6     | LGPL v2.1+      | https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html                     |
| LibVLCSharp.WPF                 | 3.9.6     | LGPL v2.1+      | https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html                     |
| VideoLAN.LibVLC.Windows.GPL     | 3.0.23    | GPL v2+         | https://www.gnu.org/licenses/old-licenses/gpl-2.0.html                      |
| Microsoft.Data.Sqlite           | 10.0.5    | MIT             | https://opensource.org/licenses/MIT                                         |
| SQLite                          | 3.13.0    | Public Domain   | https://www.sqlite.org/copyright.html                                       |
| Octokit                         | 13.0.1    | MIT             | https://opensource.org/licenses/MIT                                         |

### Notes

- LGPL libraries (LibVLCSharp, LibVLCSharp.WPF) are compatible with GPL v3, but you must comply with both licenses’ requirements.
- MIT and Public Domain licenses are fully compatible with GPL v3.
- For full license texts, see the respective links above or the `LICENSE` file in this repository.
- If you distribute binaries, ensure users can relink or replace LGPL libraries as required by the LGPL.