# RFC-0002: Distributed Storage

- Status: Draft — open for discussion
- Author(s): tidsOS founding contributors
- Discussion: https://github.com/santoputhoor/tidsos/discussions/1
- Depends on: [RFC-0001](0001-node-registration.md) (node identity and liveness)

## Summary

Defines how tidsOS stores data durably across a fleet of nodes that
individually cannot be trusted to stay online, stay reachable, or even
continue to exist. Proposes a replicated, content-addressed **object
store** — not a POSIX filesystem or block device — as the MVP storage
primitive, built on top of the node registry from RFC-0001, with its own,
much more conservative notion of "this node is gone" than RFC-0001 uses for
general liveness.

This RFC is a design proposal, not an implementation. Storage durability
is the subsystem in this project where getting the design wrong is most
expensive to discover after the fact — unlike RFC-0001, where a wrong call
just meant a reconnect delay, a wrong call here means lost data. Per
[CONTRIBUTING.md](../../CONTRIBUTING.md), this should be discussed before
any of it gets built.

## Motivation

RFC-0001 solved "does this node currently exist and respond." Storage adds
a harder question: **when a node genuinely disappears — laptop stolen,
disk dies, someone leaves the company and wipes their machine — how does
tidsOS make sure the data that happened to live there isn't gone too?**

This is a different failure model than RFC-0001's "asleep vs. offline"
distinction:

- A node that's asleep for the night is *fine* — don't touch its data,
  don't re-replicate anything, don't waste the fleet's bandwidth reacting
  to something that will resolve itself by morning.
- A node that's genuinely gone needs its data re-replicated elsewhere
  *before* the other replicas also fail, or the data is lost for good.
- tidsOS has no data center, no RAID controller, no SAN behind it — the
  replication logic proposed here **is** the durability guarantee, in its
  entirety. There's nothing underneath to fall back on.

Existing systems solve this well but assume things tidsOS can't: Ceph
assumes OSDs are mostly-stable server disks; a NAS assumes one reliable
box. tidsOS needs the durability characteristics of the former on hardware
that behaves like the latter's opposite.

## Design

### Storage model: object store, not a filesystem

Data is stored as immutable, content-addressed **objects**, split into
fixed-size **chunks** (proposed default: 8 MiB) — similar in spirit to how
Git or IPFS content-addresses data, and how Ceph's RADOS layer separates
objects from the higher-level abstractions built on it.

- Each chunk is identified by the SHA-256 hash of its contents.
- Objects (files, VM disk images, container layers) are a manifest: an
  ordered list of chunk hashes plus metadata (size, created-at, owner).
- Content-addressing gets deduplication for free and makes corruption
  trivially detectable (hash mismatch = corrupt, full stop) — valuable
  when the underlying disks are consumer-grade laptop SSDs, not
  enterprise drives with their own integrity guarantees.

This RFC deliberately does **not** propose POSIX filesystem semantics
(in-place mutation, directory locking, partial-write visibility). Those
require coordination guarantees that are expensive to get right on
flaky nodes, and nothing in tidsOS's target workloads (VM disks, container
images, app data volumes) strictly needs them at this layer — a
higher-level RFC can build a filesystem view on top of immutable objects
later (versioning via new manifests) if a real workload needs it.

### Placement and replication

Each object is replicated to **R** nodes (proposed default `R = 3`,
configurable per-object later, not in the MVP). Placement is decided by a
**Placement Service** — logically part of the Controller from RFC-0001,
reusing its live view of the node registry:

1. Filter to nodes currently `Online` (per RFC-0001) with enough advertised
   free disk space for the chunk.
2. Prefer nodes that don't already hold another replica of the same
   object, to avoid correlated loss from a single machine.
3. Where available, prefer spreading replicas across whatever failure-domain
   hints exist (e.g., different subnets) — today tidsOS has none, so this
   step is a no-op until a later RFC adds topology hints to `RegisterRequest`.

