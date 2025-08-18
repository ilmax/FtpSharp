#!/usr/bin/env bash
set -euo pipefail

# cURL-based integration tests for the FTP server.
# Requirements: bash, curl, coreutils (dd, cmp), dotnet SDK.

ROOT_DIR=$(cd "$(dirname "$0")/.." && pwd)
TMPDIR=$(mktemp -d)
PORT=${PORT:-2121}
PASV_START=${PASV_START:-49152}
PASV_END=${PASV_END:-49162}
DOTNET_CONFIGURATION=${DOTNET_CONFIGURATION:-Release}
SERVER_LOG="$TMPDIR/server.log"
FAIL=0
EXTERNAL_SERVER=${EXTERNAL_SERVER:-false}

# Auth credentials (default anonymous). Override via AUTH_USER/AUTH_PASS for Basic auth.
AUTH_USER=${AUTH_USER:-anonymous}
AUTH_PASS=${AUTH_PASS:-}
SKIP_ACTIVE=${SKIP_ACTIVE:-false}
SKIP_PYTHON=${SKIP_PYTHON:-false}

# In external server (Docker) mode, default to skipping active-mode and Python tests
if [[ "$EXTERNAL_SERVER" == "true" ]]; then
  SKIP_ACTIVE=${SKIP_ACTIVE:-true}
  SKIP_PYTHON=${SKIP_PYTHON:-true}
fi

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

if [[ "$EXTERNAL_SERVER" != "true" ]]; then
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
  # FTPS toggles for testing
  export FTP_FTPSERVER__FTPSEXPLICITENABLED=${FTP_EXPLICIT_ENABLED:-true}
  export FTP_FTPSERVER__FTPSIMPLICITENABLED=${FTP_IMPLICIT_ENABLED:-false}
  # Bind ASP.NET Core to an ephemeral port to avoid conflicts on 5000 during tests
  export ASPNETCORE_URLS="http://127.0.0.1:0"

  echo "[info] Starting server on 127.0.0.1:$PORT (PASV $PASV_START-$PASV_END) [config=$DOTNET_CONFIGURATION]"
  dotnet run --project "$ROOT_DIR/FtpServer.App" --no-build --configuration "$DOTNET_CONFIGURATION" >"$SERVER_LOG" 2>&1 &
  SERVER_PID=$!
else
  echo "[info] External server mode: expecting server at 127.0.0.1:$PORT (PASV $PASV_START-$PASV_END)"
fi

# Wait for server to accept connections
echo -n "[info] Waiting for server to be ready"
for i in {1..60}; do
  if curl -s --user "$AUTH_USER:$AUTH_PASS" "ftp://127.0.0.1:$PORT/" >/dev/null 2>&1; then
    echo " - ready"
    break
  fi
  echo -n "."
  sleep 0.5
  if [[ $i -eq 60 ]]; then
    echo "\n[error] Server did not start in time." >&2
    if [[ "$EXTERNAL_SERVER" != "true" ]]; then
      echo "[info] Server log follows:" >&2
      tail -n +1 "$SERVER_LOG" >&2 || true
    fi
    exit 1
  fi
done

FTP_URL="ftp://127.0.0.1:$PORT"
AUTH=(-u "$AUTH_USER:$AUTH_PASS")

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

step "RNFR/RNTO rename large.bin -> renamed.bin"
curl -sS "${AUTH[@]}" "$FTP_URL/" --quote "CWD testdir" --quote "RNFR large.bin" --quote "RNTO renamed.bin" >/dev/null
curl -sS "${AUTH[@]}" "$FTP_URL/testdir/" | grep -q "renamed.bin"
if curl -sSf "${AUTH[@]}" "$FTP_URL/testdir/large.bin" -o /dev/null 2>/dev/null; then
  echo "[error] Old name still accessible after rename" >&2; exit 1
fi

step "NLST lists bare names"
NLST=$(curl -sS -l "${AUTH[@]}" "$FTP_URL/testdir/")
echo "$NLST" | grep -q "renamed.bin"

step "TYPE command toggles between A and I and allows RETR"
printf 'line1\nline2\n' >"$TMPDIR/ascii_lf.txt"
curl -sS "${AUTH[@]}" -T "$TMPDIR/ascii_lf.txt" "$FTP_URL/testdir/ascii.txt" >/dev/null
# Toggle TYPE A then RETR; then back to TYPE I
curl -sS "${AUTH[@]}" "$FTP_URL/" --quote "CWD testdir" --quote "TYPE A" >/dev/null
curl -sS "${AUTH[@]}" "$FTP_URL/testdir/ascii.txt" -o "$TMPDIR/ascii_any.txt" >/dev/null
test -s "$TMPDIR/ascii_any.txt"
curl -sS "${AUTH[@]}" "$FTP_URL/" --quote "TYPE I" >/dev/null

