using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Application.Services;
using Game.Core;
using Game.Core.Attributes.Modifiers;
using Game.Core.Players;
using Game.Core.TestInfrastructure.Builders;
using Game.DataAccess;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace Game.Application.Tests.Services
{
    [Collection("Integration")]
    public class PlayerServiceIntegrationTests : ApplicationIntegrationTestBase
    {
        public PlayerServiceIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task EquipItem_ProficiencyGateNotMet_RejectsWithoutEquipping()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var proficiency = await TestDataSeeder.CreateProficiencyAsync(context, "Fire Magic", maxLevel: 10);
            // The item requires Fire Magic level 5; the player has only reached level 4.
            var item = await TestDataSeeder.CreateItemAsync(
                context, requiredProficiencyId: proficiency.Id, requiredProficiencyLevel: 5);
            await TestDataSeeder.LinkItemToPlayerAsync(context, playerEntity.Id, item.Id);
            await TestDataSeeder.AddPlayerProficiencyAsync(context, playerEntity.Id, proficiency.Id, level: 4);
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var (equipped, _) = await playerService.EquipItem(player, item.Id, EEquipmentSlot.WeaponSlot);

            Assert.False(equipped);
            Assert.DoesNotContain(player.Inventory.EquipmentSlots, s => s.ItemId == item.Id);
        }

        [Fact]
        public async Task EquipItem_ProficiencyGateMet_Equips()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var proficiency = await TestDataSeeder.CreateProficiencyAsync(context, "Fire Magic", maxLevel: 10);
            var item = await TestDataSeeder.CreateItemAsync(
                context, requiredProficiencyId: proficiency.Id, requiredProficiencyLevel: 5);
            await TestDataSeeder.LinkItemToPlayerAsync(context, playerEntity.Id, item.Id);
            // The player has reached exactly the required level, so the gate is satisfied.
            await TestDataSeeder.AddPlayerProficiencyAsync(context, playerEntity.Id, proficiency.Id, level: 5);
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var (equipped, proficiencyLevels) = await playerService.EquipItem(player, item.Id, EEquipmentSlot.WeaponSlot);

            Assert.True(equipped);
            Assert.Contains(player.Inventory.EquipmentSlots, s => s.ItemId == item.Id);
            // The returned levels are the same read the gear gate used, so callers (e.g. EquipItem's socket
            // command computing PlayerRating) can reuse them instead of re-reading the proficiency hash (#1729).
            Assert.Contains(proficiencyLevels, p => p.ProficiencyId == proficiency.Id && p.Level == 5);
        }

        [Fact]
        public async Task TryUpdateAttributes_ValidUpdate_ReturnsTrue()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            // Give the player 6 unspent stat points
            playerEntity.StatPointsGained = 106;
            await context.SaveChangesAsync(CancellationToken);

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();

            var updates = new List<SimpleAttributeUpdate>
            {
                new(EAttribute.Strength, Amount: 3),
            };

            var result = await playerService.TryUpdateAttributes(player, updates);

            Assert.True(result);
        }

        [Fact]
        public async Task TryUpdateAttributes_SpendMoreThanAvailable_ReturnsFalse()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();

            var updates = new List<SimpleAttributeUpdate>
            {
                new(EAttribute.Strength, Amount: 999),
            };

            var result = await playerService.TryUpdateAttributes(player, updates);

            Assert.False(result);
        }

        [Fact]
        public async Task LoadPlayer_ExistingPlayer_LoadsFromDbThenCache()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();

            var player1 = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player1);
            Assert.Equal(playerEntity.Name, player1.Name);

            var player2 = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player2);
            Assert.Equal(player1.Name, player2.Name);
        }

        [Fact]
        public async Task LoadPlayer_NonExistent_ReturnsNull()
        {
            using var scope = CreateScope();
            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();

            var player = await playerService.LoadPlayer(99999);

            Assert.Null(player);
        }

        [Fact]
        public async Task ApplyMod_EquippedItem_AddsModAttributesToEquippedModifiers()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // A weapon (base Strength +5) with one Prefix mod slot, equipped in the weapon slot.
            var item = await TestDataSeeder.CreateItemAsync(context);
            var modSlot = await TestDataSeeder.AddItemModSlotAsync(context, item.Id);
            await TestDataSeeder.LinkItemToPlayerAsync(context, playerEntity.Id, item.Id, EEquipmentSlot.WeaponSlot);

            // A Prefix mod granting Dexterity +7, unlocked for the player.
            var mod = await TestDataSeeder.CreateItemModAsync(context, attributeId: EAttribute.Dexterity, attributeAmount: 7m);
            await TestDataSeeder.LinkModToPlayerAsync(context, playerEntity.Id, mod.Id);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            // The client speaks the slot's DB Id, so apply resolves the target by Id (not ordinal).
            var applied = await playerService.ApplyMod(player, item.Id, mod.Id, modSlot.Id);

            Assert.True(applied);
            var modifiers = player.Inventory.GetEquippedAttributeModifiers().ToList();
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Strength && m.Amount == 5.0 && m.Source == EAttributeModifierSource.Item);
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Dexterity && m.Amount == 7.0 && m.Source == EAttributeModifierSource.ItemMod);
        }

        [Fact]
        public async Task ApplyMod_SecondItemSlotIdNotZero_ResolvesById()
        {
            // Regression for #316: the apply path used to match the slot by its per-item Index, which
            // only equals the slot's DB Id for the first authored slot (Id 0). A second item's slot has
            // Id != 0, so a client request carrying that Id silently found no slot. Authoring a throwaway
            // slotted item first pushes the real item's slot to Id 1, exercising the masked case.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // First authored slotted item consumes slot Id 0.
            var decoyItem = await TestDataSeeder.CreateItemAsync(context);
            await TestDataSeeder.AddItemModSlotAsync(context, decoyItem.Id);

            // The item under test: its only slot is authored second, so it gets a non-zero Id.
            var item = await TestDataSeeder.CreateItemAsync(context);
            var modSlot = await TestDataSeeder.AddItemModSlotAsync(context, item.Id);
            await TestDataSeeder.LinkItemToPlayerAsync(context, playerEntity.Id, item.Id, EEquipmentSlot.WeaponSlot);

            var mod = await TestDataSeeder.CreateItemModAsync(context, attributeId: EAttribute.Dexterity, attributeAmount: 7m);
            await TestDataSeeder.LinkModToPlayerAsync(context, playerEntity.Id, mod.Id);
            Assert.NotEqual(0, modSlot.Id);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var applied = await playerService.ApplyMod(player, item.Id, mod.Id, modSlot.Id);

            Assert.True(applied);
            var modifiers = player.Inventory.GetEquippedAttributeModifiers().ToList();
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Dexterity && m.Amount == 7.0 && m.Source == EAttributeModifierSource.ItemMod);
        }

        [Fact]
        public async Task ApplyMod_NonexistentMod_ReturnsFalse()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            // A mod id far beyond any seeded catalog entry: ApplyMod must reject it (returning false)
            // rather than throwing when it resolves the mod for application.
            var applied = await playerService.ApplyMod(player, itemId: 0, itemModId: 99999, itemModSlotId: 0);

            Assert.False(applied);
        }

        [Fact]
        public async Task LoadPlayer_PersistedAppliedMod_IncludesModAttributesInEquippedModifiers()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // A weapon (base Strength +5) with one Prefix mod slot, equipped in the weapon slot.
            var item = await TestDataSeeder.CreateItemAsync(context);
            var modSlot = await TestDataSeeder.AddItemModSlotAsync(context, item.Id);
            await TestDataSeeder.LinkItemToPlayerAsync(context, playerEntity.Id, item.Id, EEquipmentSlot.WeaponSlot);

            // A Prefix mod granting Dexterity +7, unlocked for the player.
            var mod = await TestDataSeeder.CreateItemModAsync(context, attributeId: EAttribute.Dexterity, attributeAmount: 7m);
            await TestDataSeeder.LinkModToPlayerAsync(context, playerEntity.Id, mod.Id);

            // Persist the applied mod as if it had been applied in a previous session, so the
            // player is loaded fresh from the DB with it already in place (the #90 scenario).
            await TestDataSeeder.ApplyModToItemAsync(context, playerEntity.Id, item.Id, modSlot.Id, mod.Id);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var modifiers = player.Inventory.GetEquippedAttributeModifiers().ToList();
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Strength && m.Amount == 5.0 && m.Source == EAttributeModifierSource.Item);
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Dexterity && m.Amount == 7.0 && m.Source == EAttributeModifierSource.ItemMod);
        }

        [Fact]
        public async Task LoadPlayer_ResolvesReferenceDataFromCache()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Reference data (skill, item with attribute + mod slot, mod) authored independently of
            // the player; PlayerRepository must stitch these in from the in-memory catalogs.
            var skill = await TestDataSeeder.CreateSkillAsync(context, name: "Fireball", baseDamage: 12m, cooldownMs: 1500);
            var item = await TestDataSeeder.CreateItemAsync(context, name: "Sword", attributeId: EAttribute.Strength, attributeAmount: 5m);
            var modSlot = await TestDataSeeder.AddItemModSlotAsync(context, item.Id);
            var mod = await TestDataSeeder.CreateItemModAsync(context, attributeId: EAttribute.Dexterity, attributeAmount: 7m);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id, selected: true);
            await TestDataSeeder.LinkItemToPlayerAsync(context, playerEntity.Id, item.Id, EEquipmentSlot.WeaponSlot);
            await TestDataSeeder.LinkModToPlayerAsync(context, playerEntity.Id, mod.Id);
            await TestDataSeeder.ApplyModToItemAsync(context, playerEntity.Id, item.Id, modSlot.Id, mod.Id);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            // Skill definition resolved from the cached catalog
            var loadedSkill = Assert.Single(player.Skills);
            Assert.Equal("Fireball", loadedSkill.Name);
            Assert.Equal(12.0, loadedSkill.BaseDamage);
            Assert.Equal(1500, loadedSkill.CooldownMs);
            Assert.NotEmpty(loadedSkill.DamageMultipliers);
            // Skills and SelectedSkills share the same resolved instance (immutable template data)
            var selectedSkill = Assert.Single(player.SelectedSkills);
            Assert.Same(loadedSkill, selectedSkill);

            // Item definition (with its attributes and mod slots) resolved from the cached catalog
            var unlocked = Assert.Single(player.Inventory.UnlockedItems);
            Assert.Equal("Sword", unlocked.Item.Name);
            Assert.Contains(unlocked.Item.Attributes, a => a.Attribute == EAttribute.Strength && a.Amount == 5.0);
            Assert.NotEmpty(unlocked.Item.ModSlots);

            // Applied mod resolved with its attributes, and folded into the equipped modifiers
            var appliedMod = Assert.Single(unlocked.AppliedMods);
            Assert.Equal(mod.Id, appliedMod.ItemModId);
            Assert.Contains(appliedMod.ItemMod.Attributes, a => a.Attribute == EAttribute.Dexterity && a.Amount == 7.0);

            var modifiers = player.Inventory.GetEquippedAttributeModifiers().ToList();
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Strength && m.Amount == 5.0 && m.Source == EAttributeModifierSource.Item);
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Dexterity && m.Amount == 7.0 && m.Source == EAttributeModifierSource.ItemMod);
        }

        [Fact]
        public async Task LoadPlayer_OrdersSelectedSkillsByOrderThenSkillId()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // CreateSkillAsync assigns sequential ids, so these five skills have ascending ids.
            var skillA = await TestDataSeeder.CreateSkillAsync(context, name: "A");
            var skillB = await TestDataSeeder.CreateSkillAsync(context, name: "B");
            var skillC = await TestDataSeeder.CreateSkillAsync(context, name: "C");
            var skillD = await TestDataSeeder.CreateSkillAsync(context, name: "D");
            var skillE = await TestDataSeeder.CreateSkillAsync(context, name: "E");

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // Linked in a deliberately scrambled order so the assertion proves the mapper sorts rather
            // than echoing insertion order. skillA and skillC share Order = 2 to exercise the SkillId
            // tie-break (which also covers legacy rows that all default to Order = 0).
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skillC.Id, selected: true, order: 2);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skillA.Id, selected: true, order: 2);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skillD.Id, selected: true, order: 0);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skillB.Id, selected: true, order: 1);
            // An unlocked-but-unselected skill must stay out of the equipped loadout.
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skillE.Id, selected: false, order: 0);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            // Equipped set is a stable total order by (Order, SkillId): D (0), B (1), then A and C
            // tie at 2 → A before C because its SkillId is lower.
            Assert.Equal(
                new[] { skillD.Id, skillB.Id, skillA.Id, skillC.Id },
                player.SelectedSkills.Select(skill => skill.Id));

            // The unselected skill is still unlocked but never equipped.
            Assert.Contains(player.Skills, skill => skill.Id == skillE.Id);
            Assert.DoesNotContain(player.SelectedSkills, skill => skill.Id == skillE.Id);
        }

        [Fact]
        public async Task SetFavorite_PersistsFavoriteFlagToDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var item = await TestDataSeeder.CreateItemAsync(context);
            await TestDataSeeder.LinkItemToPlayerAsync(context, playerEntity.Id, item.Id);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var success = await playerService.SetFavorite(player, item.Id, favorite: true);
            Assert.True(success);

            // The write-behind queue is drained by the synchronizer; run it directly (the hosted
            // worker is disabled in the test harness) so the change reaches the database.
            await DrainPlayerUpdateQueue(scope.ServiceProvider);

            // Read the row from a fresh context (the cache-flush equivalent): the favorite flag must
            // have been persisted to the database, not just the cached domain player.
            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await verifyContext.UnlockedItems
                .FirstOrDefaultAsync(ui => ui.PlayerId == playerEntity.Id && ui.ItemId == item.Id, CancellationToken);
            Assert.NotNull(persisted);
            Assert.True(persisted.Favorite);
        }

        [Fact]
        public async Task SetSelectedSkills_PersistsLoadoutToDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // Three unlocked-but-unequipped skills for the player to choose a loadout from.
            var skill0 = await TestDataSeeder.CreateSkillAsync(context, name: "S0");
            var skill1 = await TestDataSeeder.CreateSkillAsync(context, name: "S1");
            var skill2 = await TestDataSeeder.CreateSkillAsync(context, name: "S2");
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill0.Id, selected: false);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill1.Id, selected: false);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill2.Id, selected: false);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var success = await playerService.SetSelectedSkills(player, [skill2.Id, skill0.Id]);
            Assert.True(success);

            // Drain the write-behind queue (the hosted worker is disabled in the harness) so the change
            // reaches the database, then read the rows from a fresh context to confirm persistence.
            await DrainPlayerUpdateQueue(scope.ServiceProvider);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.PlayerSkills
                .Where(ps => ps.PlayerId == playerEntity.Id)
                .ToListAsync(CancellationToken);

            Assert.Equal(3, rows.Count);
            // The chosen loadout is persisted in order; the unchosen skill stays unlocked but unequipped.
            Assert.Single(rows, ps => ps.SkillId == skill2.Id && ps.Selected && ps.Order == 0);
            Assert.Single(rows, ps => ps.SkillId == skill0.Id && ps.Selected && ps.Order == 1);
            Assert.Single(rows, ps => ps.SkillId == skill1.Id && !ps.Selected && ps.Order == 0);
        }

        [Fact]
        public async Task SetSelectedSkills_SkillNotUnlocked_ReturnsFalse()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var skill = await TestDataSeeder.CreateSkillAsync(context);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            // The skill exists in the catalog but the player has not unlocked it, so the loadout is rejected.
            var success = await playerService.SetSelectedSkills(player, [skill.Id]);

            Assert.False(success);
            Assert.Empty(player.SelectedSkills);
        }

        [Fact]
        public async Task EquipItem_PersistsEquippedSlotToDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            // A Weapon-category item (CreateItemAsync default), unlocked but not yet equipped.
            var item = await TestDataSeeder.CreateItemAsync(context);
            await TestDataSeeder.LinkItemToPlayerAsync(context, playerEntity.Id, item.Id);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var (success, _) = await playerService.EquipItem(player, item.Id, EEquipmentSlot.WeaponSlot);
            Assert.True(success);

            // Drain the write-behind queue (the hosted worker is disabled in the harness) so the change
            // reaches the database, then read the row from a fresh context to confirm persistence.
            await DrainPlayerUpdateQueue(scope.ServiceProvider);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await verifyContext.UnlockedItems
                .FirstOrDefaultAsync(ui => ui.PlayerId == playerEntity.Id && ui.ItemId == item.Id, CancellationToken);
            Assert.NotNull(persisted);
            Assert.Equal((int)EEquipmentSlot.WeaponSlot, persisted.EquipmentSlotId);
        }

        [Fact]
        public async Task EquipItem_ItemNotUnlocked_ReturnsFalse()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            // The item exists in the catalog but the player has not unlocked it.
            var item = await TestDataSeeder.CreateItemAsync(context);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var (success, _) = await playerService.EquipItem(player, item.Id, EEquipmentSlot.WeaponSlot);

            Assert.False(success);
        }

        [Fact]
        public async Task UnequipItem_PersistsClearedSlotToDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            // A Weapon-category item unlocked and already equipped in the weapon slot.
            var item = await TestDataSeeder.CreateItemAsync(context);
            await TestDataSeeder.LinkItemToPlayerAsync(context, playerEntity.Id, item.Id, EEquipmentSlot.WeaponSlot);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var success = await playerService.UnequipItem(player, EEquipmentSlot.WeaponSlot);
            Assert.True(success);

            await DrainPlayerUpdateQueue(scope.ServiceProvider);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await verifyContext.UnlockedItems
                .FirstOrDefaultAsync(ui => ui.PlayerId == playerEntity.Id && ui.ItemId == item.Id, CancellationToken);
            Assert.NotNull(persisted);
            // The slot was cleared, so the item is unlocked but no longer equipped.
            Assert.Null(persisted.EquipmentSlotId);
        }

        [Fact]
        public async Task UnequipItem_EmptySlot_ReturnsFalse()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            // Nothing is equipped in the weapon slot, so there is nothing to unequip.
            var success = await playerService.UnequipItem(player, EEquipmentSlot.WeaponSlot);

            Assert.False(success);
        }

        [Fact]
        public async Task RemoveMod_PersistsRemovalToDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // A weapon with one Prefix mod slot, equipped in the weapon slot.
            var item = await TestDataSeeder.CreateItemAsync(context);
            var modSlot = await TestDataSeeder.AddItemModSlotAsync(context, item.Id);
            await TestDataSeeder.LinkItemToPlayerAsync(context, playerEntity.Id, item.Id, EEquipmentSlot.WeaponSlot);

            var mod = await TestDataSeeder.CreateItemModAsync(context, attributeId: EAttribute.Dexterity, attributeAmount: 7m);
            await TestDataSeeder.LinkModToPlayerAsync(context, playerEntity.Id, mod.Id);

            // Persist the applied mod as if it had been applied in a previous session, so the player
            // loads with it already in place and RemoveMod has something to remove (the inverse of
            // the LoadPlayer_PersistedAppliedMod scenario).
            // AppliedMod.ItemModSlotId is a FK to ItemModSlot.Id, so the persisted row, the RemoveMod
            // argument (matched against the loaded AppliedModSlot.ItemModSlotId), and the verify query
            // all key off modSlot.Id — the single identifier the apply/remove path resolves by (#316).
            await TestDataSeeder.ApplyModToItemAsync(context, playerEntity.Id, item.Id, modSlot.Id, mod.Id);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var success = await playerService.RemoveMod(player, item.Id, modSlot.Id);
            Assert.True(success);

            await DrainPlayerUpdateQueue(scope.ServiceProvider);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var remaining = await verifyContext.AppliedMods
                .AnyAsync(am => am.PlayerId == playerEntity.Id && am.ItemId == item.Id && am.ItemModSlotId == modSlot.Id, CancellationToken);
            Assert.False(remaining);
        }

        [Fact]
        public async Task RemoveMod_NoAppliedMod_ReturnsFalse()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var item = await TestDataSeeder.CreateItemAsync(context);
            await TestDataSeeder.LinkItemToPlayerAsync(context, playerEntity.Id, item.Id);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            // The item is unlocked but has no mod applied in slot 0, so there is nothing to remove.
            var success = await playerService.RemoveMod(player, item.Id, itemModSlotId: 0);

            Assert.False(success);
        }

        [Fact]
        public async Task SaveLogPreferences_PersistsPreferencesToDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // Reference data was seeded directly; reload the caches so LoadPlayer resolves it (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var preferences = new List<LogPreference>
            {
                new() { LogType = ELogType.Damage, Enabled = false },
                new() { LogType = ELogType.Exp, Enabled = true },
            };

            await playerService.SaveLogPreferences(player, preferences);

            await DrainPlayerUpdateQueue(scope.ServiceProvider);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.LogPreferences
                .Where(lp => lp.PlayerId == playerEntity.Id)
                .ToListAsync(CancellationToken);

            Assert.Single(rows, lp => lp.LogTypeId == (int)ELogType.Damage && !lp.Enabled);
            Assert.Single(rows, lp => lp.LogTypeId == (int)ELogType.Exp && lp.Enabled);
        }

        [Fact]
        public async Task SaveLogPreferences_NoValuesChanged_DoesNotEnqueueWriteBehindEvents()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            await ReloadReferenceCachesAsync();

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var preferences = new List<LogPreference>
            {
                new() { LogType = ELogType.Damage, Enabled = false },
                new() { LogType = ELogType.Exp, Enabled = true },
            };

            await playerService.SaveLogPreferences(player, preferences);
            await DrainPlayerUpdateQueue(scope.ServiceProvider);

            var options = ConfigurationOptions.Parse(Containers.PubSubConnectionString);
            using var multiplexer = await ConnectionMultiplexer.ConnectAsync(options);
            var db = multiplexer.GetDatabase();

            // Re-submitting the same values is a no-op: no LogPreferenceChangedEvent should be raised or saved.
            await playerService.SaveLogPreferences(player, preferences);

            Assert.Equal(0, await db.ListLengthAsync(Constants.PUBSUB_PLAYER_QUEUE));
        }

        private async Task DrainPlayerUpdateQueue(IServiceProvider services)
        {
            var pubsub = services.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(
                services, pubsub, NullLogger<DataProviderSynchronizer>.Instance, PlayerUpdateRetryPolicy.Default);
            await synchronizer.ProcessQueue(pubsub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE));
        }

        private record SimpleAttributeUpdate(EAttribute Attribute, int Amount) : IAttributeUpdate;
    }
}
