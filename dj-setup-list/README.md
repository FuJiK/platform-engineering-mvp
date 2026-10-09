# dj-setup-list

DJさんが現場前にセットアップリスト（機材・メディア・連絡事項）を素早く作るための C# CLI です。

## 必要環境

- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
# 未インストールなら（Linux / WSL）
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
```

診断:

```bash
../../scripts/dotnet-doctor.sh   # リポジトリルートからなら ./scripts/dotnet-doctor.sh
dotnet --info
dotnet run --project src/DjSetupList -- doctor
```

## すぐ使う

```bash
cd dj-setup-list
dotnet run --project src/DjSetupList -- templates
dotnet run --project src/DjSetupList -- new \
  --template club \
  --event "Friday Night" \
  --venue "Club Neon" \
  --date 2026-10-15 \
  --output friday.md
```

編集用 JSON が `setup-lists/` に保存されます。チェックを進める例:

```bash
dotnet run --project src/DjSetupList -- check \
  --list setup-lists/20261015-friday-night.json \
  --item USB

dotnet run --project src/DjSetupList -- summary \
  --list setup-lists/20261015-friday-night.json
```

## テンプレート

| ID | 用途 |
|----|------|
| `club` | クラブ / ブース持ち込み（USB・HP中心） |
| `mobile` | フル持ち込み（プレイヤー〜PA） |
| `outdoor` | 屋外 / フェス（電源・天候） |
| `wedding` | 結婚式 / パーティー（進行・禁止曲） |
| `usb-only` | USBのみの超軽量セット |

`--extra "項目A,項目B"` で追加項目を足せます。

## テスト

```bash
dotnet test
```

## 出力形式

- `md` — Markdown チェックリスト（共有向き）
- `txt` — プレーンテキスト
- `json` — 再編集・進捗管理用
