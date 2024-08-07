﻿using GameCore.DataAccess;
using GameCore.Entities;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockTagCategories : ITagCategories
    {
        public List<TagCategory> TagCategories { get; set; } = new();
        public List<TagCategory> GetTagCategories()
        {
            return TagCategories;
        }
    }
}
