TekuTeku File Merge - portable version
======================================

(日本語の説明は readme_ja.txt をご覧ください。)

Overview
--------
TekuTeku File Merge combines several files into one, byte for byte, in the order
you choose. Nothing is decoded, re-encoded or converted, so CSV files, logs, text
in any character encoding and split binary files (.001, .002, ...) come out
unchanged.

License
-------
Freeware, open source (MIT license). No feature limits and no trial period.
Donations are welcome but optional, and they unlock nothing. The full license
text is in LICENSE.txt.

Requirements
------------
- Windows 10 (version 1809 or later) or Windows 11
- 64-bit (x64). A separate zip is available for ARM PCs.
- The .NET runtime is included in the .exe; nothing else to install.
  (Only the small -netdep zip needs the .NET Desktop Runtime 10; its .exe is
  named FileMerge.exe.)

How to use
----------
1. Drag files onto the window, or use "Add files" / "Add folder".
2. Put them in order by dragging rows, or sort by name, date, size or path.
3. Choose where to save and press "Merge".

Two optional settings, both off by default:
- Add CRLF to files that do not end with a line break
- Remove the byte order mark (BOM) from files after the first

Installation
------------
No installation is needed. Unzip into any folder and double-click
FileMerge-<version>-win-x64.exe.

The first time, Windows SmartScreen may show "Windows protected your PC".
Choose "More info" and then "Run anyway".

While portable.txt sits next to the .exe, settings are saved beside it as
FileMerge.settings.json, and nothing is written to your user profile or the
registry. Delete portable.txt to keep settings in %LOCALAPPDATA%\FileMerge
instead.

Uninstallation
--------------
Delete the folder you unzipped. If you deleted portable.txt, also delete the
%LOCALAPPDATA%\FileMerge folder.

Privacy
-------
No network access, no telemetry, no account. The app reads only the files you
add to its list and writes only the output file you choose.

Contact
-------
Please report problems or ideas on GitHub Issues:
https://github.com/hiro5791/FileMerge/issues

Source code, installer and latest version:
https://github.com/hiro5791/FileMerge
Microsoft Store:
https://apps.microsoft.com/detail/9MSSHDP4MJZK
More apps and tools by the developer:
https://tekuteku.online/en/2026/09/20/my-apps-and-tools/

Author: Hiroyura
