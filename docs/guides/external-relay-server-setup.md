# 外部中継サーバー導入ガイド

外部中継サーバーは、Cloudflare Quick Tunnelの一時URLではなく、管理者が用意した常設URLで大会入力を運用するための機能です。

小規模な検証や短時間の大会では、3373の「簡易公開」を先に検討してください。

独自ドメイン、継続運用、通信経路の管理が必要な場合にだけ、この構成を使用します。

## 構成を選ぶ実例

### 短時間のDiscord大会

2時間だけ開催する大会で、入力担当者が1人、URLが毎回変わっても支障がないとします。

この場合は3373の「簡易公開」を使います。

大会主催者がVPS、独自ドメイン、HTTPS証明書を管理する必要はありません。

開始時に発行された一時URLを入力担当者へ個別に送り、大会終了時に「停止」を押します。

### 毎月開催する定例大会

毎月同じ案内ページから入力画面へ移動させ、運営側で稼働状況と通信先を管理したいとします。

この場合は外部中継サーバーを使い、`https://relay.example.com`のような固定URLを用意します。

relayを大会中に再起動すると進行中のセッションが消えるため、更新は大会前日までに終えます。

大会当日は3373から新しいセッションを開始し、その試合専用の入力担当者URLだけを共有します。

## 現在の実装範囲

外部中継サーバーは、配信PCと入力担当者の間で表示用snapshotと操作を受け渡します。

ADB、MAAFramework、ローカルファイルパス、3373の設定ファイルを外部へ公開するサーバーではありません。

```text
配信PCの3373
    │ HTTPS
    ▼
独自ドメインのリバースプロキシ（443/TLS）
    │ HTTP、同一サーバー内のloopbackだけ
    ▼
RHODES Tournament Relay（127.0.0.1:5180）
    ▲
    │ HTTPS
入力担当者のブラウザー
```

現在のrelayには次の運用上の制約があります。

- セッション、snapshot、操作履歴はプロセスのメモリだけに保持します。
- relayを再起動すると、進行中のセッションは失われます。
- セッションの有効期限は、最後の正常な操作から12時間です。
- 1セッションにつき操作履歴を最大500件保持し、入力画面には直近100件を返します。
- 1リクエストの上限は8 MiBです。
- 複数プロセスで同じセッションを共有する永続ストアは未実装です。

このため、現行版は単一relayプロセスで運用します。

ロードバランサーで複数台へ分散する構成には対応していません。

## 用意するもの

- 公開用のWindows ServerまたはLinuxサーバー
- サーバーへ向けた独自ドメインまたはサブドメイン
- HTTPS証明書を管理できるリバースプロキシ
- Node.js v24系
- 3373のソース一式
- 十分に長い管理トークン

3373の公開デバッグ版で検証しているNode.jsはv24.18.0です。

relayはNode.js標準ライブラリだけを使うため、`npm install`は不要です。

専用サブドメイン（例：`relay.example.com`）を推奨します。

パス配下へ配置する場合はリバースプロキシ側でパスを除去する必要があるため、構成が複雑になります。

## 管理トークンを生成する

管理トークンは、第三者が新しい入力セッションを作成することを防ぎます。

Node.jsが使える端末で、次のコマンドを実行します。

```console
node -e "console.log(require('node:crypto').randomBytes(48).toString('base64url'))"
```

出力された値をパスワード管理ソフトへ保存します。

管理トークンを入力担当者へ渡してはいけません。

3373ではセッション開始時だけ入力し、成功後は画面から消去され、設定ファイルにも保存されません。

## 環境変数

relayが使用する環境変数は次の4項目です。

| 変数 | 推奨値 | 用途 |
| --- | --- | --- |
| `TOURNAMENT_RELAY_HOST` | `127.0.0.1` | relayの待受先です。外部公開はリバースプロキシへ任せます。 |
| `TOURNAMENT_RELAY_PORT` | `5180` | relayの内部ポートです。インターネットへ直接公開しません。 |
| `TOURNAMENT_RELAY_PUBLIC_URL` | `https://relay.example.com` | 入力担当者URLへ使う公開URLです。末尾の `/` は不要です。 |
| `TOURNAMENT_RELAY_ADMIN_TOKEN` | 生成した値 | セッション作成要求を認証します。 |

`TOURNAMENT_RELAY_HOST=0.0.0.0`として直接公開する方法は推奨しません。

実装は外部アドレスへの待受時に管理トークンがなければ起動を拒否しますが、管理トークンだけでは通信を暗号化できないためです。

## このマニュアルで使う完成例

以降の実例では、次の構成を使います。

