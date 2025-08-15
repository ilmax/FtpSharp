# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 as build
WORKDIR /src
COPY FtpServer.sln ./
COPY FtpServer.Core/FtpServer.Core.csproj FtpServer.Core/
COPY FtpServer.App/FtpServer.App.csproj FtpServer.App/
COPY FtpServer.Tests/FtpServer.Tests.csproj FtpServer.Tests/
RUN dotnet restore FtpServer.sln
COPY . .
RUN dotnet publish FtpServer.App/FtpServer.App.csproj -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:9.0 as final
WORKDIR /app
COPY --from=build /app/publish .
# default environment configuration via env
ENV FTP_FtpServer__ListenAddress=0.0.0.0 \
    FTP_FtpServer__Port=21 \
    FTP_FtpServer__MaxSessions=100 \
    FTP_FtpServer__Authenticator=InMemory \
    FTP_FtpServer__StorageProvider=InMemory
EXPOSE 21/tcp
ENTRYPOINT ["dotnet", "FtpServer.App.dll"]
