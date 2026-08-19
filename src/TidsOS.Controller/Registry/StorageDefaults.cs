namespace TidsOS.Controller.Registry;

// RFC-0002 proposed defaults, shared by placement and status reporting so
// they can't drift apart. Starting points flagged for discussion, not
// tuned values — see the RFC's Unresolved Questions.
public static class StorageDefaults
{
    public const int ReplicationFactor = 3; // R
    public const int WriteQuorum = 2;       // W
}
