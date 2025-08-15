# FtpServer

A small, RFC-compliant (work-in-progress) FTP server in C# with pluggable storage and authentication.

- Plugins: storage and authentication via interfaces and a simple registry.
- Config: Microsoft.Extensions.Configuration (env vars prefix `FTP_`) and command-line overrides.
- Tests: xUnit with coverage enforced in CI (>= 80%).
- Docker: Container image via multi-stage Dockerfile.
- Versioning: GitVersion in CI.

## Run locally

```bash
# build and run
dotnet build FtpServer.sln && dotnet run --project FtpServer.App -- FtpServer:Port=2121
```

## Environment variables

Use `FTP_` prefix with double underscore for section separator, e.g.:
- `FTP_FtpServer__Port=2121`
- `FTP_FtpServer__MaxSessions=200`
- `FTP_FtpServer__Authenticator=InMemory`
- `FTP_FtpServer__StorageProvider=InMemory`

## Docker

```bash
# build image
docker build -t local/ftpserver:dev .
# run container
docker run --rm -p 21:21 -e FTP_FtpServer__Port=21 local/ftpserver:dev
```
