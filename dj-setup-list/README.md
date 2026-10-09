# dj-setup-list

DJさんが現場前にセットアップリスト（機材・連絡・音源）を作るための C# ツールです。

**テンプレートは静的なチェックリスト**です。曲データはテンプレートからは取りません。音源は別レイヤで:

| ソース | いまできること | 必要な設定 |
|--------|----------------|------------|
| **Local** | フォルダ内の音源スキャン / M3U 読込 / オフライン判定 | `DJ_LOCAL_LIBRARY` または `Music:Local:LibraryPaths` |
| **Spotify** | カタログ検索、プレイリスト取得、Web OAuth | `SPOTIFY_CLIENT_ID` / `SECRET`（+ `REFRESH_TOKEN`） |
| **SoundCloud** | 検索・URL resolve・プレイリスト取得 | `SOUNDCLOUD_CLIENT_ID` または `ACCESS_TOKEN` |
| **Apple Music** | カタログ検索・プレイリスト曲取得 | `APPLE_MUSIC_TEAM_ID` / `KEY_ID` / 秘密鍵 |

## 構成

```text
dj-setup-list/
  src/DjSetupList.Core   # モデル・テンプレート・音源プロバイダ
  src/DjSetupList        # CLI
  src/DjSetupList.Web    # Web UI (http://127.0.0.1:5088)
  sample-library/        # Local デモ用
```

## 必要環境

- .NET SDK 8

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
```

## CLI

```bash
cd dj-setup-list
export DJ_LOCAL_LIBRARY="$PWD/sample-library"

dotnet run --project src/DjSetupList -- templates
dotnet run --project src/DjSetupList -- sources
dotnet run --project src/DjSetupList -- music-search --source local --query Opening

dotnet run --project src/DjSetupList -- new -t club -e "Friday Night" -v "Club Neon" --date 2026-10-15 -o friday.md
dotnet run --project src/DjSetupList -- music-attach \
  --list setup-lists/20261015-friday-night.json \
  --source local \
  --playlist sample-library/demo-set.m3u \
  --name "Opening set"
```

## Web UI

```bash
cd dj-setup-list
export DJ_LOCAL_LIBRARY="$PWD/sample-library"
dotnet run --project src/DjSetupList.Web --urls http://127.0.0.1:5088
```

ブラウザで http://127.0.0.1:5088

- リスト作成 / チェック操作
- 音源検索（Local はすぐ、他は API キー設定後）
- Spotify は `/auth/spotify/login` で OAuth

設定は `src/DjSetupList.Web/appsettings.json` の `Music` セクション、または環境変数。

## テスト

```bash
dotnet test
```

## 診断

```bash
../../scripts/dotnet-doctor.sh
dotnet run --project src/DjSetupList -- doctor
```
