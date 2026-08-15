# Platform Engineering MVP

Clojure + EDN + Terraform JSON + MCP で、次の1本を最小構成で実証するMVPです。

```text
                 domain/service.edn
                        │
                        ▼
                Clojure Platform Core
                        │
           ┌────────────┴────────────┐
           ▼                         ▼
 generated/main.tf.json      generated/ops-policy.json
           │                         │
           ▼                         ▼
       Terraform                 MCP Server
           │                         │
           ▼                         ▼
      Docker / nginx        status / logs / restart
```

## このMVPで証明すること

1. 利用者はTerraformを直接書かず、EDNで「意図」を宣言する。
2. 同じDomain ModelからProvisioning定義とOperations Policyを生成する。
3. OperatorとSREで公開されるMCP Toolが変わる。
4. `delete` / `destroy` / 任意shell実行はToolとして存在させない。
5. `restart_service` はSREのみ、かつ `approved=true` を必須にする。

## 前提

- Java 17以上
- Clojure CLI
- Terraform
- Docker Engine / Docker Desktop

## 1. 生成

```bash
clojure -M:generate
```

生成物:

```text
generated/main.tf.json
generated/ops-policy.json
```

## 2. Provisioning

```bash
cd generated
terraform init
terraform plan
terraform apply
```

確認:

```bash
curl http://localhost:8080
```

nginxのWelcomeページが返ればProvisioning Planeは成功です。

## 3. MCP ServerをOperatorとして起動

```bash
PLATFORM_ROLE=operator clojure -M:mcp
```

Operatorに公開されるTool:

```text
get_status
get_logs
```

`restart_service` は `tools/list` に出ません。仮に直接 `tools/call` されてもサーバー側で拒否します。

## 4. MCP ServerをSREとして起動

```bash
PLATFORM_ROLE=sre clojure -M:mcp
```

SREに公開されるTool:

```text
get_status
get_logs
restart_service
```

`restart_service` は次のように明示承認が必要です。

```json
{
  "name": "restart_service",
  "arguments": {
    "approved": true
  }
}
```

## 5. MCPを手動で疎通確認

MCP stdioは1行1JSON-RPCメッセージです。まずサーバーを起動します。

```bash
PLATFORM_ROLE=operator clojure -M:mcp
```

標準入力へ以下を1行ずつ渡します。

### initialize

```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"manual-test","version":"0.1.0"}}}
```

### initialized notification

```json
{"jsonrpc":"2.0","method":"notifications/initialized"}
```

### tools/list

```json
{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}
```

### get_status

```json
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_status","arguments":{}}}
```

### get_logs

```json
{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"get_logs","arguments":{"lines":20}}}
```

## 6. テスト

```bash
clojure -M:test
```

## Domain Model

`domain/service.edn` が唯一の入力です。

```clojure
{:service :platform-mvp-web
 :environment :local

 :container
 {:image "nginx:alpine"
  :host-port 8080
  :container-port 80}

 :operations
 {:get-status
  {:roles #{:operator :sre}}

  :get-logs
  {:roles #{:operator :sre}
   :max-lines 200}

  :restart-service
  {:roles #{:sre}
   :approval :required}}}
```

ここからClojureが「作り方」と「運用可能範囲」の両方を生成します。

## セキュリティ上の意図

このMVPでは、危険操作を「権限で禁止するだけ」にしていません。

```text
1. Domain DSLで未定義
2. MCP Toolとして未実装
3. Tool一覧にも出ない
4. 実行時にもroleを再検証
5. shell文字列を組み立てずProcessBuilderへ引数配列で渡す
```

本番化するときは、さらにCloud IAM / Managed Identity、OIDC、監査ログ、Rate Limit、Approval Service、MCP Gateway / Catalogを追加します。

## 次の拡張

このローカルMVPが動いたら、Docker AdapterをAWSまたはAzureへ差し替えます。

```text
Phase 1: Docker + nginx      ← 今ここ
Phase 2: AWS ECS or Azure App Service
Phase 3: IAM / Managed IdentityをDomain Modelから生成
Phase 4: MCP Gateway + Catalog
Phase 5: Approval / Audit / Rate Limit
Phase 6: AI Operator
```
