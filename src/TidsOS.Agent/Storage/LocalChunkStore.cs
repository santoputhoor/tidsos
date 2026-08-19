using System.Security.Cryptography;

namespace TidsOS.Agent.Storage;

// This node's half of RFC-0002: a flat, content-addressed directory of
// chunks on local disk. Chunks are immutable once written, so there's no
// locking or partial-write visibility problem to solve — a write either
// lands as a complete, hash-verified file, or the temp file is discarded.
public sealed class LocalChunkStore
{
    private readonly string _baseDirectory;

    public LocalChunkStore(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "tidsOS", "chunks");
        Directory.CreateDirectory(_baseDirectory);
    }

    public static string ComputeHash(ReadOnlySpan<byte> content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));

    // Returns (accepted, hashVerified). A hash mismatch is rejected outright —
    // the store never trusts the caller's claimed hash over the actual bytes.
    public (bool Accepted, bool HashVerified) Put(string chunkHash, ReadOnlyMemory<byte> content)
    {
        var actualHash = ComputeHash(content.Span);
        if (!string.Equals(actualHash, chunkHash, StringComparison.OrdinalIgnoreCase))
        {
            return (false, false);
        }

        var path = PathFor(chunkHash);
        if (File.Exists(path))
        {
            return (true, true); // idempotent: identical hash implies identical content
        }

        // Write to a temp file first and rename, so a crash mid-write can
        // never leave a corrupt file sitting at the chunk's real path.
        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllBytes(tempPath, content.ToArray());
        File.Move(tempPath, path, overwrite: true);
        return (true, true);
    }

    public bool TryGet(string chunkHash, out byte[] content)
    {
        var path = PathFor(chunkHash);
        if (!File.Exists(path))
        {
            content = [];
            return false;
        }

        content = File.ReadAllBytes(path);
        return true;
    }

    private string PathFor(string chunkHash)
    {
        // Hashes are attacker-adjacent input in principle (a malicious peer
        // could claim any hash string); reject anything that isn't the
        // hex-SHA-256 shape rather than letting it touch the filesystem path.
        if (chunkHash.Length != 64 || !IsLowercaseHex(chunkHash))
        {
            throw new ArgumentException($"Not a valid SHA-256 hex hash: {chunkHash}", nameof(chunkHash));
        }

        return Path.Combine(_baseDirectory, chunkHash);
    }

    private static bool IsLowercaseHex(string value)
    {
        foreach (var c in value)
        {
            if (!((c is >= '0' and <= '9') || (c is >= 'a' and <= 'f')))
            {
                return false;
            }
        }

        return true;
    }
}
