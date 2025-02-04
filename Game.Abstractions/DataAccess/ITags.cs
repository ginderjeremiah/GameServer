﻿using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface ITags
    {
        public IAsyncEnumerable<Tag> All();
        public IAsyncEnumerable<Tag> GetTags(IEnumerable<int> tagIds);
        public IAsyncEnumerable<Tag> GetTagsForItem(int itemId);
        public IAsyncEnumerable<Tag> GetTagsForItemMod(int itemModId);
    }
}
