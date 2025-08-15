# Development Progress Log

- 2025-08-15: Added MODE, STRU, and ALLO command handlers (RFC 959 basics). Registered them in session dispatch and advertised MODE/STRU in FEAT. Added unit tests for the new handlers. This file will track future changes to avoid verbose chat summaries.
- 2025-08-15: Hardened thread-safety: made InMemoryStorageProvider operations atomic with a lock, snapshot reads to avoid races. Made FtpServerHost session tracking concurrent-safe. Added a basic concurrency test for parallel sessions.
