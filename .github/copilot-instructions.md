# GitHub Copilot Instructions for FtpSharp Repository

ALWAYS follow these instructions first. Only search for additional context or run bash commands if the information here is incomplete or found to be in error.

## Project Overview
FtpSharp is an RFC-oriented FTP/FTPS server in C# (.NET 9) with pluggable storage and authentication.

Key features: AUTH TLS (explicit/implicit), PBSZ/PROT, REST/APPE, passive/active data transfers, per-path locks, throttling, health endpoints, and Prometheus metrics.

## Prerequisites and Environment Setup

### Install .NET 9 SDK (REQUIRED)
The project requires .NET 9 SDK. If you encounter NETSDK1045 errors, install .NET 9:

```bash
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --version 9.0.100 --install-dir $HOME/.dotnet
export PATH="$HOME/.dotnet:$PATH"
dotnet --version  # Should show 9.0.100 or later
```

## Build and Test Commands (VALIDATED)

### Package Restore
```bash
dotnet restore FtpServer.sln
```
**Timing**: ~8 seconds. **NEVER CANCEL** - Set timeout to 300+ seconds.

### Build
```bash
dotnet build FtpServer.sln --configuration Release --no-restore
```
**Timing**: ~8 seconds. **NEVER CANCEL** - Set timeout to 300+ seconds.

### Unit Tests  
```bash
dotnet test FtpServer.sln --configuration Release --no-build --verbosity normal
```
**Timing**: ~7 seconds, 69 tests. **NEVER CANCEL** - Set timeout to 300+ seconds.

### Integration Tests
```bash
chmod +x scripts/curl_integration_tests.sh
scripts/curl_integration_tests.sh
```
**Timing**: ~4 seconds. Runs comprehensive FTP protocol tests with curl.

```bash
python3 scripts/ftplib_tests.py 2121
```
Requires a running FTP server on port 2121. Tests passive and active transfers via Python's ftplib.

## Running the Application

### Start FTP Server (Validated)
```bash
dotnet run --project FtpServer.App -- --port 2121 --storage InMemory --auth InMemory
```

Expected output:
```
info: FtpServer.Core.Server.FtpServerHost[0]
      FTP server listening on 0.0.0.0:2121
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

Connect with any FTP client to 127.0.0.1:2121 (anonymous login allowed with InMemory auth).

### With FileSystem Storage
```bash
dotnet run --project FtpServer.App -- --port 2121 --storage FileSystem --storage-root ./data --auth InMemory
```

### With FTPS (Self-signed)
```bash
dotnet run --project FtpServer.App -- --port 2121 --ftps-explicit true --tls-self-signed true --storage InMemory --auth InMemory
```

## Docker (Known Limitations)

### Build Docker Image
```bash
docker build -t local/ftpserver:dev .
```
**WARNING**: Docker build fails in sandboxed environments due to certificate issues with NuGet restore. This is expected and does not work in CI/testing environments. Only works in environments with proper certificate chains.

### Run Docker Container (If build succeeds)
```bash
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

## Validation Scenarios (CRITICAL)

After making changes, ALWAYS test these complete scenarios:

### Basic FTP Client Test
1. Start the server: `dotnet run --project FtpServer.App -- --port 2121 --storage InMemory --auth InMemory`
2. Connect with curl: `curl ftp://anonymous:@127.0.0.1:2121/`
3. Upload a file: `echo "test content" | curl -T - ftp://anonymous:@127.0.0.1:2121/test.txt`
4. Download the file: `curl ftp://anonymous:@127.0.0.1:2121/test.txt`
5. Verify content matches: "test content"

### FTPS Test
1. Start with FTPS: `dotnet run --project FtpServer.App -- --port 2121 --ftps-explicit true --tls-self-signed true --storage InMemory --auth InMemory`
2. Test FTPS: `curl --ftp-ssl-reqd --insecure -u anonymous: ftp://127.0.0.1:2121/ --quote "PBSZ 0" --quote "PROT P"`

## Repository Structure

### Key Projects
- `FtpServer.App/` - Console application entry point with CLI configuration
- `FtpServer.Core/` - Main FTP server implementation
  - `Server/Commands/Handlers/` - FTP command implementations
  - `Configuration/` - Server options and settings
  - `InMemory/` and `FileSystem/` - Storage providers
- `FtpServer.Tests/` - xUnit test project (69 tests)

### Important Files
- `FtpServer.sln` - Main solution file
- `Directory.Build.props` - Treats warnings as errors (TreatWarningsAsErrors=true)
- `Directory.Packages.props` - Central package management
- `.github/workflows/ci.yml` - CI pipeline with coverage enforcement (>= 80%)
- `scripts/` - Integration test scripts

## Architecture and Patterns

### Command Handlers
Add/modify FTP command handlers in `Server/Commands/Handlers/*`. Wire handlers in `FtpSession` command map.

### Configuration
- All options available via CLI flags and environment variables (prefix `FTP_`)
- Extend `FtpServerOptions` for new tunables
- Bind via System.CommandLine and Microsoft.Extensions.Configuration

### Critical Invariants
- **NAT-friendly passive mode**: Bind to `IPAddress.Any`, advertise correct public IP
- **Zero-copy data transfers**: Use `ReadOnlyMemory<byte>`, avoid `ToArray()`
- **Thread-safety**: Use `PathLocks` for file operations
- **ASCII mode**: Normalize line endings ("\n" vs "\r\n")

## Before Committing Changes

Always run this complete validation sequence:

```bash
# 1. Restore and build (NEVER CANCEL - ~8s each)
dotnet restore FtpServer.sln
dotnet build FtpServer.sln --configuration Release --no-restore

# 2. Run unit tests (NEVER CANCEL - ~7s)
dotnet test FtpServer.sln --configuration Release --no-build

# 3. Run integration tests (NEVER CANCEL - ~4s)
chmod +x scripts/curl_integration_tests.sh
scripts/curl_integration_tests.sh

# 4. Test application startup
timeout 10s dotnet run --project FtpServer.App -- --port 2121 --storage InMemory --auth InMemory

# 5. Manual validation: Upload/download a file via curl
```

## Common Issues and Solutions

### Build Errors
- **NETSDK1045**: Install .NET 9 SDK (see Prerequisites section)
- **NU1301 in Docker**: Expected in sandboxed environments; document as limitation

### Test Failures
- Check port availability (2121 for tests)
- Ensure no other FTP servers running
- Run integration tests only after unit tests pass

### Performance Guidelines
- Prefer `IAsyncEnumerable<ReadOnlyMemory<byte>>` for streaming
- Use `Stream.WriteAsync(ReadOnlyMemory<byte>)` overloads
- Respect timeouts and cancellation tokens

## Security
- FTPS defaults must be safe (TLS 1.2/1.3)
- Never log secrets
- Self-signed certificates only for development/testing