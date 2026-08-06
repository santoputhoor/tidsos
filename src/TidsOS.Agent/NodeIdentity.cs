namespace TidsOS.Agent;

// A node's identity must survive agent restarts (and sleep/wake cycles) so
// the controller recognizes it as the same machine rather than a new one
// every time. We persist a generated id once, next to the agent's data.
public static class NodeIdentity
{
    // Env.SpecialFolder.LocalApplicationData resolves differently per OS
    // (%LOCALAPPDATA% on Windows, XDG_DATA_HOME-derived on Linux, ~/.local/share
    // on macOS under .NET) — the override exists so tests can point this at a
    // temp directory instead of asserting on a real per-OS path.
    public static string GetOrCreateNodeId(string? baseDirectory = null)
    {
        var statePath = Path.Combine(
            baseDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "tidsOS", "node-id");

        if (File.Exists(statePath))
        {
            var existing = File.ReadAllText(statePath).Trim();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }
        }

        var nodeId = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(statePath, nodeId);
        return nodeId;
    }
}
