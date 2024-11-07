using Game.Api.Models.Common;
using System.Reflection;

namespace Game.Api.CodeGen.Data
{
    internal class EndpointMetadata
    {
        public string Endpoint { get; set; }
        public CodeGenTypeDescriptor? ResponseDescriptor { get; }
        public List<CodeGenTypeDescriptor> ParameterDescriptors { get; }
        public bool IsGet { get; set; }

        public EndpointMetadata(MethodInfo endpoint)
        {
            var returnNullabilityInfo = endpoint.ReturnParameter.GetNullabilityInfo();
            if (returnNullabilityInfo.Type.IsAssignableTo(typeof(Task)))
            {
                returnNullabilityInfo = returnNullabilityInfo.GenericTypeArguments[0];
            }

            if (returnNullabilityInfo.Type.IsConstructedGenericType)
            {
                if (returnNullabilityInfo.Type.IsAssignableTo(typeof(IApiCollectionResponse)))
                {
                    var listType = typeof(List<>).MakeGenericType(returnNullabilityInfo.GenericTypeArguments[0].Type);
                    ResponseDescriptor = new CodeGenTypeDescriptor(returnNullabilityInfo, listType);
                }
                else
                {
                    ResponseDescriptor = new CodeGenTypeDescriptor(returnNullabilityInfo.GenericTypeArguments[0]);
                }
            }

            ParameterDescriptors = endpoint.GetParameters().Select(p => new CodeGenTypeDescriptor(p)).ToList();
        }
    }
}
