﻿namespace GameCore.Entities
{
    public class EquipmentSlot
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int ItemCategoryId { get; set; }

        public virtual ItemCategory ItemCategory { get; set; }
    }
}
