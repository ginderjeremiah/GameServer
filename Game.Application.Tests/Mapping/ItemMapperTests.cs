using Game.Core;
using Game.Core.Attributes.Modifiers;
using Game.DataAccess.Mapping;
using Xunit;
using Entities = Game.Infrastructure.Entities;
using EntityItem = Game.Infrastructure.Entities.Item;
using EntityItemMod = Game.Infrastructure.Entities.ItemMod;

namespace Game.Application.Tests.Mapping
{
    /// <summary>
    /// Coverage for <see cref="ItemMapper"/>: the nullable <c>GrantedSkillId</c> round-trips from the entity
    /// to both the client-visible contract (<see cref="ItemMapper.ToContract"/>) and the lean battle domain
    /// model (<see cref="ItemMapper.ToCore"/>). The contract field drives the items reference-data version
    /// hash, and the core field is the id the battle assembly resolves a granted skill from. The ItemMod
    /// projections (<see cref="ItemMapper.ModToContract"/>/<see cref="ItemMapper.ModToCore"/>) get their own
    /// scalar-and-attribute round-trip coverage below.
    /// </summary>
    public class ItemMapperTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(7)]
        public void ToContract_RoundTripsGrantedSkillId(int? grantedSkillId)
        {
            var entity = NewItem(grantedSkillId);

            var contract = ItemMapper.ToContract(entity);

            Assert.Equal(grantedSkillId, contract.GrantedSkillId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(7)]
        public void ToCore_RoundTripsGrantedSkillId(int? grantedSkillId)
        {
            var entity = NewItem(grantedSkillId);

            var core = ItemMapper.ToCore(entity);

            Assert.Equal(grantedSkillId, core.GrantedSkillId);
        }

        [Theory]
        [InlineData(null, 0)]
        [InlineData(0, 1)]
        [InlineData(4, 7)]
        public void ToContract_RoundTripsRequiredProficiency(int? requiredProficiencyId, int requiredProficiencyLevel)
        {
            var entity = NewItem(requiredProficiencyId: requiredProficiencyId, requiredProficiencyLevel: requiredProficiencyLevel);

            var contract = ItemMapper.ToContract(entity);

            Assert.Equal(requiredProficiencyId, contract.RequiredProficiencyId);
            Assert.Equal(requiredProficiencyLevel, contract.RequiredProficiencyLevel);
        }

        [Theory]
        [InlineData(null, 0)]
        [InlineData(0, 1)]
        [InlineData(4, 7)]
        public void ToCore_RoundTripsRequiredProficiency(int? requiredProficiencyId, int requiredProficiencyLevel)
        {
            var entity = NewItem(requiredProficiencyId: requiredProficiencyId, requiredProficiencyLevel: requiredProficiencyLevel);

            var core = ItemMapper.ToCore(entity);

            Assert.Equal(requiredProficiencyId, core.RequiredProficiencyId);
            Assert.Equal(requiredProficiencyLevel, core.RequiredProficiencyLevel);
        }

        [Theory]
        [InlineData(null)]
        [InlineData((int)EDamageType.Sword)]
        [InlineData((int)EDamageType.Unarmed)]
        public void ToContract_RoundTripsWeaponType(int? weaponType)
        {
            var entity = NewItem(weaponType: weaponType);

            var contract = ItemMapper.ToContract(entity);

            Assert.Equal((EDamageType?)weaponType, contract.WeaponType);
        }

        [Theory]
        [InlineData(null)]
        [InlineData((int)EDamageType.Sword)]
        [InlineData((int)EDamageType.Unarmed)]
        public void ToCore_RoundTripsWeaponType(int? weaponType)
        {
            var entity = NewItem(weaponType: weaponType);

            var core = ItemMapper.ToCore(entity);

            Assert.Equal((EDamageType?)weaponType, core.WeaponType);
        }

        [Fact]
        public void ToContract_OrdersChildCollectionsRegardlessOfEntityOrder()
        {
            var entity = NewItem();
            // Deliberately shuffled (descending) so a stable ordering isn't a coincidence of insertion order.
            entity.ItemAttributes =
            [
                new Entities.ItemAttribute { ItemId = 0, AttributeId = (int)EAttribute.Intellect, Amount = 1m },
                new Entities.ItemAttribute { ItemId = 0, AttributeId = (int)EAttribute.Strength, Amount = 2m },
            ];
            entity.ItemModSlots =
            [
                new Entities.ItemModSlot { Id = 9, ItemId = 0, ItemModSlotTypeId = (int)EItemModType.Prefix },
                new Entities.ItemModSlot { Id = 2, ItemId = 0, ItemModSlotTypeId = (int)EItemModType.Suffix },
            ];
            entity.Tags = [new Entities.Tag { Id = 5, Name = "b" }, new Entities.Tag { Id = 1, Name = "a" }];

            var contract = ItemMapper.ToContract(entity);

            Assert.Equal([EAttribute.Strength, EAttribute.Intellect], contract.Attributes.Select(a => a.AttributeId));
            Assert.Equal([2, 9], contract.ModSlots.Select(s => s.Id));
            Assert.Equal([1, 5], contract.Tags);
        }

        [Fact]
        public void ToCore_OrdersChildCollectionsRegardlessOfEntityOrder()
        {
            var entity = NewItem();
            entity.ItemAttributes =
            [
                new Entities.ItemAttribute { ItemId = 0, AttributeId = (int)EAttribute.Intellect, Amount = 1m },
                new Entities.ItemAttribute { ItemId = 0, AttributeId = (int)EAttribute.Strength, Amount = 2m },
            ];
            entity.ItemModSlots =
            [
                new Entities.ItemModSlot { Id = 9, ItemId = 0, ItemModSlotTypeId = (int)EItemModType.Prefix },
                new Entities.ItemModSlot { Id = 2, ItemId = 0, ItemModSlotTypeId = (int)EItemModType.Suffix },
            ];

            var core = ItemMapper.ToCore(entity);

            Assert.Equal([EAttribute.Strength, EAttribute.Intellect], core.Attributes.Select(a => a.Attribute));
            Assert.Equal([2, 9], core.ModSlots.Select(s => s.Id));
        }

        [Fact]
        public void ModToContract_RoundTripsScalarFieldsAndTags()
        {
            var retiredAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var entity = NewItemMod(EItemModType.Prefix, ERarity.Rare, tagIds: [2, 5], retiredAt: retiredAt);

            var contract = ItemMapper.ModToContract(entity);

            Assert.Equal(0, contract.Id);
            Assert.Equal("Sharp", contract.Name);
            Assert.Equal("Adds an edge.", contract.Description);
            Assert.Equal(EItemModType.Prefix, contract.ItemModTypeId);
            Assert.Equal(ERarity.Rare, contract.RarityId);
            Assert.Equal([2, 5], contract.Tags);
            Assert.Equal(retiredAt, contract.RetiredAt);
        }

        [Fact]
        public void ModToContract_PreservesNullRetiredAtAndMapsAttributes()
        {
            var entity = NewItemMod(attributes: [(EAttribute.Strength, 5m), (EAttribute.Agility, 3m)]);

            var contract = ItemMapper.ModToContract(entity);

            Assert.Null(contract.RetiredAt);
            Assert.Collection(contract.Attributes,
                a => { Assert.Equal(EAttribute.Strength, a.AttributeId); Assert.Equal(5m, a.Amount); },
                a => { Assert.Equal(EAttribute.Agility, a.AttributeId); Assert.Equal(3m, a.Amount); });
        }

        [Fact]
        public void ModToCore_MapsScalarFieldsAndAttributes()
        {
            var entity = NewItemMod(EItemModType.Suffix, ERarity.Legendary,
                attributes: [(EAttribute.Strength, 5m)]);

            var core = ItemMapper.ModToCore(entity);

            Assert.Equal(0, core.Id);
            Assert.Equal("Sharp", core.Name);
            Assert.Equal("Adds an edge.", core.Description);
            Assert.Equal(EItemModType.Suffix, core.Type);
            Assert.Equal(ERarity.Legendary, core.Rarity);
            var attribute = Assert.Single(core.Attributes);
            Assert.Equal(EAttribute.Strength, attribute.Attribute);
            Assert.Equal(5d, attribute.Amount);
            Assert.Equal(EModifierType.Additive, attribute.Type);
            Assert.Equal(EAttributeModifierSource.ItemMod, attribute.Source);
        }

        private static EntityItemMod NewItemMod(
            EItemModType type = EItemModType.Component,
            ERarity rarity = ERarity.Common,
            List<(EAttribute Attribute, decimal Amount)>? attributes = null,
            List<int>? tagIds = null,
            DateTime? retiredAt = null) => new()
            {
                Id = 0,
                Name = "Sharp",
                Description = "Adds an edge.",
                ItemModTypeId = (int)type,
                RarityId = (int)rarity,
                DesignerNotes = "designer intent",
                RetiredAt = retiredAt,
                ItemModAttributes = (attributes ?? []).Select(a => new Entities.ItemModAttribute
                {
                    ItemModId = 0,
                    AttributeId = (int)a.Attribute,
                    Amount = a.Amount,
                }).ToList(),
                Tags = (tagIds ?? []).Select(id => new Entities.Tag { Id = id, Name = "tag" }).ToList(),
                UnlockedMods = [],
            };

        [Fact]
        public void ToContract_RoundTripsDesignerNotes()
        {
            var entity = NewItem();
            entity.DesignerNotes = "why this item exists";

            // Authoring-only metadata rides the contract only; the lean Core.Items.Item has no such field.
            Assert.Equal("why this item exists", ItemMapper.ToContract(entity).DesignerNotes);
        }

        [Fact]
        public void ModToContract_RoundTripsDesignerNotes()
        {
            var entity = NewItemMod();
            entity.DesignerNotes = "why this mod exists";

            // Authoring-only metadata rides the ItemMod contract only; the lean Core.Items.ItemMod has no such field.
            Assert.Equal("why this mod exists", ItemMapper.ModToContract(entity).DesignerNotes);
        }

        private static EntityItem NewItem(int? grantedSkillId = null, int? requiredProficiencyId = null,
            int requiredProficiencyLevel = 0, int? weaponType = null) => new()
            {
                Id = 0,
                Name = "Test",
                Description = "",
                IconPath = "",
                ItemCategoryId = (int)EItemCategory.Weapon,
                RarityId = (int)ERarity.Common,
                GrantedSkillId = grantedSkillId,
                WeaponType = weaponType,
                RequiredProficiencyId = requiredProficiencyId,
                RequiredProficiencyLevel = requiredProficiencyLevel,
                DesignerNotes = "designer intent",
                ItemAttributes = [],
                ItemModSlots = [],
                Tags = [],
            };
    }
}
