namespace Game.Api.Models
{
    public class AsyncModelMapper<TSource>
    {
        private readonly IAsyncEnumerable<TSource>? _source;

        public AsyncModelMapper(IAsyncEnumerable<TSource>? source)
        {
            _source = source;
        }

        public async IAsyncEnumerable<TModel> Model<TModel>() where TModel : IModelFromSource<TModel, TSource>, new()
        {
            if (_source is null)
            {
                yield break;
            }

            await foreach (var item in _source)
            {
                yield return TModel.FromSource(item);
            }
        }
    }
}
