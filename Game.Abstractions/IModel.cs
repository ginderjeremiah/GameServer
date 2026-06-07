namespace Game.Abstractions
{
    /// <summary>
    /// Marker for a type that crosses the API boundary as a serialized model/contract.
    /// It lives in <c>Game.Abstractions</c> so the read contracts (<c>Game.Abstractions.Contracts</c>)
    /// and the <c>Game.Api</c> response/codegen infrastructure can both reference it.
    /// </summary>
    public interface IModel
    {

    }
}
