# AppWatchdog Windows 操作マニュアル

このマニュアルは、Windows PCでAppWatchdogを初めて使う方向けの手順書です。

AppWatchdogは、指定したアプリが終了したことを検知し、自動で起動し直すためのツールです。

## 1. 最初に知っておくこと

- AppWatchdogは起動後、画面を開いたままにせず、Windowsのタスクトレイに常駐します。
- 監視対象アプリを普通に終了すると、AppWatchdogは異常終了と判断して再起動します。
- 監視対象を意図的に止めるときは、AppWatchdogの `Pause` または `Stop Process` を使用してください。
- AppWatchdog自体を終了すると、監視と自動再起動も停止します。

### AppWatchdog自体の自動起動登録は必要ですか？

Windows PCの起動後も自動で監視を始めたい場合は、自動起動登録が必要です。

手軽に設定したい場合は、WindowsのスタートアップフォルダーへAppWatchdogのショートカットを登録する方法を推奨します。AppWatchdogのタスクトレイメニューにある `Install Task Scheduler` を使用する方法も選択できます。

- PCを再起動するたびに手動でAppWatchdogを起動する運用: 登録不要
- Windowsへのログオン後、自動で監視を始めたい運用: スタートアップフォルダーへショートカットを登録
- タスクスケジューラで管理したい運用: `Install Task Scheduler` で登録
- スタートアップフォルダーとタスクスケジューラの両方への登録: 二重起動の原因になるため非推奨

## 2. ダウンロードと配置

