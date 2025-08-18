namespace FtpServer.Core.Protocol;

/// <summary>
/// Parses raw FTP control lines into command + argument.
/// </summary>
public static class FtpCommandParser
{
    public static ParsedCommand Parse(string line)
    {
        line = line?.Trim() ?? string.Empty;
        if (line.Length == 0) return new ParsedCommand(string.Empty, string.Empty);
        int idx = line.IndexOf(' ');
        if (idx < 0) return new ParsedCommand(line.ToUpperInvariant(), string.Empty);
        return new ParsedCommand(line[..idx].ToUpperInvariant(), line[(idx + 1)..]);
    }
}

public sealed record ParsedCommand(string Command, string Argument);
