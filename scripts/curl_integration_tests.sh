#!/usr/bin/env bash
set -euo pipefail

# cURL-based integration tests for the FTP server.
# Requirements: bash, curl, coreutils (dd, cmp), dotnet SDK.

ROOT_DIR=$(cd "$(dirname "$0")/.." && pwd)
TMPDIR=$(mktemp -d)
PORT=${PORT:-2121}
PASV_START=${PASV_START:-49152}
PASV_END=${PASV_END:-49162}
SERVER_LOG="$TMPDIR/server.log"

cleanup() {
  if [[ -n "${SERVER_PID:-}" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
    kill "$SERVER_PID" 2>/dev/null || true
    sleep 0.3 || true
    kill -9 "$SERVER_PID" 2>/dev/null || true
  fi
  rm -rf "$TMPDIR" || true
}
trap cleanup EXIT

echo "[info] Temp dir: $TMPDIR"

# Configure server via environment variables (FTP_ prefix)
export FTP_FTPSERVER__LISTENADDRESS=127.0.0.1
export FTP_FTPSERVER__PORT=$PORT
export FTP_FTPSERVER__MAXSESSIONS=10
export FTP_FTPSERVER__PASSIVEPORTRANGESTART=$PASV_START
export FTP_FTPSERVER__PASSIVEPORTRANGEEND=$PASV_END
export FTP_FTPSERVER__AUTHENTICATOR=InMemory
export FTP_FTPSERVER__STORAGEPROVIDER=FileSystem
export FTP_FTPSERVER__STORAGEROOT="$TMPDIR"
export FTP_FTPSERVER__HEALTHENABLED=false

echo "[info] Starting server on 127.0.0.1:$PORT (PASV $PASV_START-$PASV_END)"
dotnet run --project "$ROOT_DIR/FtpServer.App" --no-build >"$SERVER_LOG" 2>&1 &
SERVER_PID=$!

# Wait for server to accept connections
echo -n "[info] Waiting for server to be ready"
for i in {1..60}; do
  if curl -s --user anonymous: "ftp://127.0.0.1:$PORT/" >/dev/null 2>&1; then
    echo " - ready"
    break
  fi
  echo -n "."
  sleep 0.5
  if [[ $i -eq 60 ]]; then
    echo "\n[error] Server did not start in time. Log follows:" >&2
    tail -n +1 "$SERVER_LOG" >&2 || true
    exit 1
  fi
done

FTP_URL="ftp://127.0.0.1:$PORT"
AUTH=(-u "anonymous:")

step() { echo "[test] $*"; }

step "MKD/CWD/PWD"
curl -sS "${AUTH[@]}" "$FTP_URL/" --quote "MKD testdir" --quote "CWD testdir" --quote "PWD" >/dev/null

step "STOR upload of hello.txt"
printf 'hello' >"$TMPDIR/local1.txt"
curl -sS "${AUTH[@]}" -T "$TMPDIR/local1.txt" "$FTP_URL/testdir/hello.txt" >/dev/null

step "LIST includes hello.txt"
curl -sS "${AUTH[@]}" "$FTP_URL/testdir/" | grep -q "hello.txt"

step "RETR download hello.txt and verify contents"
curl -sS "${AUTH[@]}" "$FTP_URL/testdir/hello.txt" -o "$TMPDIR/dl1.txt" >/dev/null
cmp "$TMPDIR/local1.txt" "$TMPDIR/dl1.txt"

step "APPE append to hello.txt and verify"
printf ' world' >"$TMPDIR/append.txt"
curl -sS "${AUTH[@]}" --append -T "$TMPDIR/append.txt" "$FTP_URL/testdir/hello.txt" >/dev/null
curl -sS "${AUTH[@]}" "$FTP_URL/testdir/hello.txt" -o "$TMPDIR/dl2.txt" >/dev/null
printf 'hello world' >"$TMPDIR/expected.txt"
cmp "$TMPDIR/expected.txt" "$TMPDIR/dl2.txt"

step "STOR large.bin then RETR resume with -C -"
dd if=/dev/urandom of="$TMPDIR/large.bin" bs=1024 count=128 status=none
curl -sS "${AUTH[@]}" -T "$TMPDIR/large.bin" "$FTP_URL/testdir/large.bin" >/dev/null
# seed partial file (first half)
head -c 65536 "$TMPDIR/large.bin" >"$TMPDIR/large.dl"
curl -sS "${AUTH[@]}" -C - "$FTP_URL/testdir/large.bin" -o "$TMPDIR/large.dl" >/dev/null
cmp "$TMPDIR/large.bin" "$TMPDIR/large.dl"

step "SIZE returns value and DELE works"
SIZE=$(curl -sS "${AUTH[@]}" "$FTP_URL/" --quote "CWD testdir" --quote "SIZE large.bin" 2>&1 | awk '/^< 213/ {print $3}')
test -n "$SIZE" && [[ "$SIZE" -gt 0 ]]
curl -sS "${AUTH[@]}" "$FTP_URL/" --quote "DELE testdir/hello.txt" >/dev/null
# Verify the deleted file is not retrievable, without noisy error logs
if curl -sSf "${AUTH[@]}" "$FTP_URL/testdir/hello.txt" -o /dev/null 2>/dev/null; then
  echo "[error] Expected missing file, but retrieval succeeded" >&2
  exit 1
fi

echo "[ok] All cURL integration tests passed"