1. GitHubの[Releasesページ](https://github.com/aikiti/AppWatchdog/releases)を開きます。
2. 最新Releaseの `AppWatchdog_win-x64_vX.Y.Z.zip` をダウンロードします。
3. ZIPファイルを右クリックし、`すべて展開` を選びます。
4. 展開したフォルダーを、今後移動しない場所へ配置します。

配置例:

```text
C:\Apps\AppWatchdog
```

配置後の例:

```text
C:\Apps\AppWatchdog\
├─ AppWatchdog.exe
├─ config\
│  └─ appsettings.example.json
└─ その他のDLLファイル
```

重要:

- ZIPファイルの中から直接起動しないでください。
- 同梱されているDLLファイルを削除しないでください。
- 自動起動を設定した後は、AppWatchdogフォルダーを移動しないでください。

## 3. 初回起動

1. `AppWatchdog.exe` をダブルクリックします。
2. Windowsの警告が表示された場合は、内容を確認して実行を許可します。
3. 画面右下のタスクトレイにAppWatchdogのアイコンが表示されます。

アイコンが見つからない場合は、タスクバー右側の `^` を押して、隠れているアイコンを表示してください。

AppWatchdogは二重起動できません。再度起動したときに `AppWatchdog is already running.` と表示された場合は、既に正常に起動しています。

## 4. 監視対象を登録する

### 4.1 設定画面を開く

次のどちらかで設定画面を開きます。

- タスクトレイのAppWatchdogアイコンをダブルクリックする
- アイコンを右クリックし、`Settings...` を選ぶ

### 4.2 新しい監視対象を追加する

1. 設定画面左下の `Add` を押します。
2. 左側に追加された `New Target` を選びます。
3. 右側の設定項目を編集します。

最初は、次の項目だけ設定すれば動作確認できます。

| 設定項目 | 入力例 | 説明 |
|---|---|---|
| `Id` | `production-app` | 対象を識別する名前。半角英数字とハイフンを推奨 |
| `Enabled` | `True` | `True` にすると監視対象になります |
| `DisplayName` | `生産管理アプリ` | AppWatchdog上で表示する分かりやすい名前 |
| `ExePath` | `C:\Apps\ProductionApp\App.exe` | 起動するEXEのフルパス |
| `ProcessName` | `App` | 監視するプロセス名。`.exe` は付けません |
| `DetectMode` | `processName` | 通常は `processName` を推奨 |

設定例:

```text
Id          : production-app
Enabled     : True
DisplayName : 生産管理アプリ
ExePath     : C:\Apps\ProductionApp\App.exe
ProcessName : App
DetectMode  : processName
```

`ProcessName` が分からない場合:

1. 監視したいアプリを起動します。
2. `Ctrl + Shift + Esc` でタスクマネージャーを開きます。
3. `詳細` タブで対象アプリのプロセス名を確認します。
4. 末尾の `.exe` を除いた名前を `ProcessName` に入力します。

例: タスクマネージャーで `FAFinderMain.exe` と表示される場合、`ProcessName` は `FAFinderMain` です。

### 4.3 設定を保存する

1. 右下の `Save & Apply` を押します。
2. `Settings saved and applied.` と表示されたら保存完了です。
3. 設定画面下部のStatus欄で、対象が `Running` になったことを確認します。

`EnsureRunningOnStart` が `True` の場合、保存後に監視対象が起動していなければ自動で起動します。

## 5. 動作確認

初回設定後は、必ず次のテストを行ってください。

1. AppWatchdogのStatus欄で、対象が `Running` になっていることを確認します。
2. 監視対象アプリを終了します。
3. 初期設定では約35秒待ちます。
   - 終了検知まで最大約5秒
   - 再起動前の待機時間が30秒
4. 監視対象アプリが自動で起動することを確認します。
5. タスクトレイのAppWatchdogアイコンを右クリックし、`Open Log Folder` からログも確認します。

自動再起動をすぐ確認したい場合は、テスト中だけ `RestartDelaySec` を `5` などに変更できます。

## 6. 日常操作

タスクトレイのAppWatchdogアイコンを右クリックすると、操作メニューが表示されます。

| メニュー | 動作 |
|---|---|
| `Settings...` | 設定画面を開く |
| `Targets` | 対象ごとの状態確認と操作 |
| `Stop Monitoring` | AppWatchdog全体の監視を一時停止 |
| `Start Monitoring` | AppWatchdog全体の監視を再開 |
| `Open Log Folder` | ログフォルダーを開く |
| `Install Task Scheduler` | Windowsログオン時の自動起動を登録 |
| `Uninstall Task Scheduler` | 自動起動の登録を解除 |
| `Exit` | AppWatchdogを終了 |

`Targets` 内の対象ごとの操作:

| メニュー | 動作 |
|---|---|
| `Pause` | 対象の監視を一時停止。対象アプリは終了しません |
| `Resume` | 対象の監視を再開 |
| `Stop Process` | 対象を停止し、監視も一時停止 |
| `Start Process` | 対象を起動し、監視も再開 |

### 監視対象を手動で終了したいとき

安全な方法は次のどちらかです。

方法A: アプリは動かしたまま監視だけ止める

```text
タスクトレイ → AppWatchdog → Targets → 対象名 → Pause
```

方法B: アプリと監視を両方止める

```text
タスクトレイ → AppWatchdog → Targets → 対象名 → Stop Process
```

`Stop Process` で実際に対象アプリも終了させるには、対象の `StopMethod` を次のどちらかに設定してください。

- `closeWindow`: 通常終了を試みる。最初はこちらを推奨
- `kill`: 強制終了する。未保存データが失われる可能性あり

`StopMethod` が `none` の場合、`Stop Process` を押しても対象アプリは終了せず、監視の一時停止だけが行われます。

## 7. Windowsログオン時に自動起動する

手軽に自動起動を設定する場合は、Windowsのスタートアップフォルダーを使用します。

1. AppWatchdogを、今後移動しないフォルダーへ配置します。
2. `AppWatchdog.exe` を右クリックし、`ショートカットの作成` を選びます。
3. `Windowsキー + R` を押します。
4. `shell:startup` と入力して `OK` を押します。
5. 開いたスタートアップフォルダーへ、作成したショートカットを移動します。
6. Windowsからサインアウトして再ログインし、AppWatchdogが起動することを確認します。

スタートアップ登録を解除する場合は、`shell:startup` でフォルダーを開き、AppWatchdogのショートカットを削除します。

### タスクスケジューラを使用する場合

AppWatchdogのタスクトレイメニューから `Install Task Scheduler` を選ぶと、タスクスケジューラへ登録できます。登録解除は `Uninstall Task Scheduler` を使用します。

注意:

- 登録後にAppWatchdogフォルダーを移動すると、自動起動できなくなります。移動する場合は、移動前に登録解除し、移動後に再登録してください。
- スタートアップフォルダーとタスクスケジューラの両方へ登録しないでください。
- どちらの方法も、Windowsへのログオン時にAppWatchdogを起動します。Windowsへ誰もログオンしていない状態での起動ではありません。
- スタートアップ登録では通常ユーザー権限で起動します。管理者権限で動作するアプリを監視する場合は、一部のプロセス情報を取得できない可能性があります。

## 8. 主な設定項目

通常運用では、初期値のままでも利用できます。

| 設定項目 | 初期値 | 説明 |
|---|---:|---|
| `CheckIntervalSec` | `5` | 対象が動作中か確認する間隔 |
| `RestartDelaySec` | `30` | 対象の終了を検知してから再起動するまでの待ち時間 |
| `StartGraceSec` | `10` | 起動直後の安定待ち時間 |
| `EnsureRunningOnStart` | `True` | AppWatchdog起動時に対象も自動起動する |
| `AllowMultipleInstances` | `False` | 同じ対象の多重起動を許可する |
| `MaxRestartsInWindow` | `6` | 一定時間内に許可する最大再起動回数 |
| `RestartWindowSec` | `600` | 再起動回数を数える時間範囲 |
| `CooldownSec` | `300` | 再起動上限に達した後、再起動を止める時間 |
| `StopMethod` | `none` | `Stop Process` 実行時の停止方法 |

### `DetectMode` の選び方

通常は `processName` を使用してください。

- `processName`: プロセス名で監視します。権限問題が起きにくく、ランチャー型アプリにも対応しやすい方式です。
- `exePath`: 実行中EXEのフルパスで監視します。プロセス情報を取得する権限がない場合、正しく検知できないことがあります。

起動用EXEが別の本体アプリを起動してすぐ終了する場合は、`ExePath` に起動用EXE、`ProcessName` に本体アプリのプロセス名を設定してください。

## 9. 設定のバックアップと復元

設定画面下部のボタンを使用します。

- `Export Zip`: 現在の設定と状態をZIPファイルへバックアップ
- `Import Zip`: バックアップZIPから設定と状態を復元

PCの入れ替え前や設定変更前に、`Export Zip` でバックアップすることを推奨します。

## 10. 困ったとき

### AppWatchdogを起動しても画面が表示されない

正常な動作です。タスクバー右側の `^` を押し、AppWatchdogのタスクトレイアイコンを探してください。

### 対象アプリが自動起動しない

次を確認してください。

- `Enabled` が `True` か
- `ExePath` が正しいか
- `ProcessName` に `.exe` を付けていないか
- 対象が `Paused` または `Cooldown` になっていないか
- `Open Log Folder` のログにエラーが出ていないか

### 対象アプリが何度も起動される

`ProcessName` が実際に動作している本体プロセス名と一致していない可能性があります。タスクマネージャーの `詳細` タブで確認してください。

ランチャーを使うアプリでは、ランチャーではなく起動後に動き続ける本体のプロセス名を設定します。

### `Stop Process` を押しても対象が終了しない

対象の `StopMethod` が `none` になっている可能性があります。`closeWindow` または `kill` に変更してください。

### `Install Task Scheduler` が失敗する

AppWatchdogを管理者として実行してから、再度 `Install Task Scheduler` を選んでください。

### Windowsから実行を止められる

AppWatchdogはコード署名されていないため、Windows SmartScreenなどの警告が表示される場合があります。ダウンロード元が公式GitHub Releaseであることを確認したうえで、組織のセキュリティ方針に従って実行してください。

## 11. アンインストール

最初に自動起動を解除します。スタートアップ登録の場合は、`shell:startup` でフォルダーを開き、AppWatchdogのショートカットを削除します。タスクスケジューラ登録の場合は、タスクトレイのAppWatchdogアイコンを右クリックし、`Uninstall Task Scheduler` を選びます。

最後にタスクトレイメニューの `Exit` を選び、AppWatchdogを配置したフォルダーを削除します。
