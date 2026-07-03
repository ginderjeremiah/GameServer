namespace Game.Api.Models
{
    public interface IModelFromSource<TModel, TSource> : IModel where TModel : IModel
    {
        public static abstract TModel FromSource(TSource source);
    }
}