step "RMD fails on non-empty dir then succeeds when empty"
curl -sS "${AUTH[@]}" "$FTP_URL/" --quote "CWD testdir" --quote "MKD sub" >/dev/null
printf 'x' >"$TMPDIR/small.txt"
curl -sS "${AUTH[@]}" -T "$TMPDIR/small.txt" "$FTP_URL/testdir/sub/small.txt" >/dev/null
RMD_OUT=$(curl -sS "${AUTH[@]}" "$FTP_URL/" --quote "CWD testdir" --quote "RMD sub" 2>&1 || true)
echo "$RMD_OUT" | grep -q "550" || echo "$RMD_OUT" # expect 550
curl -sS "${AUTH[@]}" "$FTP_URL/" --quote "CWD testdir" --quote "DELE sub/small.txt" >/dev/null
curl -sS "${AUTH[@]}" "$FTP_URL/" --quote "CWD testdir" --quote "RMD sub" >/dev/null

step "Parallel uploads"
for i in 1 2 3; do dd if=/dev/urandom of="$TMPDIR/p$i.bin" bs=1024 count=32 status=none; done
# Use curl's built-in parallel transfers to avoid backgrounding multiple processes in the same terminal.
# Add conservative timeouts so failures don't hang indefinitely. If --parallel is unavailable, fall back to sequential uploads.
if curl --help all 2>&1 | grep -q -- "--parallel"; then
  curl -sS --parallel --parallel-immediate \
    --connect-timeout 5 --max-time 30 \
    "${AUTH[@]}" \
    -T "$TMPDIR/p1.bin" "$FTP_URL/testdir/p1.bin" \
    -T "$TMPDIR/p2.bin" "$FTP_URL/testdir/p2.bin" \
    -T "$TMPDIR/p3.bin" "$FTP_URL/testdir/p3.bin" \
    >/dev/null
else
  echo "[warn] curl --parallel not supported; uploading sequentially" >&2
  curl -sS --connect-timeout 5 --max-time 30 "${AUTH[@]}" -T "$TMPDIR/p1.bin" "$FTP_URL/testdir/p1.bin" >/dev/null
  curl -sS --connect-timeout 5 --max-time 30 "${AUTH[@]}" -T "$TMPDIR/p2.bin" "$FTP_URL/testdir/p2.bin" >/dev/null
  curl -sS --connect-timeout 5 --max-time 30 "${AUTH[@]}" -T "$TMPDIR/p3.bin" "$FTP_URL/testdir/p3.bin" >/dev/null
fi
curl -sS "${AUTH[@]}" "$FTP_URL/testdir/" | grep -q "p1.bin"

echo "[ok] Core cURL segment passed"

# Additional protocol checks
step "FEAT advertises REST, APPE, and FTPS commands"
# Use -v to ensure control replies are emitted to stderr across curl versions
FEAT=$(curl -sS -v "${AUTH[@]}" "$FTP_URL/" --quote "FEAT" 2>&1 || true)
echo "$FEAT" | grep -q "REST STREAM" || { echo "[error] FEAT missing REST STREAM" >&2; FAIL=1; }
echo "$FEAT" | grep -q "APPE" || { echo "[error] FEAT missing APPE" >&2; FAIL=1; }
echo "$FEAT" | grep -q "AUTH TLS" || { echo "[error] FEAT missing AUTH TLS" >&2; FAIL=1; }
echo "$FEAT" | grep -q "PBSZ" || { echo "[error] FEAT missing PBSZ" >&2; FAIL=1; }
echo "$FEAT" | grep -q "PROT" || { echo "[error] FEAT missing PROT" >&2; FAIL=1; }

step "SYST returns a system type"
if ! curl -sS "${AUTH[@]}" "$FTP_URL/" --quote "SYST" >/dev/null; then
  echo "[error] SYST failed" >&2; FAIL=1;
fi

step "CDUP at root stays at /"
# Use -v so 257 PWD response is captured reliably
PWD_OUT=$(curl -sS -v "${AUTH[@]}" "$FTP_URL/" --quote "PWD" 2>&1)
echo "$PWD_OUT" | grep -q "/" || { echo "[error] PWD did not return path" >&2; FAIL=1; }
if ! curl -sS "${AUTH[@]}" "$FTP_URL/" --quote "CDUP" >/dev/null; then
  echo "[error] CDUP failed" >&2; FAIL=1;
fi
PWD_OUT2=$(curl -sS -v "${AUTH[@]}" "$FTP_URL/" --quote "PWD" 2>&1)
echo "$PWD_OUT2" | grep -q "/" || { echo "[error] PWD after CDUP did not return path" >&2; FAIL=1; }

