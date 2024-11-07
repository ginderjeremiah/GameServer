﻿using System.Text.Json.Serialization;

namespace Game.Core.Entities
{
    public partial class InventoryItemMod
    {
        public int InventoryItemId { get; set; }
        public int ItemModId { get; set; }
        public int ItemModSlotId { get; set; }

        [JsonIgnore]
        public virtual InventoryItem InventoryItem { get; set; }
        public virtual ItemMod ItemMod { get; set; }
        public virtual ItemModSlot ItemModSlot { get; set; }
    }
}