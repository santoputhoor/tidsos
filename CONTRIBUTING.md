# Contributing to tidsOS

Thanks for considering it. This project is trying to do something specific —
a private cloud that runs on unreliable, heterogeneous, everyday hardware —
and stay small and coherent while it does it. That shapes how contributions
work here.

## Before you write code

**Non-trivial features start as an RFC, not a pull request.** If your change
affects node identity, storage, networking, scheduling, security, or the
application runtime, open an RFC first using
[`docs/rfcs/0000-template.md`](docs/rfcs/0000-template.md) and start a
[Discussion](https://github.com/santoputhoor/tidsos/discussions) before
writing implementation code. This isn't bureaucracy for its own sake — it's
so we don't merge two incompatible ideas of how the scheduler should work
and have to unwind one later.

Bug fixes, documentation, tests, and small, self-contained improvements to
already-implemented RFCs don't need this — just open a pull request.

## Setting up

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/santoputhoor/tidsos.git
cd tidsos
dotnet build
dotnet test
```

See the [README quickstart](README.md#quickstart-run-the-node-registration-demo)
to run the controller and an agent locally.

## Making a change

1. Fork the repo and create a branch from `main`.
2. Make your change. Match the existing style — the codebase intentionally
   avoids unnecessary abstraction; prefer the direct, readable version.
3. Add or update tests for anything behavioral.
4. Run `dotnet build` and `dotnet test` — CI runs the same checks.
5. Open a pull request describing *why*, not just *what* — link the RFC or
   issue it implements.

## Code style

- C#, nullable reference types on, `ImplicitUsings` on.
- No commented-out code, no speculative abstractions for hypothetical future
  needs — build what the current RFC actually calls for.
- Comments explain *why* when it's non-obvious (a workaround, an invariant),
  not *what* — the code should already say what.

## Reporting bugs / requesting features

Open a [GitHub Issue](https://github.com/santoputhoor/tidsos/issues). For
anything involving a real security vulnerability, do not open a public
issue — see [SECURITY.md](SECURITY.md) (or, until that exists, contact the
maintainers directly).

## Code of Conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md).
Participation means agreeing to abide by it.
