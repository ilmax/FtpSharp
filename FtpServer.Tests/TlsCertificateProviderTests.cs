using FtpServer.Core.Configuration;
using FtpServer.Core.Server;
using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;

namespace FtpServer.Tests;

public class TlsCertificateProviderTests
{
    [Fact]
    public void GetOrCreate_WithValidCertificatePath_ShouldLoadCertificate()
    {
        // Arrange
        var tempCertPath = Path.GetTempFileName();
        try
        {
            // Create a simple self-signed certificate for testing
            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            var req = new CertificateRequest("CN=Test", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            
            // Export as PFX to temp file
            File.WriteAllBytes(tempCertPath, cert.Export(X509ContentType.Pfx));

            var options = Options.Create(new FtpServerOptions
            {
                TlsCertPath = tempCertPath,
                TlsCertPassword = null
            });

            var provider = new TlsCertificateProvider();

            // Act
            var result = provider.GetOrCreate(options);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test", result.Subject.Split('=')[1]);
        }
        finally
        {
            if (File.Exists(tempCertPath))
                File.Delete(tempCertPath);
        }
    }

    [Fact]
    public void GetOrCreate_WithCertificateAndPassword_ShouldLoadCertificate()
    {
        // Arrange
        var tempCertPath = Path.GetTempFileName();
        var password = "testpass123";
        
        try
        {
            // Create a password-protected certificate
            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            var req = new CertificateRequest("CN=TestWithPassword", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            
            // Export as password-protected PFX
            File.WriteAllBytes(tempCertPath, cert.Export(X509ContentType.Pfx, password));

            var options = Options.Create(new FtpServerOptions
            {
                TlsCertPath = tempCertPath,
                TlsCertPassword = password
            });

            var provider = new TlsCertificateProvider();

            // Act
            var result = provider.GetOrCreate(options);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestWithPassword", result.Subject.Split('=')[1]);
        }
        finally
        {
            if (File.Exists(tempCertPath))
                File.Delete(tempCertPath);
        }
    }

    [Fact]
    public void GetOrCreate_WithSelfSignedEnabled_ShouldCreateSelfSignedCertificate()
    {
        // Arrange
        var options = Options.Create(new FtpServerOptions
        {
            TlsCertPath = null,
            TlsSelfSigned = true
        });

        var provider = new TlsCertificateProvider();

        // Act
        var result = provider.GetOrCreate(options);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("FtpSharp", result.Subject);
        Assert.True(result.NotAfter > DateTime.UtcNow);
    }

    [Fact]
    public void GetOrCreate_WithSelfSignedDisabledAndNoCert_ShouldThrowException()
    {
        // Arrange
        var options = Options.Create(new FtpServerOptions
        {
            TlsCertPath = null,
            TlsSelfSigned = false
        });

        var provider = new TlsCertificateProvider();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetOrCreate(options));
        Assert.Contains("TLS certificate not configured", exception.Message);
    }

    [Fact]
    public void GetOrCreate_WithNonExistentCertPath_ShouldFallbackToSelfSigned()
    {
        // Arrange
        var options = Options.Create(new FtpServerOptions
        {
            TlsCertPath = "/nonexistent/path/cert.pfx",
            TlsSelfSigned = true
        });

        var provider = new TlsCertificateProvider();

        // Act
        var result = provider.GetOrCreate(options);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("FtpSharp", result.Subject);
    }

    [Fact]
    public void GetOrCreate_CalledTwice_ShouldReturnCachedCertificate()
    {
        // Arrange
        var options = Options.Create(new FtpServerOptions
        {
            TlsCertPath = null,
            TlsSelfSigned = true
        });

        var provider = new TlsCertificateProvider();

        // Act
        var result1 = provider.GetOrCreate(options);
        var result2 = provider.GetOrCreate(options);

        // Assert
        Assert.Same(result1, result2);
    }

    [Fact]
    public void GetOrCreate_WithEmptyPassword_ShouldTreatAsNoPassword()
    {
        // Arrange
        var tempCertPath = Path.GetTempFileName();
        try
        {
            // Create a certificate without password
            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            var req = new CertificateRequest("CN=EmptyPassword", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            
            File.WriteAllBytes(tempCertPath, cert.Export(X509ContentType.Pfx));

            var options = Options.Create(new FtpServerOptions
            {
                TlsCertPath = tempCertPath,
                TlsCertPassword = ""  // Empty password
            });

            var provider = new TlsCertificateProvider();

            // Act
            var result = provider.GetOrCreate(options);

            // Assert
            Assert.NotNull(result);
        }
        finally
        {
            if (File.Exists(tempCertPath))
                File.Delete(tempCertPath);
        }
    }
}