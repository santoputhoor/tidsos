namespace TidsOS.Agent;

// A node's identity must survive agent restarts (and sleep/wake cycles) so
// the controller recognizes it as the same machine rather than a new one
// every time. We persist a generated id once, next to the agent's data.
public static class NodeIdentity
{
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "tidsOS", "node-id");

    public static string GetOrCreateNodeId()
    {
        if (File.Exists(StatePath))
        {
            var existing = File.ReadAllText(StatePath).Trim();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }
        }

        var nodeId = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        File.WriteAllText(StatePath, nodeId);
        return nodeId;
    }
}