Placement decisions and the resulting chunk→node map are the storage
metadata. For the MVP this metadata lives on the Controller (in-memory,
same pragmatic starting point RFC-0001 took for the node registry) — this
is a known single point of failure and is explicitly **not** solved here;
see [Unresolved questions](#unresolved-questions).

### Write and read path

- **Write:** client (agent or a future SDK) hashes the object into chunks,
  asks the Controller for a placement (a list of R target nodes per
  chunk), then pushes each chunk directly to those nodes' local chunk
  store. The write is considered durable once **W** of R nodes
  acknowledge (proposed `W = 2` of `R = 3` — tunable consistency/latency
  trade-off, not fixed by this RFC).
- **Read:** client asks the Controller (or, once cached, goes directly) for
  the node list holding a chunk, reads from any one, and verifies the
  hash. A hash mismatch or unreachable node falls through to the next
  replica and reports the failure so it counts toward that replica's
  health.

### Liveness for storage: a second, longer timeout

RFC-0001's reaper marks a node offline after ~30 seconds of missed
heartbeats — the right threshold for "should the scheduler avoid this node
right now," and far too aggressive for "should we spend fleet bandwidth
re-replicating everything it was holding." A laptop closed for a long
weekend would otherwise trigger a storm of unnecessary replication traffic
the moment everyone's back online and it reconnects.

This RFC proposes a **second, independent threshold**, owned by the
storage subsystem, not RFC-0001's reaper:

- `Suspect` (inherited from RFC-0001's `IsOnline = false`): stop placing
  *new* writes on this node, but do nothing to its existing replicas yet.
- `PresumedLost` (new; proposed default: offline for **24 hours**,
  configurable): treat every chunk this node held as under-replicated and
  queue re-replication from surviving copies to new target nodes.

The 24-hour default is a starting proposal, not a researched number —
flagged explicitly for discussion. It should comfortably exceed "gone for
a long weekend" while not leaving data under-replicated for so long that a
second, independent failure during that window causes real loss.

### Reconciliation loop

A background process (the storage-side analog of RFC-0001's
`StaleNodeReaper`) periodically scans for objects with fewer than `R`
healthy replicas — whether from a node crossing into `PresumedLost`, a
checksum failure on read, or a node that came back online with a
corrupted local store — and issues re-replication work to restore the
target count. This is the same idea as Ceph's placement-group scrubbing,
scaled down.

### What this explicitly does not cover

- **Security** — no encryption at rest, no authenticated writes, no
  access control on who can read/write which objects. Same gap RFC-0001
  flagged for the registry; both are blocked on RFC-0005.
- **Controller/metadata high availability** — the placement metadata has
  the same single-Controller SPOF the RFC-0001 registry has. A follow-up
  RFC needs to address Controller HA for the whole system, not just
  storage, once there's a concrete design to react to.
- **Erasure coding** — replication only for the MVP; erasure coding is a
  reasonable v2 to cut storage overhead once plain replication is proven
  and instrumented in Phase 3.
- **Quotas on donated disk space** — nothing here stops the storage layer
  from filling someone's laptop disk. Needs a per-node configurable budget
  before this leaves demo status.

## Alternatives considered

- **Run Ceph underneath tidsOS instead of building this.** Rejected as
  the *primary* layer: Ceph's OSD failure/recovery tuning assumes nodes
  are mostly-stable server disks; getting it to behave well with nodes
  that routinely sleep and roam networks fights the tool rather than uses
  it. The manifesto's "Ceph-inspired ideas, not a copy" stance reflects
  this — we're borrowing the object/placement-group *shape* of the idea,
  not the deployment.
- **IPFS / IPFS-Cluster.** Rejected as the primary layer: IPFS's
  content-addressing model is directly relevant and partly why it's
  borrowed here, but its DHT-based, globally-oriented design doesn't give
  a bounded private cluster the tight, controller-driven placement control
  this RFC wants (we already have an authoritative membership view from
  RFC-0001; re-deriving it via a DHT would be redundant).
- **POSIX-compliant distributed filesystem (GlusterFS/DRBD-style).**
  Rejected for the MVP — the locking and in-place-mutation guarantees a
  real POSIX filesystem needs are expensive to make correct on nodes that
  vanish mid-operation. Immutable objects sidestep that whole class of
  problem at the cost of not being a drop-in filesystem.
- **Erasure coding from day one.** Rejected for the MVP on complexity
  grounds — plain replication is easier to reason about, easier to debug
  when something goes wrong, and easier to explain to a new contributor.
  Revisit once the replication path has real operational experience
  behind it.

## Unresolved questions

- What is the right default for `R` (replication factor) and `W` (write
  quorum)? Proposed 3 and 2 above are starting points for discussion, not
  conclusions.
- Is 24 hours the right `PresumedLost` threshold? Should it be
  configurable per-deployment, or eventually adaptive based on an
  individual node's observed uptime pattern?
- How does the Controller's storage metadata survive a Controller
  restart or failure? (Same open question RFC-0001 left for the node
  registry — likely wants a unified answer, probably its own RFC on
  Controller persistence/HA rather than solving it twice.)
- How much local disk should an agent be allowed to donate, and who
  configures that — the machine's owner, a fleet-wide policy, or both?
- How do chunks get garbage-collected once no manifest references them
  anymore?
- What's the right chunk size? 8 MiB is a starting guess borrowed from
  similar systems, not derived from tidsOS-specific measurement.

## Compatibility

New addition — no existing wire protocol changes. Storage will need its
own gRPC service (proposed `StorageService`, alongside RFC-0001's
`NodeService`, likely in a new `TidsOS.Contracts/Protos/storage.proto`)
once this RFC moves past Draft. `RegisterRequest` from RFC-0001 will
likely need new fields later (advertised disk capacity, failure-domain
hints) — that's an additive, backward-compatible protobuf change when it
happens, not something this RFC needs to land itself.
