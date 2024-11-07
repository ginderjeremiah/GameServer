namespace Game.Api.Models
{
    public class ModelMapper<TSource>
    {
        private readonly IEnumerable<TSource>? _source;

        public ModelMapper(IEnumerable<TSource>? source)
        {
            _source = source;
        }

        public IEnumerable<TModel> Model<TModel>() where TModel : IModelFromSource<TModel, TSource>, new()
        {
            if (_source is null)
            {
                yield break;
            }

            foreach (var item in _source)
            {
                yield return TModel.FromSource(item);
            }
        }
    }
}