| 項目 | 例示する値 |
| --- | --- |
| 公開サーバーのIPアドレス | `203.0.113.10` |
| 公開URL | `https://relay.example.com` |
| relayの内部待受 | `127.0.0.1:5180` |
| 3373のプレイヤー表示名 | `第1試合 Player A` |
| 管理トークン | `<実際に生成した管理トークン>` |

`203.0.113.10`と`example.com`は説明用の予約値です。

そのままでは接続できないため、実際のサーバーIPアドレスと取得済みドメインへ置き換えます。

管理トークン欄もプレースホルダーであり、この文字列を本番用トークンとして使いません。

## Linuxで常設する

以下はsystemdを使う一般的な構成例です。

ディストリビューションによって、Node.js、Git、Caddyの導入方法と実行ファイルの位置は異なります。

### ソースを配置する

専用ユーザーと配置先を用意し、公開リポジトリを取得します。

```console
sudo useradd --system --home-dir /opt/rhodes-relay --shell /usr/sbin/nologin rhodes-relay
sudo git clone --depth 1 https://github.com/ratedat/RHODES-OBS-COMMANDER3373.git /opt/rhodes-relay
sudo chown -R rhodes-relay:rhodes-relay /opt/rhodes-relay
node --version
```

既存のユーザー管理方針がある場合は、専用ユーザー名と配置先を読み替えてください。

### 環境変数ファイルを作る

rootだけが読めるファイルを作成します。

```console
sudo install -m 600 -o root -g root /dev/null /etc/rhodes-relay.env
sudoedit /etc/rhodes-relay.env
```

`/etc/rhodes-relay.env`へ次の内容を記入します。

```ini
TOURNAMENT_RELAY_HOST=127.0.0.1
TOURNAMENT_RELAY_PORT=5180
TOURNAMENT_RELAY_PUBLIC_URL=https://relay.example.com
TOURNAMENT_RELAY_ADMIN_TOKEN=ここを生成した管理トークンに置き換える
```

実際の管理トークンをチャット、スクリーンショット、作業ログへ残さないでください。

### systemdへ登録する

`/etc/systemd/system/rhodes-relay.service`を作成します。

```ini
[Unit]
Description=RHODES OBS COMMANDER3373 Tournament Relay
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=rhodes-relay
Group=rhodes-relay
WorkingDirectory=/opt/rhodes-relay
EnvironmentFile=/etc/rhodes-relay.env
ExecStart=/usr/bin/node services/tournament-relay/server.mjs
Restart=on-failure
RestartSec=3
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true

[Install]
WantedBy=multi-user.target
```

`node`の実体が`/usr/bin/node`以外にある場合は、`ExecStart`を`command -v node`の結果へ変更します。

設定を反映して起動します。

```console
sudo systemctl daemon-reload
sudo systemctl enable --now rhodes-relay
sudo systemctl status rhodes-relay
curl --fail http://127.0.0.1:5180/api/health
```

正常時のhealth responseは次の形です。

```json
{"ok":true,"service":"rhodes-tournament-relay"}
```

### 内部接続を確認する実例

サーバー内部では、まずリバースプロキシを通さずrelayへ接続します。

```console
curl --fail http://127.0.0.1:5180/api/health
```

ここでhealth responseが返らない場合は、DNSやCaddyを調べる前にrelayプロセスを直します。

たとえば`Connection refused`なら、`systemctl status`と`journalctl`で起動失敗を確認します。

### HTTPSリバースプロキシを設定する

Caddyを使う場合の最小構成は次のとおりです。

```caddyfile
relay.example.com {
    reverse_proxy 127.0.0.1:5180
}
```

DNSの`A`または`AAAA`レコードをサーバーへ向け、外部からTCP 80と443へ到達できる状態でCaddyを起動します。

Caddy以外を使う場合も、公開側はHTTPS 443、転送先は`http://127.0.0.1:5180`とします。

リバースプロキシのrequest body上限は8 MiB以上にしてください。

内部ポート5180をファイアウォールで外部公開する必要はありません。

招待URLには入力コードがquery stringとして含まれます。

リバースプロキシのアクセスログへ完全なURLを記録すると入力コードも残るため、relay用ホストではquery stringを除外するか、アクセスログを無効化してください。

### 外部接続を確認する実例

DNSとCaddyを設定した後、配信PCなどサーバー外の端末からhealth endpointを確認します。

```console
curl --fail https://relay.example.com/api/health
```

内部のhealth checkと同じJSONが返れば、DNS、TLS、リバースプロキシ、relayまでの経路が通っています。

続いて、管理トークンなしではセッションを作れないことを確認します。

```console
curl -i -X POST https://relay.example.com/api/sessions \
  -H 'content-type: application/json' \
  --data '{"playerLabel":"接続確認"}'
```

