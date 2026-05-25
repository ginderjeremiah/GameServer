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
}
