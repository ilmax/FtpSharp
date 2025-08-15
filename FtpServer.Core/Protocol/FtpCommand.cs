namespace FtpServer.Core.Protocol;

/// <summary>
/// RFC 959 core commands (subset). Extend incrementally.
/// </summary>
public enum FtpCommand
{
    USER,
    PASS,
    SYST,
    FEAT,
    PWD,
    CWD,
    TYPE,
    PASV,
    LIST,
    RETR,
    STOR,
    DELE,
    MKD,
    RMD,
    QUIT
}
