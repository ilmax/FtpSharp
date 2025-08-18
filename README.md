# FtpServer

An RFC-oriented FTP/FTPS server in C# with pluggable storage and authentication.

- Plugins: storage and authentication via interfaces and a simple registry.
	- Authenticators: InMemory (default), Basic (from config via FtpServer:Users)
- Config: Microsoft.Extensions.Configuration (env vars prefix `FTP_`) and rich command-line flags.
- FTPS: Explicit (AUTH TLS) and optional implicit TLS with PBSZ/PROT and certificate management.
- Observability: Health endpoints (optional) and Prometheus metrics.
- Tests: xUnit plus cURL/Python ftplib integration; coverage enforced in CI (>= 80%).
- Docker: Multi-stage build producing a slim ASP.NET runtime image.

## Quick start

```bash
# build
dotnet build FtpServer.sln

# run with defaults (in-memory auth/storage), FTP on 2121
dotnet run --project FtpServer.App -- --port 2121 --storage InMemory --auth InMemory
```

Connect with any FTP client to 127.0.0.1:2121 (anonymous allowed by default when using InMemory authenticator).

## Command-line options

All server options are available as flags:

- Networking: `--listen`, `--port`, `--max-sessions`, `--pasv-start`, `--pasv-end`
- Timeouts/limits: `--data-open-timeout`, `--data-transfer-timeout`, `--control-read-timeout`, `--rate-limit`
- Plugins/storage: `--auth`, `--storage`, `--storage-root`
- FTPS/TLS: `--ftps-explicit`, `--ftps-implicit`, `--ftps-implicit-port`, `--tls-cert`, `--tls-cert-pass`, `--tls-self-signed`
- Health/metrics: `--health`, `--health-url` (Prometheus metrics are always exposed)

Example (explicit FTPS with self-signed certificate and passive range):

```bash
dotnet run --project FtpServer.App -- \
	--port 2121 \
	--ftps-explicit true \
	--tls-self-signed true \
	--storage FileSystem \
	--storage-root ./data \
	--pasv-start 50000 --pasv-end 50050
```

Client example (curl, explicit FTPS):

```bash
curl --ftp-ssl-reqd --insecure -u anonymous: ftp://127.0.0.1:2121/ --quote "PBSZ 0" --quote "PROT P"
```

## FTPS and certificates

- Explicit FTPS upgrades the control channel via `AUTH TLS` and supports `PBSZ 0` and `PROT C|P` for data channels.
- Implicit FTPS can be enabled on a dedicated port using `--ftps-implicit true --ftps-implicit-port <port>`.
- Certificates:
	- Provide a PFX via `--tls-cert /path/server.pfx` and `--tls-cert-pass <password>`, or
	- Let the server generate a self-signed cert via `--tls-self-signed true` (good for local testing).

## Environment variables

All options can be set via environment variables (prefix `FTP_` and `__` section separator). Examples:

- `FTP_FtpServer__ListenAddress=0.0.0.0`
- `FTP_FtpServer__Port=2121`
- `FTP_FtpServer__MaxSessions=100`
- `FTP_FtpServer__PassivePortRangeStart=50000`
- `FTP_FtpServer__PassivePortRangeEnd=50050`
- `FTP_FtpServer__Authenticator=InMemory`
	- To use Basic from config: `FTP_FtpServer__Authenticator=Basic` and set users like `FTP_FtpServer__Users__alice=secret`
- `FTP_FtpServer__StorageProvider=FileSystem`
- `FTP_FtpServer__StorageRoot=/data`
- `FTP_FtpServer__FtpsExplicitEnabled=true`
- `FTP_FtpServer__FtpsImplicitEnabled=false`
- `FTP_FtpServer__FtpsImplicitPort=990`
- `FTP_FtpServer__TlsCertPath=/certs/server.pfx`
- `FTP_FtpServer__TlsCertPassword=...`
- `FTP_FtpServer__TlsSelfSigned=true`

## Health and metrics

- Health (if enabled): `/health`
- Prometheus metrics: `/metrics`

By default in Docker the ASP.NET endpoint is bound to `0.0.0.0:8080` (see Dockerfile). You can override via `ASPNETCORE_URLS`.

## Integration tests

- `scripts/curl_integration_tests.sh` runs end-to-end protocol checks (including explicit FTPS path).
- `scripts/ftplib_tests.py` runs basic passive and active transfers via Python’s `ftplib`.

## Docker

```bash
# build image
docker build -t local/ftpserver:dev .

# run with passive ports and explicit FTPS enabled (self-signed)
docker run --rm \
	-p 21:21 -p 50000-50050:50000-50050 \
	-p 8080:8080 \
	-e FTP_FtpServer__Port=21 \
	-e FTP_FtpServer__PassivePortRangeStart=50000 \
	-e FTP_FtpServer__PassivePortRangeEnd=50050 \
	-e FTP_FtpServer__FtpsExplicitEnabled=true \
	-e FTP_FtpServer__TlsSelfSigned=true \
	local/ftpserver:dev
```

To persist files, mount a volume and use the FileSystem provider:

```bash
docker run --rm \
	-v "$PWD/data":/data \
	-e FTP_FtpServer__StorageProvider=FileSystem \
	-e FTP_FtpServer__StorageRoot=/data \
	-p 21:21 -p 50000-50050:50000-50050 \
	local/ftpserver:dev
```
