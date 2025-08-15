namespace FtpServer.Core.Protocol;

/// <summary>
/// Named record for passive data connection endpoint.
/// </summary>
public sealed record PassiveEndpoint(string IpAddress, int Port)
{
    public override string ToString() => $"{IpAddress}:{Port}";
}
