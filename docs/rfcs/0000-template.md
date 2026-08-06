# RFC-NNNN: Title

- Status: Draft | In Discussion | Accepted | Implemented | Rejected | Superseded
- Author(s):
- Discussion: (link to the GitHub Discussion once opened)

## Summary

One paragraph: what is being proposed, and why.

## Motivation

What problem does this solve? Who hits it, and how, today? Be concrete —
tie it back to tidsOS's actual constraint (heterogeneous, unreliable,
unmanaged office hardware), not a generic cloud-infrastructure concern.

## Design

The proposal itself. Enough detail that someone else could implement it
without having to ask you clarifying questions. Include:

- Interfaces / protocols (proto definitions, APIs) if applicable
- Data structures and how they're persisted
- Failure modes: what happens when a node disappears mid-operation? When
  two nodes disagree? When the controller itself restarts?

## Alternatives considered

What else was considered, and why was it rejected? This is often the most
useful section for future readers wondering "why didn't they just...".

## Unresolved questions

What's still open, deferred to implementation, or deferred to a later RFC?

## Compatibility

Does this change any existing wire protocol, on-disk format, or CLI
surface? How do older agents/controllers behave against a newer peer?
