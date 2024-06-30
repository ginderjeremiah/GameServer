using GameCore.DataAccess;
using GameCore.Entities;
using GameCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories
{
    internal class Tags : BaseRepository, ITags
    {
        public Tags(IDatabaseService database) : base(database) { }

        public IQueryable<Tag> AllTags()
        {
            return Database.Tags;
        }

        public IQueryable<Tag> TagsForItem(int itemId)
        {
            return Database.Items
                .Include(i => i.Tags)
                .Where(i => i.Id == itemId)
                .SelectMany(i => i.Tags);
        }

        public IQueryable<Tag> TagsForItemMod(int itemModId)
        {
            return Database.ItemMods
                .Include(im => im.Tags)
                .Where(im => im.Id == itemModId)
                .SelectMany(i => i.Tags);
        }
    }
}
