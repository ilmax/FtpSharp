using System.Runtime.InteropServices;
using FtpServer.Core.Configuration;
using FtpServer.Core.FileSystem;
using Microsoft.Extensions.Options;

namespace FtpServer.Tests;

public class FileSystemStorageProviderTests
{
    [Fact]
    public async Task FileOperations_EndToEnd()
    {
        var root = Path.Combine(Path.GetTempPath(), "ftpstest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var opts = Options.Create(new FtpServerOptions { StorageRoot = root });
            var fs = new FileSystemStorageProvider(opts);

            await fs.CreateDirectoryAsync("/d1", CancellationToken.None);
            Assert.True(await fs.ExistsAsync("/d1", CancellationToken.None));

            static async IAsyncEnumerable<ReadOnlyMemory<byte>> Chunks(params string[] parts)
            {
                foreach (var p in parts)
                {
                    yield return System.Text.Encoding.ASCII.GetBytes(p);
                    await Task.Yield();
                }
            }

            await fs.WriteAsync("/d1/a.txt", Chunks("hel", "lo"), CancellationToken.None);
            await fs.AppendAsync("/d1/a.txt", Chunks("!"), CancellationToken.None);
            Assert.Equal(6, await fs.GetSizeAsync("/d1/a.txt", CancellationToken.None));

            var read = new List<byte>();
            await foreach (var c in fs.ReadAsync("/d1/a.txt", 8, CancellationToken.None)) read.AddRange(c.ToArray());
            Assert.Equal("hello!", System.Text.Encoding.ASCII.GetString(read.ToArray()));

            read.Clear();
            await foreach (var c in fs.ReadFromOffsetAsync("/d1/a.txt", 3, 8, CancellationToken.None)) read.AddRange(c.ToArray());
            Assert.Equal("lo!", System.Text.Encoding.ASCII.GetString(read.ToArray()));

            var list = await fs.ListAsync("/d1", CancellationToken.None);
            Assert.Single(list);
            Assert.Equal("a.txt", list[0].Name);

            await fs.RenameAsync("/d1/a.txt", "/d1/b.txt", CancellationToken.None);
            Assert.True(await fs.ExistsAsync("/d1/b.txt", CancellationToken.None));

            await fs.WriteTruncateThenAppendAsync("/d1/b.txt", 2, Chunks("XYZ"), CancellationToken.None);
            read.Clear();
            await foreach (var c in fs.ReadAsync("/d1/b.txt", 8, CancellationToken.None)) read.AddRange(c.ToArray());
            Assert.Equal("heXYZ", System.Text.Encoding.ASCII.GetString(read.ToArray()));

            await fs.DeleteAsync("/d1/b.txt", recursive: false, CancellationToken.None);
            Assert.False(await fs.ExistsAsync("/d1/b.txt", CancellationToken.None));

            await fs.DeleteAsync("/d1", recursive: false, CancellationToken.None);
            Assert.False(await fs.ExistsAsync("/d1", CancellationToken.None));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
