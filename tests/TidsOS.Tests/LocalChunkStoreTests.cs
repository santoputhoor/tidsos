using System.Text;
using TidsOS.Agent.Storage;
using Xunit;

namespace TidsOS.Tests;

public class LocalChunkStoreTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("tidsos-chunkstore-tests-").FullName;
    private readonly LocalChunkStore _store;

    public LocalChunkStoreTests()
    {
        _store = new LocalChunkStore(_tempDir);
    }

    [Fact]
    public void Put_with_the_correct_hash_is_accepted_and_retrievable()
    {
        var content = "hello tidsOS"u8.ToArray();
        var hash = LocalChunkStore.ComputeHash(content);

        var (accepted, hashVerified) = _store.Put(hash, content);

        Assert.True(accepted);
        Assert.True(hashVerified);
        Assert.True(_store.TryGet(hash, out var retrieved));
        Assert.Equal(content, retrieved);
    }

    [Fact]
    public void Put_rejects_content_that_does_not_match_the_claimed_hash()
    {
        var content = "hello tidsOS"u8.ToArray();
        var wrongHash = LocalChunkStore.ComputeHash("something else entirely"u8.ToArray());

        var (accepted, hashVerified) = _store.Put(wrongHash, content);

        Assert.False(accepted);
        Assert.False(hashVerified);
        Assert.False(_store.TryGet(wrongHash, out _));
    }

    [Fact]
    public void Put_is_idempotent_for_the_same_hash()
    {
        var content = Encoding.UTF8.GetBytes("idempotent write");
        var hash = LocalChunkStore.ComputeHash(content);

        var first = _store.Put(hash, content);
        var second = _store.Put(hash, content);

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
    }

    [Fact]
    public void GetChunk_for_an_unknown_hash_is_not_found()
    {
        var unknownHash = LocalChunkStore.ComputeHash("never written"u8.ToArray());

        Assert.False(_store.TryGet(unknownHash, out _));
    }

    [Fact]
    public void PathFor_rejects_hash_shaped_input_that_could_escape_the_storage_directory()
    {
        // The chunk store trusts hashes as path segments — reject anything
        // that isn't the exact lowercase-hex-SHA-256 shape before it ever
        // reaches the filesystem, rather than sanitizing a path traversal.
        Assert.Throws<ArgumentException>(() => _store.TryGet("../../etc/passwd", out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
