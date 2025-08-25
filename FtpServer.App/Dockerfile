# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY FtpServer.sln ./
# Include central package and build props for proper restore
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY FtpServer.Core/FtpServer.Core.csproj FtpServer.Core/
COPY FtpServer.App/FtpServer.App.csproj FtpServer.App/
COPY FtpServer.Tests/FtpServer.Tests.csproj FtpServer.Tests/
COPY FtpServer.Storage.AzureBlob/FtpServer.Storage.AzureBlob.csproj FtpServer.Storage.AzureBlob/
RUN dotnet restore FtpServer.sln
COPY . .
RUN dotnet publish FtpServer.App/FtpServer.App.csproj -c Release -o /app/publish --no-restore

# Runtime image (ASP.NET Core runtime required for web hosting)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Default environment configuration:
# - Bind FTP control to 0.0.0.0:21
# - Bind ASP.NET Core to 0.0.0.0:8080 for metrics (/metrics) and optional health endpoints
ENV FTP_FtpServer__ListenAddress=0.0.0.0 \
    FTP_FtpServer__Port=21 \
    FTP_FtpServer__MaxSessions=100 \
    FTP_FtpServer__Authenticator=InMemory \
    FTP_FtpServer__StorageProvider=InMemory \
    FTP_FtpServer__PassivePortRangeStart=49152 \
    FTP_FtpServer__PassivePortRangeEnd=49200 \
    ASPNETCORE_URLS=http://0.0.0.0:8080

# Expose FTP control and web ports; publish passive data range at runtime as needed, e.g., -p 50000-50050:50000-50050
EXPOSE 21/tcp 8080/tcp
ENTRYPOINT ["dotnet", "FtpServer.App.dll"]
