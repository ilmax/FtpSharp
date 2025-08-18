# Enhancements and Ideas

This document tracks possible improvements and future features for the FTP server.

## Protocol Features

- ✅ FTPS (AUTH TLS, PBSZ, PROT, explicit/implicit modes) with cert management.
- ✅ Resume/offset: REST and APPE support; integrate with FEAT.
- MDTM (modification time) and MFMT; MLST/MLSD structured listings.
- SITE commands (e.g., CHMOD), CHOWN/CHGRP (if provider supports permissions).
- HASH/XCRC/XMD5 (non-standard) for integrity checks.
- OPTS UTF8 ON/OFF and advanced FEAT negotiation.

## Data Channel and Performance

- ✅ Per-path reader/writer locks to serialize same-file writers and allow concurrent readers.
- ✅ Throttling/rate limits per-transfer; configurable. (Shared/global limiter pending)
- Zero-copy streaming where possible (Pipeline or MemoryPool usage).
- Pipelining support and command queueing (where safe) to reduce RTT.

## Concurrency and Robustness

- ✅ Timeouts for control commands (idle) and data open/transfer timeouts.
- Back-pressure and cancellation propagation improvements for slow clients.
- ✅ Passive port pool with leasing to avoid linear scan; randomization to reduce collisions.
- Session-level metrics: current transfers, bytes sent/received, timing.

## Authentication and Authorization

- Pluggable auth providers: Basic over config, OAuth2 introspection, PAM/LDAP.
- Per-user roots (chroot), virtual users, and permissions/ACLs.
- Login attempt rate limiting and temporary bans.

## Storage Providers

- Local filesystem: advanced options (symlinks, permissions mapping, disk quotas).
- Cloud backends: Azure Blob, S3-compatible, Google Cloud Storage.
- Object-store semantics: eventual consistency handling and ETags for resume.

## Observability

- ✅ Structured logs (basic) and metrics: sessions active, commands count and duration, transfer bytes, errors.
- ✅ Health endpoint (ASP.NET Core) with /health and /metrics-snapshot.

## Tooling and DX

- CLI: richer help, subcommands, config export, dry-run validation.
- Config hot-reload and dynamic plugin loading.
- Test harness for stress (soak tests), chaos testing for network faults.

## Security

- FTPS hardening, cipher suites, HSTS for implicit TLS, certificate rotation.
- Chroot jail enforcement and directory traversal hardening (already normalized).
- Secret management via environment or Azure Key Vault.

## CI/CD

- Integration tests against common FTP clients (curl, lftp) in CI.
- Docker multi-arch, image scanning, SBOM.

## Nice-to-have

- Internationalization of replies where applicable.
- Friendly banner and message customization per user or time.
