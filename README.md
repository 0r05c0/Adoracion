## Third-Party Dependencies and Licenses

This project uses the following NuGet packages. Each is compatible with the GNU GPL v3. Their licenses are summarized below:

| Package                        | Version   | License         | License URL                                                                 |
|---------------------------------|-----------|-----------------|-----------------------------------------------------------------------------|
| LibVLCSharp                     | 3.9.6     | LGPL v2.1+      | https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html                     |
| LibVLCSharp.WPF                 | 3.9.6     | LGPL v2.1+      | https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html                     |
| VideoLAN.LibVLC.Windows.GPL     | 3.0.23    | GPL v2+         | https://www.gnu.org/licenses/old-licenses/gpl-2.0.html                      |
| Microsoft.Data.Sqlite           | 10.0.5    | MIT             | https://opensource.org/licenses/MIT                                         |
| SQLite                          | 3.13.0    | Public Domain   | https://www.sqlite.org/copyright.html                                       |

### Notes

- LGPL libraries (LibVLCSharp, LibVLCSharp.WPF) are compatible with GPL v3, but you must comply with both licenses’ requirements.
- MIT and Public Domain licenses are fully compatible with GPL v3.
- For full license texts, see the respective links above or the `LICENSE` file in this repository.
- If you distribute binaries, ensure users can relink or replace LGPL libraries as required by the LGPL.