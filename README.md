# tidsOS

**A private cloud operating system for the computers you already own.**

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![Build](https://github.com/santoputhoor/tidsos/actions/workflows/ci.yml/badge.svg)](https://github.com/santoputhoor/tidsos/actions/workflows/ci.yml)
[![Discussions](https://img.shields.io/badge/discuss-GitHub%20Discussions-blueviolet)](https://github.com/santoputhoor/tidsos/discussions)

> Computing should not depend on expensive servers. Every computer already has
> unused CPU, RAM, storage, and network bandwidth. tidsOS turns these isolated
> resources into one intelligent, self-healing private cloud — so every
> organization can own its own cloud, not rent one.

## The problem this project solves

Kubernetes, VMware, OpenStack, CloudStack, OpenNebula, and Harvester are all
excellent — and all built on the same assumption: **the machines underneath
are servers.** Rack-mounted, always on, wired to a UPS, managed by someone
whose job is to keep them running.

Most organizations don't have that. They have 20 to 500 office PCs and
laptops that:

- go to sleep or get shut down every night,
- move between Wi-Fi and Ethernet, home and office,
- run Windows, Linux, and macOS, with wildly different hardware,
- have no dedicated system administrator watching them.

tidsOS is a private cloud operating system designed for *that* environment —
not a smaller Kubernetes, a different engineering problem: build a cluster
that assumes its nodes are unreliable by default, and stays healthy anyway.

## What tidsOS is not

- Not a Kubernetes competitor for data-center workloads — use Kubernetes for that.
- Not a hypervisor. It orchestrates workloads across existing hardware;
  it borrows QEMU, containerd, and WireGuard rather than reinventing them.
- Not ready for production. This is the earliest possible stage: one RFC
  implemented, one demo working. See [Status](#status) below.

## Status

**Pre-alpha.** RFC-0001 (Node Registration) has a working reference
implementation: a Controller that nodes register and heartbeat against, and
an Agent that runs on a node, survives disconnects, and reconnects with
backoff. That's it so far — storage, scheduling, networking, and the runtime
are all open design work. See [`docs/rfcs/`](docs/rfcs) and the
[roadmap](#roadmap).

If you're looking for something you can run in production today, this isn't
it yet. If you want to help design and build it, you're in the right place —
see [Contributing](#contributing).

## Quickstart: run the node-registration demo

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/santoputhoor/tidsos.git
cd tidsos
dotnet build
```

In one terminal, start the controller:

```bash
dotnet run --project src/TidsOS.Controller
```

In another terminal, start an agent (it will register itself and begin
heartbeating):

```bash
dotnet run --project src/TidsOS.Agent
```

Watch the controller's view of the fleet:

```bash
curl http://localhost:5271/nodes
```

Kill the agent (Ctrl+C) and restart it — it keeps the same node identity
and picks up where it left off. Kill the *controller* instead and the agent
will retry with exponential backoff until it comes back. That resilience to
disappearing nodes is the whole point of RFC-0001, and the reason this is the
first thing built.

## Architecture direction

| Layer | Approach |
|---|---|
| Node agent / controller | Custom, C# / .NET, gRPC over QUIC/HTTP2 |
| Node identity & liveness | RFC-0001 (implemented) |
| Distributed storage | RFC-0002 (design stage) — Ceph-inspired, not a copy |
| Virtual networking | RFC-0003 (design stage) — WireGuard-based overlay |
| Scheduler | RFC-0004 (design stage) — resource- and reliability-aware |
| Security model | RFC-0005 (design stage) |
| Application runtime | RFC-0006 (design stage) — containers via containerd, VMs via QEMU |
| Marketplace | RFC-0007 (not started) |

Full technology rationale lives in the RFCs, not in this README — the README
should stay readable; the RFCs carry the detail and the trade-offs.

## Repository structure

This starts as a single repository (`tidsos`) so the early architecture can
move fast without cross-repo coordination overhead. Once interfaces
stabilize, pieces are expected to split out (agent, controller, storage,
network, scheduler, runtime, CLI, dashboard, SDK) the way Kubernetes did —
not before.

```
src/
  TidsOS.Contracts/    shared gRPC contracts (protobuf)
  TidsOS.Controller/    the control plane nodes register and heartbeat against
  TidsOS.Agent/         runs on every participating machine
docs/
  rfcs/                 design proposals — how tidsOS makes decisions
```

## RFC process

Every non-trivial feature starts as an RFC, the way Kubernetes and Rust do
it — a written proposal, open for discussion, before code. See
[`docs/rfcs/0000-template.md`](docs/rfcs/0000-template.md) to propose one and
[`docs/rfcs/0001-node-registration.md`](docs/rfcs/0001-node-registration.md)
for a worked example matching the code in this repo.

This project intentionally does not want "hundreds of contributors adding
random features." It wants a small number of well-considered ones. If that's
the kind of project you want to help build, RFCs are the way in.

## Roadmap

- **Phase 1 — Architecture:** vision, whitepaper, RFCs for storage,
  networking, scheduling, security.
- **Phase 2 — Foundation:** repo structure, CI/CD, coding standards, first
  agent *(you are here)*.
- **Phase 3 — MVP:** 5-node cluster, distributed storage, first deployed
  application, live dashboard, high availability.
- **Phase 4 — v1.0:** Oracle / PostgreSQL / SQL Server deployment support,
  AI workload scheduling, marketplace, enterprise edition.

## Contributing

Start with [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of
Conduct](CODE_OF_CONDUCT.md). Good entry points right now:

- Read RFC-0001 and try to break the demo — sleep the agent's machine,
  cut its network, run two agents with the same identity file, and file
  an issue for whatever surprises you.
- Pick up an open RFC discussion in [Discussions](https://github.com/santoputhoor/tidsos/discussions)
  and add a design proposal.
- Anything tagged [`good first issue`](https://github.com/santoputhoor/tidsos/labels/good%20first%20issue)
  once the tracker has some.

## License

Apache License 2.0 — see [LICENSE](LICENSE). Chosen deliberately: permissive
enough that any organization can adopt tidsOS freely, with an explicit
patent grant to protect contributors and adopters alike.
