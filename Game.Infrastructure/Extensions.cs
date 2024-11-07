using Game.Core;
using Game.Core.CustomAttributes;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Reflection;

namespace Game.Infrastructure
{
    internal static class Extensions
    {
        public static void HasEnumValues<T, E>(this EntityTypeBuilder<T> entityBuilder)
            where T : class, new()
            where E : struct, Enum
        {
            var entityProps = typeof(T).GetProperties();
            var idProp = entityProps.FirstOrDefault(p => p.Name == "Id");
            var nameProp = entityProps.FirstOrDefault(p => p.Name == "Name");
            var values = Enum.GetValues<E>();
            var enumType = typeof(E);

            var entities = values.Select(enumValue =>
            {
                var entity = new T();
                var valueName = enumValue.ToString();
                idProp?.SetValue(entity, enumValue);
                nameProp?.SetValue(entity, valueName.Capitalize().SpaceWords());
                var metaData = enumType.GetField(valueName)?.GetCustomAttributes<MetadataAttribute>();
                if (metaData is not null)
                {
                    foreach (var data in metaData)
                    {
                        if (entityProps.FirstOrDefault(p => p.Name == data.Key) is PropertyInfo p)
                        {
                            p.SetValue(entity, data.Value);
                        }
                    }
                }

                return entity;
            });

            entityBuilder.HasData(entities);
        }
    }
}
