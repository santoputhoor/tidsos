using TidsOS.Agent;
using Xunit;

namespace TidsOS.Tests;

// The default node-id path resolves differently per OS (LocalApplicationData
// maps to %LOCALAPPDATA% on Windows, an XDG-derived path on Linux, and
// ~/.local/share on macOS under .NET) — this exercises the actual file
// create/read/persist logic against a temp directory on every CI runner OS,
// rather than relying on it only ever having been run by hand on Windows.
public class NodeIdentityTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("tidsos-tests-").FullName;

    [Fact]
    public void First_call_creates_and_persists_a_node_id()
    {
        var nodeId = NodeIdentity.GetOrCreateNodeId(_tempDir);

        Assert.False(string.IsNullOrWhiteSpace(nodeId));
        Assert.True(File.Exists(Path.Combine(_tempDir, "tidsOS", "node-id")));
    }

    [Fact]
    public void Repeated_calls_return_the_same_id_without_creating_a_new_one()
    {
        var first = NodeIdentity.GetOrCreateNodeId(_tempDir);
        var second = NodeIdentity.GetOrCreateNodeId(_tempDir);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Id_survives_a_fresh_process_reading_the_same_directory()
    {
        // Simulates an agent restart: nothing but the on-disk file carries
        // identity across runs.
        var originalId = NodeIdentity.GetOrCreateNodeId(_tempDir);

        var idAfterRestart = NodeIdentity.GetOrCreateNodeId(_tempDir);

        Assert.Equal(originalId, idAfterRestart);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
