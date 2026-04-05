# AppWatchdog

任意のWindowsアプリケーション（EXE）を監視し、落ちたら条件に従って自動再起動する汎用ウォッチドッグツールです。

## 機能一覧

- **複数ターゲット監視**: 複数のアプリケーションを同時に監視・自動再起動
- **processName推奨モード**: ランチャーが終了しても本体プロセスを追跡可能
- **クラッシュループ防止**: 連続再起動回数制限＋クールダウン
- **手動停止対応**: Pause/Stopで意図的停止時は再起動しない（状態永続化）
- **ヘルスチェック**: TCP / HTTP / ファイルハートビート の3種類
- **GUI（トレイ常駐）**: NotifyIconから設定・監視操作・ログ確認
- **CLI/Headlessモード**: サーバー運用やスクリプト連携に対応
- **タスクスケジューラ連携**: ログオン時自動起動
- **二重起動防止**: Mutexによるアプリ側制御
- **設定エクスポート/インポート**: ZIP形式で設定＋状態を丸ごと移行
- **GitHub Releasesで永続配布**: タグpushで自動ビルド・リリース

## Quick Start

### 1. ダウンロード

[Releases](../../releases) から最新の `AppWatchdog_win-x64_vX.Y.Z.zip` をダウンロード・展開します。

### 2. 設定

```
config\appsettings.example.json → config\appsettings.json にコピー
```

`appsettings.json` を編集して監視対象を設定します:

```json
{
  "targets": [
    {
      "id": "my-app",
      "enabled": true,
      "displayName": "My Application",
      "exePath": "C:\\Program Files\\MyApp\\MyApp.exe",
      "processName": "MyApp",
      "detectMode": "processName",
      "checkIntervalSec": 5,
      "restartDelaySec": 30,
      "startGraceSec": 10,
      "ensureRunningOnStart": true,
      "maxRestartsInWindow": 6,
      "restartWindowSec": 600,
      "cooldownSec": 300,
      "stopMethod": "none",
      "manualStopBehavior": "noRestartUntilManualResume"
    }
  ]
}
```

### 3. 起動

- **GUI**: `AppWatchdog.exe` を実行（トレイに常駐）
- **Headless**: `AppWatchdog.exe --headless` または `AppWatchdog.Cli.exe run`

### 4. 自動起動設定

```
AppWatchdog.Cli.exe install-task
```

GUIからも「Install Task Scheduler」ボタンで設定可能です。

### 5. リリース（開発者向け）

```bash
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions が自動的にビルドし、Releases に ZIP を永続添付します。

## processName 推奨（重要）

**`detectMode: "processName"` を推奨します。**

理由: ランチャー型アプリ（例: `FAFinder.exe`）は、子プロセスを起動後にランチャー自体が終了するケースが多くあります。`exePath` モードではランチャーのプロセスしか見つけられず、本体が動いていても「落ちた」と誤判定して再起動してしまいます。

`processName` モードなら、起動EXE（`exePath`）と監視対象プロセス（`processName`）を分離できるため、ランチャー終了後も本体プロセスを正しく追跡できます。

```json
{
  "exePath": "C:\\Tools\\FAFinder.exe",
  "args": "/s",
  "processName": "FAFinderMain",
  "detectMode": "processName"
}
```

## 手動停止について

**アプリを止めたい場合は、必ず Pause/Stop 機能を使ってください。**

- GUI: トレイアイコン → Targets → 対象アプリ → Stop Process
- CLI: `AppWatchdog.Cli.exe stop <target-id>`

Windowsの「×ボタン」でアプリを閉じた場合、OSレベルではクラッシュとの区別が困難です。そのため、Watchdogは「落ちた」と判断して再起動します。

### manualStopHeuristic（オプション）

`manualStopHeuristic: true` を設定すると、最近 Pause/Stop コマンドが実行された直後にプロセスが消えた場合に限り、「手動終了」と判定して再起動を抑止します。完璧ではありませんが、運用上の補助として利用できます。

## 設定項目一覧

| 項目 | 型 | 既定値 | 説明 |
|------|-----|--------|------|
| `id` | string | (必須) | ターゲットのユニークID |
| `enabled` | bool | `true` | 監視の有効/無効 |
| `displayName` | string | (必須) | UI表示名 |
| `exePath` | string | (必須) | 起動に使うEXEのフルパス |
| `workDir` | string | exeのフォルダ | 作業ディレクトリ |
| `args` | string | null | 起動引数（例: `/s`）。空の場合は引数なしで起動 |
| `processName` | string | null | 監視対象のプロセス名（拡張子なし）。指定時はこちらで監視 |
| `detectMode` | string | `"processName"` | `"processName"` or `"exePath"` |
| `checkIntervalSec` | int | `5` | 生存確認の間隔（秒） |
| `restartDelaySec` | int | `30` | 落下検知後、起動前に待つ時間（秒）。待機後に再確認する |
| `startGraceSec` | int | `10` | 起動後の安定待ち時間（秒） |
| `ensureRunningOnStart` | bool | `true` | Watchdog起動時に対象が動いていなければ起動する |
| `allowMultipleInstances` | bool | `false` | 多重起動を許可するか |
| `maxRestartsInWindow` | int | `6` | ウィンドウ内の最大再起動回数 |
| `restartWindowSec` | int | `600` | 再起動回数カウントのウィンドウ（秒） |
| `cooldownSec` | int | `300` | 上限到達後のクールダウン（秒） |
| `stopMethod` | string | `"none"` | `"none"`, `"closeWindow"`, `"kill"` |
| `manualStopBehavior` | string | `"noRestartUntilManualResume"` | 手動停止時の挙動 |

### ヘルスチェック設定

```json
"healthCheck": {
  "type": "tcp",
  "host": "localhost",
  "port": 8080,
  "intervalSec": 30,
  "failureCountForHang": 3,
  "restartOnHang": true
}
```

| 項目 | 型 | 説明 |
|------|-----|------|
| `type` | string | `"none"`, `"tcp"`, `"http"`, `"fileHeartbeat"` |
| `host` | string | TCP チェック先ホスト |
| `port` | int | TCP チェック先ポート |
| `url` | string | HTTP チェック先URL（2xxで成功） |
| `filePath` | string | ハートビートファイルのパス |
| `thresholdSec` | int | ファイル最終更新からの許容秒数 |
| `intervalSec` | int | チェック間隔（秒） |
| `failureCountForHang` | int | ハング判定までの連続失敗回数 |
| `restartOnHang` | bool | ハング時に再起動するか |

### グローバル設定

| 項目 | 型 | 既定値 | 説明 |
|------|-----|--------|------|
| `logDir` | string | `"logs"` | ログディレクトリ |
| `stateDir` | string | `"state"` | 状態ファイルのディレクトリ |
| `logRetainDays` | int | `30` | ログ保持日数 |
| `manualStopHeuristic` | bool | `false` | 手動停止ヒューリスティクスの有効化 |
| `manualStopHeuristicWindowSec` | int | `10` | ヒューリスティクスの判定ウィンドウ（秒） |

## CLI コマンド

```
AppWatchdog.Cli.exe <command> [options]