正常に保護されている場合はHTTP 401となり、response bodyに次のコードが含まれます。

```json
{"error":"中継サーバーの管理認証に失敗しました。","code":"admin_auth_failed"}
```

この401は障害ではありません。

管理トークンを持たない第三者がセッションを作れないことを示す期待結果です。

## Windowsで検証する

Windowsでもrelay本体の動作は同じです。

常設前の検証では、PowerShellをリポジトリ直下で開き、現在のプロセスへ環境変数を設定します。

```powershell
$env:TOURNAMENT_RELAY_HOST = '127.0.0.1'
$env:TOURNAMENT_RELAY_PORT = '5180'
$env:TOURNAMENT_RELAY_PUBLIC_URL = 'https://relay.example.com'
$env:TOURNAMENT_RELAY_ADMIN_TOKEN = '生成した管理トークン'
node .\services\tournament-relay\server.mjs
```

共有PCでは、実際の管理トークンをPowerShell履歴へ残す運用を避けてください。

常設時は専用のWindowsアカウントを用意し、サービス管理ツールまたはタスクスケジューラから環境変数と作業フォルダーを設定します。

公開側にはCaddyなどのHTTPSリバースプロキシを置き、relayはloopbackだけで待ち受けます。

### 同じPCだけで事前確認する実例

DNSとHTTPSを設定する前に、relayと3373の接続だけを1台のWindows PCで確認できます。

PowerShellをリポジトリ直下で開き、トークンを画面と履歴へ表示せずに入力します。

```powershell
$relayToken = Read-Host '管理トークン' -AsSecureString
$env:TOURNAMENT_RELAY_HOST = '127.0.0.1'
$env:TOURNAMENT_RELAY_PORT = '5180'
$env:TOURNAMENT_RELAY_PUBLIC_URL = 'http://127.0.0.1:5180'
$env:TOURNAMENT_RELAY_ADMIN_TOKEN = [System.Net.NetworkCredential]::new('', $relayToken).Password
node .\services\tournament-relay\server.mjs
```

別のPowerShellでhealth endpointを確認します。

```powershell
Invoke-RestMethod 'http://127.0.0.1:5180/api/health'
```

3373には次の値を入力します。

| 3373の項目 | 入力例 |
| --- | --- |
| プレイヤー表示名 | `ローカル接続確認` |
| Relay URL | `http://127.0.0.1:5180` |
| Relay管理トークン | PowerShellへ入力したものと同じトークン |

「開始」を押して`http://127.0.0.1:5180/input/...`形式の入力担当者URLが表示されれば、3373とrelayの接続は成功です。

この確認で検証できるのは同じPC内の接続だけです。

外部公開前には、別端末からHTTPS URLを開く確認が残ります。

## 3373から接続する

サーバーのhealth checkとHTTPS公開を確認してから3373を設定します。

1. 3373の「出力」を開きます。
2. Node.jsを導入し、ローカルの配信サーバーを起動します。
3. 「大会入力」へプレイヤー表示名を入力します。
4. 「外部中継サーバー（上級者向け）」を開きます。
5. `Relay URL`へ`https://relay.example.com`を入力します。
6. `Relay管理トークン`へサーバーと同じ管理トークンを入力します。
7. 「開始」を押します。
8. 表示された入力担当者URLを、担当者本人へ安全な方法で共有します。
9. 入力画面を開き、現在のラン状態が表示されることを確認します。

3373は開始後、relayを約750ミリ秒間隔で確認します。

入力担当者の変更はrelayへ一度保留され、配信PCの3373が内容を検証してからローカルstateへ反映します。

3373を終了すると検証と反映も止まるため、大会中は配信PCの3373とローカル配信サーバーを稼働させたままにします。

ラン状態を手動修正した後は「現在値を同期」を押すと、入力画面へ最新snapshotを送れます。

大会終了時は「停止」を押し、relay上のセッションを削除します。

### 大会当日の反映実例

配信中のOBSに源石錐24個が表示され、入力担当者が秘宝の追加も受け付けている場面を考えます。

1. 3373のプレイヤー表示名へ`第1試合 Player A`を入力し、外部中継セッションを開始します。
2. 運営担当者は発行された`https://relay.example.com/input/<session-id>?code=<6文字コード>`形式のURLを入力担当者へ個別に送ります。
3. 入力担当者は源石錐を24から31へ変更し、追加された秘宝を選んで「変更を送信」を押します。
4. relayは変更を`pending`として保持し、3373が次の確認周期で取得します。
5. 3373は源石錐の範囲と秘宝IDをローカルデータで検証し、問題がなければ一度のstate更新として反映します。
6. 入力画面の履歴は`applied`となり、OBSにも源石錐31個と追加された秘宝が表示されます。

