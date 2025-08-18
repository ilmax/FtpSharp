# Copilot instructions for this repository (FtpSharp)

These instructions guide Copilot to make high-quality, repo-aligned changes. Keep answers concise, practical, and code-focused.

## Overview
- Project: RFC-minded FTP/FTPS server in C# (.NET 9) with plugin-based storage/auth, CI, Docker.
- Key features: AUTH TLS (explicit/implicit), PBSZ/PROT, REST/APPE, passive/active data, per-path locks, throttling, health/metrics.
- Code style: follow existing patterns; small, targeted diffs; preserve public APIs; warnings-as-errors are enabled.

## Architecture quick map
- App: `FtpServer.App` (bootstrap/CLI). Options bound via System.CommandLine/env (prefix `FTP_`).
- Core: `FtpServer.Core`
  - Server: `FtpServerHost`, `FtpSession`, command handlers in `Server/Commands/Handlers/*`.
  - Storage: `IStorageProvider` with implementations `FileSystem/` and `InMemory/`.
  - Auth: `Basic` and `InMemory` via `PluginRegistry`.
  - Networking: `PassivePortPool`, PASV/EPSV logic; FTPS via `SslStream`.
  - Concurrency: `PathLocks` (per-path RW), `Throttle`/`ByteRateLimiter`.
- Tests: `FtpServer.Tests` (xUnit) include concurrency, storage, host, active/passive, health.
- Scripts/CI: GitHub Actions runs unit + integration (curl script), coverage enforced.

## Critical invariants and patterns
- NAT-friendly passive mode:
  - Bind passive data listeners to `IPAddress.Any`.
  - Advertise PASV IP using `PassivePublicIp` when set, else derive from control socket.
- Data transfers:
  - Use zero-copy where safe: prefer `ReadOnlyMemory<byte>` slices; avoid `ToArray()`.
  - ASCII mode must normalize line endings ("\n" vs "\r\n").
  - Respect per-transfer throttling and timeouts from `FtpServerOptions`.
- Storage providers:
  - `Read*` may yield reusable buffers; consumers must process before next MoveNext.
  - Honor REST offset for RETR/STOR/APPE; appends and truncation via provider APIs.
  - Enforce root path normalization; prevent path escape.
- Thread-safety:
  - Acquire `PathLocks` for the target path for RETR (read) and STOR/APPE (write).
  - Allow concurrent readers; serialize writers.
- Public behavior:
  - Do not change command replies/status codes without tests.
  - Keep FEAT list consistent with implemented commands.

## When implementing features
- Add/modify command handlers under `Server/Commands/Handlers/*`.
- Wire handlers in `FtpSession` command map.
- Extend `FtpServerOptions` for new tunables and bind via CLI/env (prefix `FTP_`).
- Update FEAT and HELP outputs where relevant.
- Add unit tests (xUnit). For protocol flows, prefer focused handler tests and server-host tests.

## Performance and streaming
- Prefer streaming through `IAsyncEnumerable<ReadOnlyMemory<byte>>`.
- Use `Stream.WriteAsync(ReadOnlyMemory<byte>)` overloads.
- Consider `System.IO.Pipelines` for further data path work, but keep changes incremental.

## CI, tests, and integration
- Keep build green: warnings-as-errors.
- Maintain coverage (>= 80%). Add tests alongside public behavior changes.
- Integration: scripts/curl_integration_tests.sh can target dockerized server (`EXTERNAL_SERVER=true`).
  - Default docker run uses PASV port range and `PassivePublicIp=127.0.0.1`.
  - In CI external mode, active/Python tests may be skipped.

## Coding conventions
- Follow `.editorconfig` settings.
- Keep diffs minimal; avoid reformatting unrelated code.
- Favor explicit names, early returns, and cancellation propagation.

## Do and don’t
- Do: small PRs, tests first for public changes, update `ENHANCEMENTS.md` and `PROGRESS.md` when applicable.
- Do: document new options/envs in README.
- Don’t: break PASV/NAT behavior; don’t block on long operations without cancellation.
- Don’t: allocate per-chunk buffers unnecessarily; avoid `ToArray()` in hot paths.

## How to run locally
- Build and test: `dotnet build`, `dotnet test`.
- App run: use CLI flags or `FTP_` envs; map control port and PASV range if in Docker.

## Security
- FTPS defaults must be safe; support TLS 1.2/1.3.
- Handle certificates via config (PFX path/secret) or generate self-signed for dev.
- Never log secrets.

## Review checklist for changes Copilot makes
- Protocol correctness (replies, states, FEAT/HELP).
- Zero-copy preserved; ASCII mode handled.
- Locks and throttling applied; timeouts wired.
- Tests updated/added and passing; CI-friendly.
- Docs updated where appropriate.
