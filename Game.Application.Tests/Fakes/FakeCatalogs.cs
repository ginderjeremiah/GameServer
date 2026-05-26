using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;

namespace Game.Application.Tests.Fakes
{
    /// <summary>
    /// Fake item catalog that returns entity items by ID from a provided list.
    /// </summary>
    internal class FakeItems(List<Item>? items = null) : IItems
    {
        private readonly List<Item> _items = items ?? [];

        public void InvalidateCache() { }
        public List<Item> All(bool refreshCache = false) => _items;
        public Item? LookupItem(int itemId) => _items.Count > itemId ? _items[itemId] : null;
        public Item GetItem(int itemId) => _items[itemId];
    }

    /// <summary>
    /// Fake item mod catalog that returns entity item mods by ID from a provided list.
    /// </summary>
    internal class FakeItemMods(List<ItemMod>? mods = null) : IItemMods
    {
        private readonly List<ItemMod> _mods = mods ?? [];

        public void InvalidateCache() { }
        public List<ItemMod> All(bool refreshCache = false) => _mods;
        public ItemMod? LookupItemMod(int itemModId) => _mods.Count > itemModId ? _mods[itemModId] : null;
        public ItemMod GetItemMod(int itemModId) => _mods[itemModId];
        public Dictionary<int, IEnumerable<ItemMod>> GetModsForItemByType(int itemId) => [];
    }

    /// <summary>
    /// Fake skill catalog that returns entity skills by ID from a provided list.
    /// </summary>
    internal class FakeSkills(List<Skill>? skills = null) : ISkills
    {
        private readonly List<Skill> _skills = skills ?? [];

        public void InvalidateCache() { }
        public List<Skill> AllSkills(bool refreshCache = false) => _skills;
        public Skill? LookupSkill(int skillId) => _skills.Count > skillId ? _skills[skillId] : null;
        public Skill GetSkill(int skillId) => _skills[skillId];
        public Task SaveSkillsAsync(List<int> skillIds) => Task.CompletedTask;
    }
}
