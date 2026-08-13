# Spec: ADB / PC版 接続先モードの分離

## Objective

ADB接続とPC版ウィンドウ接続の設定値を独立して保持したまま、利用者が「今回使用する接続先」をADBまたはPC版のどちらか一方へ明示的に切り替えられるようにする。

成功時は、接続先を切り替えても反対側の設定値が失われず、「接続・撮影」「取得して反映」が選択中の接続先だけを使用する。

## Assumptions

1. 接続先は `adb` と `pc` の2択とし、同時使用モードは追加しない。
2. 非選択側の設定値は保存したまま画面上では隠し、切り替えると再表示する。
3. MAA推論provider（自動 / CPU / DirectML）は接続方式に依存しない共通設定として維持する。
4. 切替は即座に現在の操作対象へ反映する。永続化は既存の「保存」または接続成功時の設定保存で行う。
5. 既存設定は `MaaRuntime.ConnectionTargetId` をそのまま引き継ぎ、未設定・不明値は従来どおりADBを選ぶ。

## Tech Stack

- Avalonia / SukiUI
- .NET 8 / C#
- `RhodesSukiSettingsStore` の既存JSON設定
- MAAFramework ADB Controller / Win32 Controller

## Commands

- 対象テスト: `dotnet run --project tests/rhodes-suki/RhodesSuki.ServiceTests.csproj`
- UI契約テスト: `node --test tests/suki-maaframework-shell.test.mjs`
- 全テスト: `npm test`
- ビルド: `dotnet build apps/rhodes-suki/RhodesSuki.csproj --no-restore`

## Project Structure

- `apps/rhodes-suki/Models/SukiMaaRuntimeSettings.cs`
  - 接続先IDと選択肢の正規化
- `apps/rhodes-suki/Services/RhodesSukiSettingsStore.cs`
  - 既存設定の読み込み・移行・保存
- `apps/rhodes-suki/ViewModels/MainWindowViewModel.cs`
  - 排他選択、接続経路、状態表示
- `apps/rhodes-suki/Views/Workspaces/RuntimeWorkspaceView.axaml`
  - 接続先切替とADB / PC版の専用設定画面
- `tests/rhodes-suki/Program.cs`
  - 接続先ポリシーと設定移行の単体テスト
- `tests/suki-maaframework-shell.test.mjs`
  - UIとコマンド配線の契約テスト

## UI / Interaction

1. ランタイム画面上部に「使用する接続先」を配置する。
2. 「ADB / Android」と「PC版」をセグメント型RadioButtonで排他的に選択する。
3. ADB選択中はADB接続・エミュレーター強化・ADB検証・ADB候補を表示する。
4. PC版選択中はウィンドウ、撮影方式、マウス方式、キーボード方式を表示する。
5. 非選択側の設定は破棄せず、切替後に以前の入力値を復元する。
6. キーボード操作とアクセシビリティツリーで、2つの選択肢と現在値を判別できるようにする。

## Runtime Behavior

- 上部の「接続・撮影」は選択中の接続先へ接続して1枚撮影する。
- 「取得して反映」は選択中の接続先のControllerを準備する。
- ADB専用テストとPC版専用テストは、接続先選択を暗黙に変更しない。
- 接続先を切り替えた直後は、以前のControllerの成功表示を引き継がず「未接続」として表示する。
- 実際の再接続時は `RhodesMaaSession` が既存Controllerを破棄して、選択中のControllerへ切り替える。

## Code Style

接続先IDは既存カタログを通して正規化し、文字列比較をUIへ散らさない。

```csharp
SelectedPcConnectionTarget = SukiMaaConnectionTargetCatalog.Find(targetId);
```

UIのRadioButtonは現在状態をOneWayで表示し、切替コマンドへ `adb` または `pc` を渡す。

## Testing Strategy

1. RED: 選択中の接続先に応じて接続経路が変わる契約テストを先に追加する。
2. GREEN: 排他選択、接続分岐、UI表示を最小実装する。
3. REFACTOR: ADB / PC版の個別接続処理を分離し、選択を強制変更する副作用を除く。
4. 全テスト、Avaloniaビルド、実画面のADB / PC版切替を確認する。

## Boundaries

- Always:
  - ADBとPC版の既存設定値を両方保持する。
  - 既存JSON設定からの後方互換を維持する。
  - 選択中Controllerと接続状態表示を一致させる。
- Ask first:
  - 接続先の自動判定や同時接続モードを追加する。
  - 切替だけで設定ファイルへ自動保存する仕様へ変更する。
- Never:
  - 切替時に非選択側の設定を初期化する。
  - ADBとPC版の両Controllerへ同時に入力を送る。
  - Android Back keyeventを復活させる。

## Success Criteria

- ADB / PC版のどちらか一方だけが選択状態になる。
- 選択中の設定画面だけが表示される。
- ADB → PC版 → ADBと切り替えても、それぞれの入力値が維持される。
- 「接続・撮影」と「取得して反映」が選択中の接続先を使用する。
- 専用テストボタンが接続先を暗黙に変更しない。
- 既存設定の `ConnectionTargetId` が維持され、未設定はADBになる。
- 全テストとビルドが成功する。

## Approved Decisions

- 2026-08-13: 非選択側は画面から隠し、設定値は保持する。
- 2026-08-13: 切替は即時反映し、永続化は既存の保存操作または接続成功時に行う。