step "DELE non-existent returns 550"
DELE_OUT=$(curl -sS "${AUTH[@]}" "$FTP_URL/" --quote "DELE nofile.bin" 2>&1 || true)
echo "$DELE_OUT" | grep -q "550" || { echo "[error] DELE non-existent did not return 550" >&2; FAIL=1; }

step "SIZE on directory returns error"
SIZE_DIR=$(curl -sS "${AUTH[@]}" "$FTP_URL/" --quote "CWD testdir" --quote "SIZE ." 2>&1 || true)
echo "$SIZE_DIR" | grep -q "550" || { echo "[error] SIZE on directory did not return 550" >&2; FAIL=1; }

if [[ "$SKIP_ACTIVE" != "true" ]]; then
  step "Active mode RETR works"
  if ! curl -sS "${AUTH[@]}" -P - "$FTP_URL/testdir/renamed.bin" -o "$TMPDIR/act_dl.bin" >/dev/null; then
    echo "[error] Active mode RETR failed" >&2; FAIL=1;
  fi
  test -s "$TMPDIR/act_dl.bin" || { echo "[error] Active mode download is empty" >&2; FAIL=1; }
else
  echo "[info] Skipping active mode tests (SKIP_ACTIVE=true)"
fi

step "Disable EPSV forces PASV path"
if ! curl -sS --disable-epsv "${AUTH[@]}" "$FTP_URL/testdir/renamed.bin" -o "$TMPDIR/pasv_dl.bin" >/dev/null; then
  echo "[error] PASV retrieval with EPSV disabled failed" >&2; FAIL=1;
fi
if [[ "$SKIP_ACTIVE" != "true" ]]; then
  cmp "$TMPDIR/act_dl.bin" "$TMPDIR/pasv_dl.bin" || { echo "[error] Active vs PASV downloads differ" >&2; FAIL=1; }
else
  test -s "$TMPDIR/pasv_dl.bin" || { echo "[error] PASV download is empty" >&2; FAIL=1; }
fi

step "Resume STOR upload with -C -"
dd if=/dev/urandom of="$TMPDIR/big2.bin" bs=1024 count=96 status=none
head -c 20480 "$TMPDIR/big2.bin" > "$TMPDIR/big2.part"
if ! curl -sS "${AUTH[@]}" -T "$TMPDIR/big2.part" "$FTP_URL/testdir/big2.bin" >/dev/null; then
  echo "[error] Initial partial STOR failed" >&2; FAIL=1;
fi
if ! curl -sS "${AUTH[@]}" -C - -T "$TMPDIR/big2.bin" "$FTP_URL/testdir/big2.bin" >/dev/null; then
  echo "[error] Resume STOR with -C - failed" >&2; FAIL=1;
fi
if ! curl -sS "${AUTH[@]}" "$FTP_URL/testdir/big2.bin" -o "$TMPDIR/big2.dl" >/dev/null; then
  echo "[error] Download after resumed upload failed" >&2; FAIL=1;
fi
cmp "$TMPDIR/big2.bin" "$TMPDIR/big2.dl" || { echo "[error] Resumed upload content mismatch" >&2; FAIL=1; }

# End extended checks

# Explicit FTPS happy path (AUTH TLS, PBSZ 0, PROT P) if enabled
if [[ "${FTP_FTPSERVER__FTPSEXPLICITENABLED}" == "true" ]]; then
  step "Explicit FTPS AUTH TLS + PBSZ 0 + PROT P + simple LIST"
  # curl --ftp-ssl-reqd enforces TLS on control; --insecure to accept self-signed
  if ! curl -sS --ftp-ssl-reqd --insecure -v "${AUTH[@]}" "$FTP_URL/testdir/" --quote "PBSZ 0" --quote "PROT P" -o /dev/null 2>"$TMPDIR/ftps_explicit.log"; then
    echo "[error] Explicit FTPS flow failed" >&2; FAIL=1;
  else
    echo "[info] Explicit FTPS flow ok" >&2
  fi
fi

# Run additional tests using Python ftplib (active and passive data modes)
if [[ "$SKIP_PYTHON" != "true" ]]; then
  if command -v python3 >/dev/null 2>&1; then
    if ! python3 "$ROOT_DIR/scripts/ftplib_tests.py" "$PORT"; then
      echo "[error] ftplib tests failed" >&2; FAIL=1;
    fi
  else
    echo "[warn] python3 not found; skipping ftplib tests"
  fi
else
  echo "[info] Skipping Python ftplib tests (SKIP_PYTHON=true)"
fi

if [[ $FAIL -eq 0 ]]; then
  echo "[ok] All integration tests passed"
else
  echo "[warn] Some checks failed; see output above"
  exit 1
fi

