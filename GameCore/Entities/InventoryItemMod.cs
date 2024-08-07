﻿using System.Text.Json.Serialization;

namespace GameCore.Entities
{
    public class InventoryItemMod
    {
        public int InventoryItemId { get; set; }
        public int ItemModId { get; set; }
        public int ItemSlotId { get; set; }

        [JsonIgnore]
        public virtual InventoryItem InventoryItem { get; set; }
        public virtual ItemMod ItemMod { get; set; }
        public virtual ItemSlot ItemSlot { get; set; }
    }
}