Commands:
  run               Headlessモードで実行
  status            全ターゲットの状態を表示
  pause <id>        ターゲットの監視を一時停止（再起動しない）
  resume <id>       ターゲットの監視を再開
  stop <id>         プロセス停止 + 監視一時停止
  start <id>        プロセス起動 + 監視再開
  print-config      現在の設定をJSON出力
  install-task      タスクスケジューラに登録（ログオン時自動起動）
  uninstall-task    タスクスケジューラから削除
  status-task       タスクスケジューラの状態を表示
  export [path]     設定+状態をZIPエクスポート
  import <path>     ZIPからインポート

Options:
  --config <path>   設定ファイルのパスを指定
```

## ファイル構成

```
AppWatchdog/
├── AppWatchdog.exe          # GUI + Headless対応メインEXE
├── config/
│   ├── appsettings.json     # 設定ファイル（ユーザー作成）
│   └── appsettings.example.json  # 設定テンプレート
├── state/
│   └── state.json           # Pause状態等の永続化（自動生成）
├── logs/
│   └── appwatchdog*.log     # ログファイル（日次ローテーション）
└── README.md
```

## Release 手順

1. コードを main ブランチにマージ
2. タグを作成して push:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```
3. GitHub Actions (`release.yml`) が自動実行:
   - win-x64 の単体EXE（self-contained）をビルド
   - ZIP を作成
   - GitHub Releases に永続添付（Artifactsではなく Releases assets）
4. [Releases](../../releases) ページからダウンロード可能

**Artifacts は期限で削除されますが、Releases assets は永続です。**

手動実行: Actions → Release → Run workflow でタグを指定して実行も可能です。

## トラブルシューティング

### 権限エラー
- `Process.MainModule` へのアクセスには管理者権限が必要な場合があります
- `detectMode: "processName"` なら権限不要で動作します（推奨）

### 二重起動
- Mutex (`Global\AppWatchdog_SingleInstance`) で防止しています
- 既に起動中の場合はメッセージが表示されます

### ログの場所
- デフォルト: `logs/appwatchdog*.log`
- GUI: トレイアイコン → Open Log Folder
- 設定の `logDir` で変更可能

### ヘルスチェックが失敗する
- `failureCountForHang` 回連続で失敗するとハング判定されます
- `restartOnHang: false` にすればハング時の自動再起動を無効化できます
- ログでチェック結果を確認してください

### タスクスケジューラ
- 管理者権限で実行する必要があります
- `status-task` でタスクの状態を確認できます

## ビルド（開発者向け）

```bash
# ビルド
dotnet build

# 単体EXE出力
dotnet publish src/AppWatchdog.App/AppWatchdog.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o dist
```

## License

MIT
