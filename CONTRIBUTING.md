# Development guidelines

## Fail fast on invalid assumptions

Mod code should distinguish expected host lifecycle states from broken invariants.

- Guard and return only for states explicitly allowed by the game or module contract, such as an entity whose `isUnloaded` flag has been set during normal teardown.
- Treat module-owned initialization, object identity, and required data as invariants. Do not add null propagation, broad exception handling, fallback values, or compatibility branches merely to keep running after an invariant is violated.
- When an invariant is not structurally guaranteed, assert it or throw a descriptive exception as close to its boundary as possible. Use the resulting failure to fix the defect or explicitly declare the affected game or mod combination incompatible.
- Compatibility handling must be based on a known semantic difference. It must not silently turn unexpected state into degraded behavior.

The goal of error handling during mod development is early, attributable failure—not maximum tolerance at the cost of hiding defects.
