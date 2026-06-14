using Game.Abstractions;
using Game.Api.Models;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Tests.CodeGen
{
    public class SimpleModel : IModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
    }

    public class ModelWithNullable : IModel
    {
        public int? NullableInt { get; set; }
        public string? NullableName { get; set; }
        public DateTime? NullableDate { get; set; }
    }

    public class ModelWithList : IModel
    {
        public List<SimpleModel> Items { get; set; } = [];
        public List<int> Numbers { get; set; } = [];
    }

    public class ModelWithEnum : IModel
    {
        public TestEnum Status { get; set; }
        public string Label { get; set; } = "";
    }

    public class GenericModel<T> : IModel
    {
        public T Value { get; set; } = default!;
        public string Description { get; set; } = "";
    }

    public class ModelWithDecimal : IModel
    {
        public decimal Amount { get; set; }
        public float Rate { get; set; }
    }

    public class ModelWithDateTime : IModel
    {
        public DateTime CreatedAt { get; set; }
        public string Name { get; set; } = "";
    }

    public enum TestEnum
    {
        None = 0,
        Active = 1,
        Inactive = 2,
        Pending = 3
    }

    // A byte-backed enum: casting its members to int (the old enum-rendering path) would have worked
    // here but throwing/truncation risk exists for larger backing types; this pins that the raw
    // constant value is rendered regardless of backing type.
    public enum ByteBackedEnum : byte
    {
        Zero = 0,
        TwoHundred = 200
    }

    // A long-backed enum with a value outside the int range: the old (int)value cast would overflow,
    // so this pins that GetRawConstantValue renders the true value.
    public enum LongBackedEnum : long
    {
        Small = 1,
        Huge = 5_000_000_000
    }

    // An enum with an aliased member (two names sharing a value): rendering from field.Name keeps both
    // distinct names, where value.ToString() would have emitted the same canonical name twice.
    public enum AliasedEnum
    {
        First = 1,
        Second = 2,
        AliasOfFirst = 1
    }

    // A model whose property types (byte, char) have no TypeScript mapping and are rejected by
    // NeedsInterface (they are primitives), so GetTypeText must throw rather than emit a reference to an
    // interface that is never generated.
    public class ModelWithUnmappedType : IModel
    {
        public byte ByteValue { get; set; }
        public char CharValue { get; set; }
    }

    public class NestedModel : IModel
    {
        public SimpleModel Child { get; set; } = new();
        public string Title { get; set; } = "";
    }

    public class ModelWithDictionary : IModel
    {
        public Dictionary<string, int> StringToInt { get; set; } = [];
        public Dictionary<string, SimpleModel> StringToClass { get; set; } = [];
        public Dictionary<int, string> IntToString { get; set; } = [];
        public Dictionary<string, SimpleModel?> StringToNullableClass { get; set; } = [];
    }

    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpGet("/api/[controller]")]
        public ApiResponse<SimpleModel> GetSimple()
        {
            return new ApiResponse<SimpleModel>();
        }

        [HttpPost]
        public ApiResponse PostData([FromBody] SimpleModel model)
        {
            return new ApiResponse();
        }

        [HttpGet]
        public ApiEnumerableResponse<SimpleModel> GetList()
        {
            return new ApiEnumerableResponse<SimpleModel>();
        }

        [HttpPost]
        public Task<ApiResponse<SimpleModel>> AsyncEndpoint([FromBody] ModelWithEnum data)
        {
            return Task.FromResult(new ApiResponse<SimpleModel>());
        }
    }

    [Route("api/custom/[action]")]
    [ApiController]
    public class CustomRouteController : ControllerBase
    {
        [HttpGet]
        public ApiResponse<SimpleModel> Items()
        {
            return new ApiResponse<SimpleModel>();
        }

        [HttpPost("/api/override/path")]
        public ApiResponse Save([FromBody] SimpleModel model)
        {
            return new ApiResponse();
        }
    }

    // Resolves to the route "api/Prefix" (no [action]); pins that the "api/" prefix is removed cleanly
    // to "Prefix" with no leading slash and no dropped character (the route["api/".Length..] slice).
    [Route("/api/[controller]")]
    [ApiController]
    public class PrefixController : ControllerBase
    {
        [HttpGet]
        public ApiResponse<SimpleModel> Get()
        {
            return new ApiResponse<SimpleModel>();
        }
    }

    // Resolves to a route that does NOT start with "api/"; pins that such a route is left intact rather
    // than being silently mis-sliced.
    [Route("/health/[action]")]
    [ApiController]
    public class NonApiRouteController : ControllerBase
    {
        [HttpGet]
        public ApiResponse<SimpleModel> Check()
        {
            return new ApiResponse<SimpleModel>();
        }
    }

    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class MultiParamController : ControllerBase
    {
        [HttpPost]
        public ApiResponse UpdateMultiple(int id, string name)
        {
            return new ApiResponse();
        }

        [HttpPost]
        public ApiResponse OptionalParams(int id, string? name)
        {
            return new ApiResponse();
        }
    }

    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ControllerWithNonAction : ControllerBase
    {
        [HttpGet]
        public ApiResponse<SimpleModel> ValidAction()
        {
            return new ApiResponse<SimpleModel>();
        }

        [NonAction]
        public ApiResponse<SimpleModel> NotAnAction()
        {
            return new ApiResponse<SimpleModel>();
        }
    }

    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ControllerWithMixedReturns : ControllerBase
    {
        [HttpGet]
        public ApiResponse<SimpleModel> ValidEndpoint()
        {
            return new ApiResponse<SimpleModel>();
        }

        public string NotAnEndpoint()
        {
            return "hello";
        }

        public int AlsoNotAnEndpoint()
        {
            return 42;
        }
    }

    public class GenericModelWithGenericProperty<T> : IModel
    {
        public List<T> Items { get; set; } = [];
        public T Value { get; set; } = default!;
    }

    public class ModelWithNestedGenerics : IModel
    {
        public List<SimpleModel> SimpleList { get; set; } = [];
        public Dictionary<string, SimpleModel> DictWithClass { get; set; } = [];
        public GenericModel<SimpleModel> NestedGeneric { get; set; } = new();
        public string NonGenericProperty { get; set; } = "";
    }

    public class ModelWithDeeplyNestedGenerics : IModel
    {
        public List<List<SimpleModel>> DeepList { get; set; } = [];
        public Dictionary<string, List<SimpleModel>> DictOfLists { get; set; } = [];
    }
}