複数項目をまとめて送った場合、1項目でも不正なら全体が`rejected`となります。

源石錐だけが31へ変わり、秘宝だけが失敗するような部分反映は行いません。

## 公開前の確認

公開前に次の項目を確認します。

- `https://relay.example.com/api/health`が200を返す。
- HTTPアクセスがHTTPSへ転送される。
- サーバーの5180番ポートへインターネットから直接接続できない。
- 管理トークンを付けないセッション作成要求が401になる。
- 3373から開始すると入力担当者URLが公開ドメインになる。
- 入力担当者URLを別ブラウザーで開き、現在値が表示される。
- テスト変更が配信PCで検証され、OBS出力へ反映される。
- 「停止」後は同じ入力担当者URLを再利用できない。
- リバースプロキシのログへquery stringと認証headerが保存されない。

## エラーの切り分け

| 症状 | 確認箇所 |
| --- | --- |
| `502 Bad Gateway` | relayプロセス、systemd status、`127.0.0.1:5180/api/health`を確認します。 |
| 接続がtimeoutする | DNS、TCP 443、TLS証明書、サーバーのファイアウォールを確認します。 |
| 管理認証に失敗する | サーバーと3373へ入力した`TOURNAMENT_RELAY_ADMIN_TOKEN`が一致しているか確認します。 |
| 入力URLがlocalhostになる | `TOURNAMENT_RELAY_PUBLIC_URL`と3373の`Relay URL`を公開URLへ直します。 |
| `413 Payload Too Large` | リバースプロキシとrelayのrequest body上限を確認します。relay既定値は8 MiBです。 |
| セッションが見つからない | relayの再起動、12時間の期限切れ、「停止」の実行有無を確認します。 |
| 入力できるが3373へ反映されない | 3373、ローカル配信サーバー、外部セッションの稼働状態を確認します。 |
| URLは開くが表示が古い | 3373で「現在値を同期」を押し、relayとの接続エラーを確認します。 |

Linuxでは次のコマンドで直近ログを確認できます。

```console
sudo journalctl -u rhodes-relay -n 200 --no-pager
```

ログを共有するときは、管理トークン、`hostToken`、入力担当者URLのquery stringを削除します。

### 502を切り分ける実例

外部の`https://relay.example.com/api/health`が`502 Bad Gateway`でも、サーバー内の`http://127.0.0.1:5180/api/health`が成功する場合があります。

この組み合わせではrelayは動いているため、Caddyの転送先、サービス起動順、ローカルファイアウォールを確認します。

内部のhealth checkも失敗する場合は、先にrelayの起動ログを確認します。

### 認証失敗を切り分ける実例

公開health checkは成功するのに3373の「開始」だけが管理認証エラーになる場合、通信経路は通っています。

`/etc/rhodes-relay.env`のトークンを変更した後にrelayを再起動していない、または3373へ古いトークンを入力した可能性を確認します。

### 再起動後にURLが無効になる実例

大会中にrelayを再起動すると、それまでの入力担当者URLは`session_not_found`になります。

これは現行relayがセッションをメモリだけに保持するためです。

3373で古いセッションを停止扱いにし、新しいセッションを開始して新しいURLを入力担当者へ送り直します。

## 更新とトークン交換

relay更新時は進行中のセッションが失われます。

大会が行われていない時間に更新します。

```console
cd /opt/rhodes-relay
sudo -u rhodes-relay git pull --ff-only
sudo systemctl restart rhodes-relay
curl --fail http://127.0.0.1:5180/api/health
```

管理トークンが漏れた場合は、新しい値を生成して環境変数ファイルを更新し、relayを再起動します。

再起動後は3373へ新しい管理トークンを入力して、新しいセッションを開始します。

## セキュリティ境界

- relayをTLSなしでインターネットへ公開しません。
- 管理トークンをURL、ソース、設定共有、Discordへ貼り付けません。
- 入力担当者には管理トークンではなく、セッションごとの入力担当者URLだけを渡します。
- 入力担当者URLを公開チャンネルへ貼ると、そのURLを知った第三者も入力操作を送れるため、個別に共有します。
- relay自体はセッションをディスクへ保存しませんが、OS、リバースプロキシ、ホスティング事業者が接続情報を記録する場合があります。
- 現行relayは単一プロセスの一時セッション管理です。永続保存、冗長化、監査保管が必要な大会では、追加実装と運用設計が必要です。

このマニュアルは、リポジトリ内の`services/tournament-relay/server.mjs`と`session-store.mjs`に基づいています。

relay実装を更新した場合は、環境変数、期限、request body上限、保存方式を再確認してください。
