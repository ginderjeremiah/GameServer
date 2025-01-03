﻿using System.Text.Json.Serialization;

namespace Game.Core.Entities
{
    public partial class Item : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ItemCategoryId { get; set; }
        public string IconPath { get; set; }

        public virtual List<ItemAttribute> ItemAttributes { get; set; }
        public virtual ItemCategory ItemCategory { get; set; }
        public virtual List<ItemModSlot> ItemModSlots { get; set; }
        public virtual List<Tag> Tags { get; set; }
        [JsonIgnore]
        public virtual List<EnemyDrop> EnemyDrops { get; set; }
        [JsonIgnore]
        public virtual List<ZoneDrop> ZoneDrops { get; set; }
        [JsonIgnore]
        public virtual List<InventoryItem> InventoryItems { get; set; }
    }
}
