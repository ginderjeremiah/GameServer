namespace Game.Core
{
    /// <summary>
    /// Marks a domain enum that must be mirrored into the generated TypeScript client even when no
    /// API or socket DTO references it (so the codegen reflection walk, which only discovers types on
    /// the wire contract, would never reach it). The code generator
    /// (<c>Game.Api.CodeGen.ApiCodeGenerator</c>) scans this assembly for every enum carrying this
    /// attribute and emits it into <c>enums.ts</c>, keeping a single source of truth across the
    /// frontend/backend boundary. Applying it at the enum declaration keeps the "this is mirrored to
    /// the client" decision next to the enum itself rather than in a list elsewhere.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Enum, Inherited = false)]
    public sealed class ClientMirroredAttribute : System.Attribute
    {
    }
}
