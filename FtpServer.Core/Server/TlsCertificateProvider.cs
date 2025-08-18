using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FtpServer.Core.Configuration;
using Microsoft.Extensions.Options;

namespace FtpServer.Core.Server;

public sealed class TlsCertificateProvider
{
    private X509Certificate2? _cached;
    public X509Certificate2 GetOrCreate(IOptions<FtpServerOptions> options)
    {
        if (_cached is not null) return _cached;
        var opt = options.Value;
        if (!string.IsNullOrWhiteSpace(opt.TlsCertPath) && File.Exists(opt.TlsCertPath))
        {
            _cached = string.IsNullOrEmpty(opt.TlsCertPassword)
                ? X509CertificateLoader.LoadPkcs12FromFile(opt.TlsCertPath!, ReadOnlySpan<char>.Empty)
                : X509CertificateLoader.LoadPkcs12FromFile(opt.TlsCertPath!, opt.TlsCertPassword.AsSpan());
            return _cached;
        }
        if (!opt.TlsSelfSigned)
            throw new InvalidOperationException("TLS certificate not configured and self-signed generation disabled");
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=FtpSharp", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(System.Net.IPAddress.Loopback);
        req.CertificateExtensions.Add(san.Build());
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        _cached = cert;
        return _cached;
    }
}
