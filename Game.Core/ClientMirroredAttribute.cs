namespace Game.Core
{
    /// <summary>
    /// Marks a domain type that must be mirrored into the generated TypeScript client even when no
    /// API or socket DTO references it (so the codegen reflection walk, which only discovers types on
    /// the wire contract, would never reach it). The code generator
    /// (<c>Game.Api.CodeGen.ApiCodeGenerator</c>) scans this assembly for every type carrying this
    /// attribute and emits it: an <b>enum</b> is written to <c>enums.ts</c>, and a class of public
    /// constants is written to <c>game-constants.ts</c>. Either way it keeps a single source of truth
    /// across the frontend/backend boundary. Applying it at the declaration keeps the "this is
    /// mirrored to the client" decision next to the type itself rather than in a list elsewhere.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum | AttributeTargets.Class, Inherited = false)]
    public sealed class ClientMirroredAttribute : System.Attribute
    {
    }
}
