# てくてくファイル結合

複数のファイルを1つに結合する Windows アプリです。EXE 1個だけ。インストール不要、.NET のインストールも不要、レジストリも常駐も使いません。

[English README](README.md)

![FileMerge](docs/images/screenshot-dark-ja.png)

## 最初に知っておいてほしいこと

**FileMerge は既定では、ファイルの中身を一切変更しません。**

すべて初期設定のまま実行すると、出力は入力ファイルを**バイト単位でそのまま連結**したものになります（`copy /b a+b out` と同じ結果です）。テキストでも CSV でもログでも、分割ファイルの `.001` / `.002` でも同じです。

これが重要なのは、勝手に変換する方が危険だからです。文字コードを自動変換するツールは、各ファイルの文字コードを**推測**する必要があります。そして文字コード判定はヒューリスティックなので、外れることがあります。外れたとき、そのツールはテキストを誤った内容に書き換えてしまい、元のバイト列はもう残っていません。FileMerge は、あなたが明示的に指示しない限りこのリスクを取りません。

中身を変える可能性のある処理は、すべて**明示的にONにしたときだけ**動きます。

| 設定 | 既定値 | 動作 |
| --- | --- | --- |
| 出力の文字コード | **変換しない** | 全ファイルを1つの文字コードに揃える |
| 改行コード | **変更しない** | CRLF / LF / CR に統一する |
| ファイルの区切り | **なし** | 空行やファイル名の見出しを挿入する |
| 改行で終わっていないファイルに改行を補う | **OFF** | 2つのファイルが同じ行に繋がるのを防ぐ |
| 2 個目以降のファイルの見出し行を除く | **OFF** | 2個目以降の1行目を落とす（CSV / TSV 向け） |
| 各ファイル末尾の空行を削除する | **OFF** | 末尾の空行を取り除く |
| 2 個目以降のファイルの BOM を取り除く | **OFF** | BOM が本文の途中に残るのを防ぐ |

**警告バナーの類は一切出しません。** 文字コードが違うファイルを結合すれば文字化けしますし、2個目以降の BOM は本文の途中に残りますし、改行で終わっていないファイルは次のファイルと同じ行に繋がります。これらはすべて「何も変換しない」ことの正しい結果であり、変えたい場合は上のオプションで変えられます。画面が表示するのは、各ファイルの判定された文字コードだけです（一覧の「文字コード」列に、単なる情報として）。

## 機能

- **ドラッグ＆ドロップ**、**ファイルを追加**、**フォルダーを追加**（`*.log; *.txt` のようなワイルドカード指定、サブフォルダー再帰、該当件数のリアルタイム表示）
- **行をドラッグして並べ替え**。上下ボタン、名前順・日時順・サイズ順・パス順のソートも可能
- **自然順ソート**。`part2` が `part10` より前、`.001` が `.010` より前になります。通常のアルファベット順は分割ファイルを静かに壊すため、これを既定にしています
- **分割ファイルの復元**（`.001` `.part01` `.r00` `.z01`）をバイト単位で正確に
- 結合前に**各ファイルの文字コードを判定して表示**
- 大きなファイルでも**進捗表示とキャンセル**が可能。いったん一時ファイルに書くため、中断・失敗しても中途半端なファイルが残りません
- **入力ファイル自身への上書きを拒否**します
- **ライト / ダークテーマ**（Windows 連動または固定）
- **20言語対応**。実行中に切り替え可能。アラビア語では右から左のレイアウトになります

## ダウンロード

[Releases](../../releases) から取得してください。

| ファイル | サイズ | 必要なもの |
| --- | --- | --- |
| `FileMerge-<version>-win-x64.exe` | 約 59 MB | なし（.NET は EXE の中に同梱） |
| `FileMerge-<version>-win-arm64.exe` | 約 59 MB | なし |
| `FileMerge-<version>-win-x64-netdep.zip` | 約 0.3 MB | [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0) |

Windows 10 バージョン 1809 以降。ダブルクリックで起動します。

単一EXE版は初回起動時のみ、WPF のネイティブコンポーネントを `%TEMP%` に展開します。これは単一EXE形式の WPF の仕様で、管理者権限は不要、展開は1回だけです。

自動更新が欲しい場合は **Microsoft Store** 版もあります。

### ポータブルモード

設定は通常 `%LOCALAPPDATA%\FileMerge\` に保存されます。EXE と同じ場所に `portable.txt` という空ファイルを置くと、設定が EXE の隣に保存されるようになり、USB メモリーに入れて持ち運べます。書き込み不可のメディアでは設定が保存されないだけで、アプリは通常どおり動きます。

## 対応言語

English · 日本語 · 简体中文 · 繁體中文 · 한국어 · Español · Português (Brasil) · Français ·
Deutsch · Italiano · Русский · Українська · Polski · Nederlands · Türkçe · العربية · हिन्दी ·
Bahasa Indonesia · Tiếng Việt · ไทย

初回起動時に Windows の言語から自動選択され、画面上部でいつでも変更できます。文字列はすべて [`src/FileMerge/Localization/Strings`](src/FileMerge/Localization/Strings) に言語ごとの JSON ファイルとして置かれ、EXE に埋め込まれます（ポータブル版を単一ファイルに保つため）。`en.json` にあるキーが欠けている言語があると CI が失敗します。

## ソースからビルド

[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) が必要です。

```powershell
git clone https://github.com/hiro5791/FileMerge.git
cd FileMerge

dotnet test tests/FileMerge.Tests/FileMerge.Tests.csproj   # 43 件
dotnet run --project src/FileMerge/FileMerge.csproj

./build/Build-Portable.ps1                                  # artifacts/*.exe
```

| プロジェクト | 内容 |
| --- | --- |
| `src/FileMerge.Core` | 結合エンジン、文字コード判定、フォルダー走査。UI に依存しません |
| `src/FileMerge` | WPF の画面、ビューモデル、テーマ、20言語の文字列 |
| `tests/FileMerge.Tests` | xUnit テスト。大半は「バイト列が変化しないこと」の検証です |

アイコンとストア用タイルは `./build/New-Icons.ps1` で再生成できます。画像はバイナリとしてコミットせず、コードで描画しています。

## Microsoft Store

```powershell
./packaging/msix/Build-Msix.ps1 `
    -IdentityName '12345Publisher.FileMerge' `
    -Publisher 'CN=ABCDEFGH-1234-5678-9012-ABCDEFGHIJKL' `
    -PublisherDisplayName 'Your Publisher Name'
```

この3つの値は、Partner Center で予約したアプリ名の **製品 ID（Product identity）** に表示されるものです。パッケージは意図的に未署名のままにします。ストア提出物には Partner Center 側が署名するためです。提出手順の全体は [docs/STORE.md](docs/STORE.md) を参照してください。

## ライセンス

[MIT](LICENSE)
