# Phase 1 Development Journal

このドキュメントは、Platform Engineering MVP の Phase 1 を進める過程での分析・検証・修正の記録です。
コミットメッセージだけでは伝わりにくい「なぜそうしたか」「何を学んだか」を残すために書いています。

## プロジェクトの目的

`domain/service.edn` を唯一の入力として、次の2つを同時に生成する MVP:

- **Provisioning Plane**: Terraform JSON → Docker / nginx
- **Operations Plane**: ops-policy.json → MCP Server（ロール別ツール公開）

## セッション記録

### 1. コードベース分析

- Clojure + EDN + Terraform JSON + MCP の最小構成
- 危険操作（delete / destroy / 任意 shell）は DSL レベルで未定義
- Operator と SRE で MCP ツールの公開範囲が変わる設計

### 2. 環境セットアップ

| 項目 | 結果 |
|------|------|
| Java 25 | OK |
| Terraform 1.15.8 | OK |
| Docker 29.4.3 | OK |
| Clojure CLI | 公式インストール（`clojure.org`）で `clj` / `clojure` が利用可能に |

**学び**: `apt install clojure` は JVM 用の古いラッパーであり、`deps.edn` の `-M:alias` には使えない。公式 Clojure CLI が必要。

### 3. MCP バグ修正

**症状**

- `tools/list` が空配列を返す
- operator でも `get_status` が「Operation is not allowed for this role」で拒否される

**原因**

`ops-policy.json` のロールは文字列（`"operator"`）だが、実行時ロールはキーワード（`:operator`）。`role-allowed?` が `contains?` で直接比較していたため常に失敗。

**修正** (`src/platform_mvp/policy.clj`)

- `role-name` で正規化して比較
- 回帰テスト `role-allowed-matches-json-string-roles` を追加

### 4. 動作確認（E2E）

```bash
clj -M:test
clj -M:generate
cd generated && terraform apply
curl http://localhost:8080
```

| 確認項目 | 結果 |
|----------|------|
| ユニットテスト | 5 tests, 0 failures |
| Terraform apply | nginx コンテナ起動 |
| HTTP 8080 | 200 |
| MCP `get_status` (operator) | `status=running` |
| MCP `get_logs` (operator) | ログ取得成功 |
| operator → `restart_service` | 拒否（期待通り） |
| sre → `restart_service`（承認なし） | `approved=true` 必須エラー |

### 5. MCP の動作確認方法

MCP は HTTP ではなく **stdio（標準入出力）** で JSON-RPC をやり取りする。

```bash
printf '%s\n%s\n%s\n' \
  '{"jsonrpc":"2.0","id":1,"method":"initialize",...}' \
  '{"jsonrpc":"2.0","method":"notifications/initialized"}' \
  '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_status","arguments":{}}}' \
  | PLATFORM_ROLE=operator clj -M:mcp
```

Cursor から使う場合は `mcp-client-config.example.json` を参考に MCP Server を登録する。

### 6. `service.edn` のポート変更（8080 → 8079）

**学び: ホットリロードはない**

```text
service.edn 変更
    → clj -M:generate（手動）
    → terraform apply（手動、コンテナ再作成）
```

| 段階 | 8080 時代 | 8079 変更後 |
|------|-----------|-------------|
| `service.edn` | 8080 | 8079 |
| `generated/main.tf.json` | 自動更新されない | generate 後に 8079 |
| Docker | 8080→80 | 8079→80（コンテナ作り直し） |
| MCP | 影響なし（コンテナ名で操作） | そのまま動作 |

## Phase 1 完了条件（チェックリスト）

- [x] EDN から Terraform JSON / ops-policy を生成できる
- [x] Terraform で nginx コンテナをプロビジョニングできる
- [x] MCP Server がロール別にツールを公開する
- [x] 危険操作がツールとして存在しない
- [x] `restart_service` が SRE + 明示承認のみ
- [x] ユニットテストが通る
- [x] E2E 動作確認（Provisioning + Operations）

## 次のステップ（Phase 2 以降）

README より:

1. Docker Adapter を AWS ECS / Azure App Service へ差し替え
2. IAM / Managed Identity を Domain Model から生成
3. MCP Gateway + Catalog
4. Approval / Audit / Rate Limit
5. AI Operator
