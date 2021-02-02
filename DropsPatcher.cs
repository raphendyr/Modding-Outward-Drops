using HarmonyLib;
using SideLoader;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nm1fiOutward.Drops
{
    internal class DropsPatcher
    {
        internal static HashSet<string> UpdatedMerchantInventories = new HashSet<string>();

        internal static DropableWrapper PatchMerchantInventory(
            Dropable merchantInventory,
            IEnumerable<DropTableAlteration> alterations = null,
            bool simulate = false)
        {
            if (merchantInventory == null)
                return null;

            if (alterations == null)
                alterations = DropTableAlterationCategory.GetAlterationsFor(DropTableAlteration.ItemSourceType.Merchant);

            var merchantInventoryWrapper = new DropableWrapper(merchantInventory, simulate);
            foreach (var alteration in alterations)
                alteration.TryUpdate(merchantInventoryWrapper);
            if (merchantInventoryWrapper.HasChanges && merchantInventoryWrapper.UID is string uid && !string.IsNullOrEmpty(uid))
                UpdatedMerchantInventories.Add(uid);
            return merchantInventoryWrapper;
        }

        internal static void PatchTreasureChest(
            TreasureChest chest,
            IEnumerable<DropTableAlteration> alterations = null,
            bool simulate = false)
        {
            if (At.GetField(chest, "m_drops") is List<Dropable> dropables && dropables.Any())
            {
                if (alterations == null)
                    alterations = DropTableAlterationCategory.GetAlterationsFor(DropTableAlteration.ItemSourceType.LootContainer);
                if (alterations.Any())
                {
                    var dropableWrappers = dropables.Select((dropable, i) => new DropableWrapper(i, dropable, simulate)).ToList();
                    var changed = DropsPlugin.Config.Debug ? new List<DropableWrapper>() : null;
                    var anyChanges = false;
                    foreach (var dropable in dropableWrappers)
                        foreach (var update in alterations)
                            anyChanges |= update.TryUpdate(dropable);
                    if (simulate || DropsPlugin.Config.Debug)
                    {
                        var action = !anyChanges ? "Will not update" : simulate ? "Would update" : "Updated";
                        var builder = new StringBuilder($"{action} a treasure chest {chest.Name ?? "?"} (UID={chest.UID ?? ""}):\n");
                        foreach (var dropable in dropableWrappers)
                        {
                            if (dropable.HasChanges)
                                dropable.ToInfoString(1, builder);
                            else
                                builder.Append($"  {dropable}\n");
                        }
                        DropsPlugin.LogInfo(builder.ToString());
                    }
                    else if (anyChanges)
                        DropsPlugin.Log($"Updated a treasure chest {chest.Name} (UID={chest.UID})");
                }
            }
        }

        internal static IEnumerator PatchTreasureChestsCoro(List<TreasureChest> chests, bool simulate = false)
        {
            DropsPlugin.Log($"Starting droptable patcher for {chests.Count} TreasureCests.");
            var alterations = DropTableAlterationCategory.GetAlterationsFor(DropTableAlteration.ItemSourceType.LootContainer).ToList();
            foreach (var chest in chests)
            {
                PatchTreasureChest(chest, alterations, simulate);
                yield return null; // process single item per frame
            }
            DropsPlugin.Log("Droptable patcher for TreasureChests completed!");
            yield break;
        }

        internal static void PatchLootOnDeath(LootableOnDeath corpse, bool simulate = false)
        {
            if (At.GetField(corpse, "m_lootDroppers") is Dropable[] dropables
                && dropables != null
                && dropables.Length > 0)
            {
                var alterations = DropTableAlterationCategory.GetAlterationsFor(DropTableAlteration.ItemSourceType.EnemyOrMonster).ToList();
                if (alterations.Any())
                {
                    var dropableWrappers = dropables.Select((dropable, i) => new DropableWrapper(i, dropable, simulate)).ToList();
                    var anyChanges = false;
                    foreach (var dropper in dropableWrappers)
                        foreach (var alteration in alterations)
                            anyChanges |= alteration.TryUpdate(dropper);

                    if (simulate || DropsPlugin.Config.Debug)
                    {
                        var character = corpse.Character;
                        var action = !anyChanges ? "Will not update" : simulate ? "Would update" : "Updated";
                        var builder = new StringBuilder($"{action} droptables for a mob {character.Name} (UID={character.UID})\n");
                        foreach (var dropable in dropableWrappers)
                        {
                            if (dropable.HasChanges)
                                dropable.ToInfoString(1, builder);
                            else
                                builder.Append($"  {dropable}\n");
                        }
                        DropsPlugin.LogInfo(builder.ToString());
                    }
                    else if (anyChanges)
                    {
                        var character = corpse.Character;
                        DropsPlugin.Log($"Updated droptables for a mob {character.Name} (UID={character.UID})");
                    }
                }
            }
        }
    }

    namespace HarmonyPatches
    {
        [HarmonyPatch(typeof(Merchant), "Initialize")]
        public class Merchant_Initialize
        {
            [HarmonyPostfix]
            public static void Postfix(Merchant __instance)
            {
                var uid = __instance.DropableInventory?.UID;
                if (!string.IsNullOrEmpty(uid) && DropsPatcher.UpdatedMerchantInventories.Contains(uid))
                {
                    DropsPlugin.LogWarning($"Merchant.DropableInventory(UID={uid}) existed in UpdatedMerchantInventories after Merchant.Initialize()!");
                    DropsPatcher.UpdatedMerchantInventories.Remove(uid);
                }
            }
        }

        /// <summary>
        /// Patch <see cref="MerchantPouch.RefreshInventory"/> so we can update drop tables just before they are needed
        /// and add additional drops just after.
        /// </summary>
        [HarmonyPatch(typeof(MerchantPouch), nameof(MerchantPouch.RefreshInventory))]
        public static class MerchantPouch_RefreshInventory
        {
            /// <summary>
            /// In prefix we ensure that droptable updates have been applied or apply them now.
            /// </summary>
            [HarmonyPrefix]
            public static void Prefix(ref object[] __state, MerchantPouch __instance, double ___m_nextRefreshTime, Dropable _dropable)
            {
                if (PhotonNetwork.isNonMasterClientInRoom || !DropsPlugin.Config.Enabled)
                    return;

                if (!DropsPatcher.UpdatedMerchantInventories.Contains(_dropable.UID) // check that not yet patched
                    && DropTableAlterationCategory.GetAlterationsFor(DropTableAlteration.ItemSourceType.Merchant)
                    is IEnumerable<DropTableAlteration> alterations
                    && alterations.Any()
                    && DropsPatcher.PatchMerchantInventory(_dropable, alterations) is DropableWrapper wrapped) // apply alterations
                {
                    var merchant = __instance.Merchant;
                    var message = $"Updated droptables for a merchant {merchant.ShopName} (HolderUID={merchant.HolderUID})";
                    if (DropsPlugin.Config.Debug)
                        message += ":\n" + wrapped.ToInfoString().ToString();
                    DropsPlugin.Log(message);
                }

                __state = new object[] {
                    ___m_nextRefreshTime
                };
            }

            /// <summary>
            /// In postfix we apply additional drops. SideLoader ItemSources hook to this same place.
            /// </summary>
            [HarmonyPostfix]
            public static void Postfix(MerchantPouch __instance, object[] __state, double ___m_nextRefreshTime, Dropable _dropable)
            {
                if (PhotonNetwork.isNonMasterClientInRoom || !DropsPlugin.Config.Enabled)
                    return;

                // check if nextRefreshTime has changed, if not, then no drops were generated
                if (__state == null || (double)__state[0] == ___m_nextRefreshTime)
                    return;

                if (DropTableAlterationCategory.GetAdditionalDropsFor(DropTableAlteration.ItemSourceType.Merchant)
                    is IEnumerable<DropTableAlteration> additionalDrops
                    && additionalDrops.Any())
                {
                    var container = __instance.transform;
                    var anyChanges = false;
                    foreach (var alteration in additionalDrops)
                        anyChanges |= alteration.TryGenerateAdditionalItems(_dropable, container);
                    if (anyChanges)
                    {
                        var merchant = __instance.Merchant;
                        DropsPlugin.Log($"Applied additional drops for a merchant {merchant.ShopName} (UID={merchant.HolderUID})");
                    }
                }
            }
        }

        /// <summary>
        /// Patch <see cref="LootableOnDeath.OnDeath"/> to enable updating enemy drop inventories.
        /// </summary>
        [HarmonyPatch(typeof(LootableOnDeath), "OnDeath")]
        public class LootableOnDeath_OnDeath
        {
            [HarmonyPrefix]
            public static void Prefix(LootableOnDeath __instance, bool _loadedDead)
            {
                if (__instance.enabled
                    && !_loadedDead
                    && !PhotonNetwork.isNonMasterClientInRoom
                    && DropsPlugin.Config.Enabled)
                {
                    DropsPatcher.PatchLootOnDeath(__instance);
                }
            }
        }
    }
}
