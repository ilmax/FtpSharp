# Development Progress Log

- 2025-08-15: Added MODE, STRU, and ALLO command handlers (RFC 959 basics). Registered them in session dispatch and advertised MODE/STRU in FEAT. Added unit tests for the new handlers. This file will track future changes to avoid verbose chat summaries.
- 2025-08-15: Hardened thread-safety: made InMemoryStorageProvider operations atomic with a lock, snapshot reads to avoid races. Made FtpServerHost session tracking concurrent-safe. Added a basic concurrency test for parallel sessions.
- 2025-08-15: Added parallel data transfer tests for STOR and RETR over passive mode to validate multi-session throughput and storage thread-safety.
- 2025-08-15: Added mixed parallel RETR/STOR tests and introduced data open/transfer timeouts via options. Created ENHANCEMENTS.md to track future features.
- 2025-08-15: Implemented control channel read timeout (idle command timeout) via ControlReadTimeoutMs option and applied in FtpSession loop; added a test to validate session closure when idle.
- 2025-08-15: Updated FEAT handler comments to reflect configurable timeouts (features list unchanged).
