Combine multiple files into one, byte for byte. Nothing is decoded or converted.
複数のファイルを、バイト単位でそのまま1つに結合します。文字コードの変換は一切行いません。

## Which file? / どれをダウンロードすればいい？

| File | For | Notes |
| --- | --- | --- |
| `FileMerge-*-setup-x64.exe` | Most people / ほとんどの方 | Installer. Start menu entry, uninstall from Settings. No admin rights needed. / インストーラー。スタートメニューに登録、「設定」からアンインストール可。管理者権限は不要 |
| `FileMerge-*-win-x64.exe` | No installation / インストールしない | One file. Double-click and it runs. / ファイル1つ。ダブルクリックで起動 |
| `FileMerge-*-win-arm64.exe` | ARM PCs / ARM 版 Windows | Same as above for ARM (Snapdragon, etc.) / ARM 版（Snapdragon など）用 |
| `FileMerge-*-win-x64-netdep.zip` | Small download / 軽量版 | Needs the [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0). / .NET デスクトップ ランタイム 10 が必要 |

The `.zip` next to each `.exe` holds the same file.
各 `.exe` と同じ名前の `.zip` は、中身が同じです。

Windows 10 version 1809 or later. / Windows 10 バージョン 1809 以降。

The files are not code-signed yet, so Windows SmartScreen may show "Windows protected your PC" the first time. Choose **More info → Run anyway**.
まだコード署名をしていないため、初回に「Windows によって PC が保護されました」と表示されることがあります。**詳細情報 → 実行** を選んでください。

## Privacy / プライバシー

No network access, no telemetry. See [PRIVACY.md](https://github.com/hiro5791/FileMerge/blob/main/PRIVACY.md).
通信もテレメトリーもありません。

## Support / 寄付

Free, and it stays free. If it helps, you can leave a tip on [Ko-fi](https://ko-fi.com/hiro5791). Entirely optional; it unlocks nothing.
無料で、これからも無料です。役に立ったら [Ko-fi](https://ko-fi.com/hiro5791) から寄付できます。任意で、寄付によって機能が増えることはありません。
