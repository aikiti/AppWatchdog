# AI Collaboration Policy (Repository Local)

このリポジトリで Claude Code / Codex が作業する際の必須ルールです。

## 1. 完了条件（必須）
- 実装・修正後は、必ず `python3 scripts/verify_repo.py` を実行する。
- 失敗したら原因を修正し、成功するまで完了扱いにしない。

## 2. クロスファイル整合（必須）
- 別ファイルのクラス/関数/フィールド名を変更した場合、参照側も同時に更新する。
- データ契約（関数の戻り値の型や構造）を変更した場合は、消費側を同時に更新する。
- C# の Model / Service 間の型整合を保つ。

## 3. ビルド
- .NET 8 / C# プロジェクト（Windows専用 WinForms）
- ビルド: `dotnet build`
- パブリッシュ: `dotnet publish src/AppWatchdog.App/AppWatchdog.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o dist`
- CI は windows-latest でのみ WinForms がビルド可能

## 4. 禁止事項
- 検証未実行のまま「完了」としない。
- config/appsettings.json を git に含めない（.gitignore済み）。

## 5. つまずき改善メモ（随時追加）
- WinForms プロジェクトは macOS/Linux ではビルド不可。CI は windows-latest を使用する。
- Process.MainModule アクセスには権限例外対策（try/catch）が必須。
