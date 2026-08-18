#!/usr/bin/env bash
set -euo pipefail

ARTICLE="${1:-docs/myblog-articles/platform-engineering-mvp.md}"
MYBLOG_DIR="${MYBLOG_DIR:-/tmp/myblog}"
MYBLOG_REPO="${MYBLOG_REPO:-https://github.com/FuJiK/myblog.git}"

if [[ ! -f "$ARTICLE" ]]; then
  echo "Article not found: $ARTICLE" >&2
  exit 1
fi

if [[ ! -d "$MYBLOG_DIR/.git" ]]; then
  git clone "$MYBLOG_REPO" "$MYBLOG_DIR"
fi

git -C "$MYBLOG_DIR" checkout main
git -C "$MYBLOG_DIR" pull --ff-only origin main

slug="$(basename "$ARTICLE")"
cp "$ARTICLE" "$MYBLOG_DIR/content/articles/$slug"

git -C "$MYBLOG_DIR" add "content/articles/$slug"
if git -C "$MYBLOG_DIR" diff --cached --quiet; then
  echo "No changes to publish."
  exit 0
fi

git -C "$MYBLOG_DIR" commit -m "Add blog post: ${slug%.md}"
git -C "$MYBLOG_DIR" push origin main

echo "Published $slug to myblog/main. GitHub Actions will build and deploy gh-pages."
