# RFC-0001: Node Registration and Liveness

- Status: Implemented (MVP)
- Author(s): tidsOS founding contributors
- Discussion: (link once opened on GitHub Discussions)

## Summary

Defines how an Agent (running on an office PC or laptop) introduces itself
to a Controller, how the Controller tracks whether a node is currently
reachable, and — critically — how the system tells the difference between
"this node is gone" and "this node is asleep." This is the foundation every
later RFC (storage, scheduling, networking) builds on: nothing else can
place work on a node or replicate data to it until the cluster has a
trustworthy, low-drama answer to "which nodes exist right now?"

## Motivation

On server-class infrastructure, a node going quiet almost always means
something is wrong. On a fleet of office PCs and laptops, a node going
quiet is the *normal, hourly* case: someone closed their laptop lid, Wi-Fi
dropped for ten seconds, a machine got shut down at 5pm and will be back at
9am tomorrow. A registration/liveness design copied from a data-center
system would treat all of these as failures and either thrash (constantly
rescheduling work) or require an administrator to babysit it — exactly what
tidsOS exists to avoid.

Two requirements follow directly:

1. Registration must be **idempotent and cheap to repeat** — an agent that
   reconnects after a nap should not need special "am I already known?"
   logic; it just registers again.
2. Liveness must be judged on a **pattern of missed heartbeats**, not a
   single one, and the threshold must be visible and tunable — not a magic
   number buried in code.

## Design

### Identity

Each node generates a UUID on first run and persists it locally
(`%LOCALAPPDATA%\tidsOS\node-id` today; the equivalent XDG path on
Linux/macOS is future work). This id, not the hostname or IP address —
both of which change — is the node's stable identity across restarts,
sleep/wake cycles, and network changes.

### Wire protocol

A single gRPC service, `NodeService` (see
[`src/TidsOS.Contracts/Protos/node.proto`](../../src/TidsOS.Contracts/Protos/node.proto)):

```protobuf
service NodeService {
  rpc Register(RegisterRequest) returns (RegisterResponse);
  rpc Heartbeat(stream HeartbeatRequest) returns (stream HeartbeatAck);
}
```

- **`Register`** is a unary call. It carries the node id, hostname, OS
  description, CPU core count, available memory, and agent version. It is
  safe to call repeatedly with the same `node_id` — the Controller treats a
  second registration as "this node is back," not an error.
- **`Heartbeat`** is a **client-streaming** call, kept open for the life of
  the agent's session. The agent writes a beat on the interval the
  Controller assigned during registration (10s in the current
  implementation); the Controller acknowledges each one. Streaming rather
  than repeated unary calls was chosen deliberately: it makes "the stream
  closed" a first-class, cheap-to-detect signal, separate from "a single
  beat was late."

### Liveness

The Controller does not mark a node offline on a missed beat. A background
reaper (`StaleNodeReaper`) runs every 5 seconds and only flips a node to
offline once its last heartbeat is older than 30 seconds — three missed
intervals at the default cadence. This threshold is intentionally generous
and intentionally a named constant, not tuned to be clever: false "node is
down" signals are more disruptive to this fleet than a 30-second detection
lag.

### Reconnection

The agent's session loop (`Worker.RunSessionAsync`) treats any failure —
network drop, controller restart, sleep/wake — the same way: catch it, back
off (2s, 5s, 10s, 30s, 60s, then hold), and try again. A successful session
resets the backoff. There is no "give up" state; a laptop that's closed for
a three-day weekend is expected to reconnect Monday exactly like one that
dropped Wi-Fi for a second.

### What this explicitly does not do yet

- No authentication/authorization on `Register` or `Heartbeat` — any agent
  can currently register as any node id. That's RFC-0005 (Security).
  Fine for a local demo; not fine to expose beyond localhost/a trusted LAN.
- No persistence of the registry across a Controller restart — it's an
  in-memory `ConcurrentDictionary` today. That's RFC-0002 (Distributed
  Storage): once it exists, the registry should live there.
- No signal to the (not-yet-built) scheduler when a node's status changes.
  The reaper currently only logs. Wiring that up is scoped to RFC-0004.

## Alternatives considered

- **Single missed heartbeat = offline.** Rejected — this is precisely the
  behavior that would make tidsOS unusable on real office hardware; a
  laptop locking for 15 seconds shouldn't evict it from the cluster.
- **Polling instead of streaming heartbeats** (Controller calls out to each
  agent periodically). Rejected for the MVP: it inverts the connectivity
  assumption tidsOS needs, since agents are far more likely to be reachable
  *from* than reachable *to* (NAT, sleep, firewalls) — the agent should
  always be the one holding the connection open.
- **Hostname or IP as node identity.** Rejected — both are unstable on
  laptops that roam between networks.

## Unresolved questions

- Should the heartbeat interval be adaptive (e.g., longer for known-stable
  desktops, shorter for laptops observed to roam) rather than a single
  global default? Deferred to real-world data from Phase 3.
- What happens when the same `node_id` file is copied onto two different
  machines (e.g., cloned VM image)? Currently: the second one to register
  silently overwrites the first's state. Needs a real answer before this
  leaves "demo" status — likely a collision check plus a controller-issued,
  signed identity, which folds into RFC-0005.

## Compatibility

First version of the protocol — no backward-compatibility constraints yet.
Once this ships in a tagged release, protobuf field numbers in `node.proto`
become append-only (new fields get new numbers; existing ones are never
reused or renumbered).
