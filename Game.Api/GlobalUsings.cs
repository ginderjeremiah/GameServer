// IModel (the cross-boundary model marker) lives in Game.Abstractions so the read contracts
// there can satisfy the IModel-constrained API response/Change generics. This global using keeps
// it resolvable from every Game.Api file without per-file usings.
global using Game.Abstractions;
