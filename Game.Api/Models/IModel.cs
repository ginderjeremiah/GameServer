namespace Game.Api.Models
{
    public interface IModel
    {

    }

    public interface IModelFromSource<TModel, TSource> : IModel where TModel : IModel
    {
        public static abstract TModel FromSource(TSource source);
    }

    public interface IModelToSource<TSource> : IModel
    {
        public TSource ToSource();
    }

    public interface IMappedModel<TModel, TEntity> : IModelFromSource<TModel, TEntity>, IModelToSource<TEntity> where TModel : IModel
    {

    }
}
