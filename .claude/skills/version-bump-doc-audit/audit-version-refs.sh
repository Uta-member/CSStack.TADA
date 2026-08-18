#!/usr/bin/env bash
# バージョンアップのたびに docs/ と README.md の記述が実態(CHANGELOG.md / src/)と
# 食い違っていないかを機械的に洗い出す。判定は正規表現ベースのヒューリスティックなので、
# 出力は「レビュー候補のリスト」であり、そのまま鵜呑みにせず目視で確認すること。
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT"

PROPS="Directory.Build.props"
CHANGELOG="CHANGELOG.md"

CURRENT_VERSION=$(grep -oE '<Version>[0-9]+\.[0-9]+\.[0-9]+</Version>' "$PROPS" | grep -oE '[0-9]+\.[0-9]+\.[0-9]+')
if [ -z "$CURRENT_VERSION" ]; then
  echo "ERROR: $PROPS から <Version> を取得できませんでした" >&2
  exit 1
fi

CHANGELOG_HEAD_VERSION=$(grep -oE '^## \[[0-9]+\.[0-9]+\.[0-9]+\]' "$CHANGELOG" | head -1 | grep -oE '[0-9]+\.[0-9]+\.[0-9]+')

echo "== バージョン整合性 =="
echo "$PROPS: $CURRENT_VERSION"
echo "$CHANGELOG 先頭セクション: $CHANGELOG_HEAD_VERSION"
if [ "$CURRENT_VERSION" != "$CHANGELOG_HEAD_VERSION" ]; then
  echo "!! 不一致: $PROPS と $CHANGELOG の先頭セクションのバージョンが異なります。まず CHANGELOG を書くこと。"
fi
echo

# CHANGELOG.md の「現在のバージョンのセクション本文」だけを取り出す
# (次の "## [" が出てくるまで)
SECTION=$(awk -v ver="$CURRENT_VERSION" '
  BEGIN { inside = 0 }
  /^## \[/ {
    if (inside) exit
    if ($0 ~ "\\[" ver "\\]") { inside = 1; next }
  }
  inside { print }
' "$CHANGELOG")

if [ -z "$SECTION" ]; then
  echo "ERROR: $CHANGELOG 内にバージョン $CURRENT_VERSION のセクションが見つかりません" >&2
  exit 1
fi

# 「削除した」と書いてある空行区切りの段落(見出し単位のまとまり)ごと、バッククォートで
# 囲まれた識別子らしきものを候補として抽出する。単一行だけを見ると、markdown の折り返しで
# 「削除しました。」という文言と削除対象の識別子の列挙が別の物理行に分かれるケースを
# 取りこぼすため、段落単位で緩く拾う。取りこぼし(偽陰性)より多少のノイズ(偽陽性)を許容する。
# Create / Validate のような残存メンバー名の典型的な誤検出はブロックリストで弾く。
BLOCKLIST='^(Create|Reconstruct|Validate|Req|Res|void|TADAException)$'
CANDIDATES=$(echo "$SECTION" \
  | awk 'BEGIN{RS=""} /削除|Removed/ {print}' \
  | grep -oE '`[A-Za-z][A-Za-z0-9_<>,\. ]*`' \
  | tr -d '`' \
  | sed -E 's/^[[:space:]]+//; s/[[:space:]]+$//' \
  | grep -vE "$BLOCKLIST" \
  | sort -u)

if [ -z "$CANDIDATES" ]; then
  echo "(v$CURRENT_VERSION のセクションから削除/破壊的変更の識別子候補を抽出できませんでした。"
  echo " 追加のみのリリースならここは空で正常。手動でも一度 CHANGELOG を確認すること)"
  exit 0
fi

echo "== v$CURRENT_VERSION で削除/破壊的変更の対象になった識別子候補 =="
echo "$CANDIDATES"
echo

echo "== docs/ と README.md 内で、これらの識別子に別バージョン番号が付いたまま残っていないか =="
FOUND_STALE=0
while IFS= read -r ident; do
  [ -z "$ident" ] && continue
  MATCHES=$(grep -rn -F -- "$ident" README.md docs/*.md 2>/dev/null || true)
  [ -z "$MATCHES" ] && continue
  while IFS= read -r line; do
    VERS_IN_LINE=$(echo "$line" | grep -oE 'v[0-9]+\.[0-9]+\.[0-9]+' || true)
    [ -z "$VERS_IN_LINE" ] && continue
    while IFS= read -r v; do
      if [ "$v" != "v$CURRENT_VERSION" ]; then
        echo "$line"
        FOUND_STALE=1
      fi
    done <<< "$VERS_IN_LINE"
  done <<< "$MATCHES"
done <<< "$CANDIDATES"
[ "$FOUND_STALE" -eq 0 ] && echo "(別バージョン番号付きの言及は見つかりませんでした)"
echo

echo "== samples/ のコメント・XML doc が、src/ に実在しない識別子を参照していないか =="
FOUND_GHOST=0
while IFS= read -r ident; do
  [ -z "$ident" ] && continue
  BARE=$(echo "$ident" | grep -oE '^[A-Za-z0-9_]+' || true)
  [ -z "$BARE" ] && continue
  IN_SRC=$(grep -rl -F -- "$BARE" src/ 2>/dev/null || true)
  IN_SAMPLES=$(grep -rn -F -- "$BARE" --include='*.cs' --include='*.md' samples/ 2>/dev/null \
    | grep -v -E '[\\/](bin|obj)[\\/]' || true)
  if [ -z "$IN_SRC" ] && [ -n "$IN_SAMPLES" ]; then
    echo "$IN_SAMPLES"
    FOUND_GHOST=1
  fi
done <<< "$CANDIDATES"
[ "$FOUND_GHOST" -eq 0 ] && echo "(該当なし)"
