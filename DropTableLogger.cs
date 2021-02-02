using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nm1fiOutward.Drops
{
    /// <summary>
    /// <see cref="DropTableLogger"/> is used to simulate <see cref="DropTableAlteration"/>s on scene load.
    /// </summary>
    internal static class DropTableLogger
    {
        internal static IEnumerator LogAllCoro()
        {
            var merchants = Resources.FindObjectsOfTypeAll<Merchant>()
                .Where(merchant => merchant.gameObject?.scene != null)
                .Select(merchant => merchant.DropableInventory)
                .ToList();
            if (merchants.Any())
            {
                var coro = LogMerchantsCoro(merchants);
                while (coro.MoveNext())
                    yield return coro.Current;
            }

            var enemies = Resources.FindObjectsOfTypeAll<LootableOnDeath>()
                .Where(enemy => enemy.gameObject?.scene != null)
                .ToList();
            if (enemies.Any())
                foreach (var enemy in enemies)
                {
                    DropsPatcher.PatchLootOnDeath(enemy, true);
                    yield return null;
                }

             yield break;
        }

        private static IEnumerator LogMerchantsCoro(List<Dropable> merchants)
        {
            var alterations = DropTableAlterationCategory.GetAlterationsFor(DropTableAlteration.ItemSourceType.Merchant).ToList();
            var additions = DropTableAlterationCategory.GetAdditionalDropsFor(DropTableAlteration.ItemSourceType.Merchant).ToList();
            if (alterations.Any() || additions.Any())
            {
                DropsPlugin.Log($"Checking droptable alterations for {merchants.Count} merchants.");
                var altered = new List<DropableWrapper>();
                foreach (var dropable in merchants)
                {
                    if (DropsPatcher.PatchMerchantInventory(dropable, alterations, true) is DropableWrapper wrapped)
                    {
                        foreach (var addition in additions)
                            addition.AddAdditionalDroppersTo(wrapped);
                        if (wrapped.HasChanges)
                            altered.Add(wrapped);
                    }
                    yield return null; // process a single per loop
                }
                foreach (var dropableWrapper in altered)
                    if (dropableWrapper.Real.transform.parent.GetComponent<Merchant>() is Merchant merchant)
                        DropsPlugin.LogInfo("Would update droptables for"
                            + $" a merchant {merchant.ShopName} (UID={merchant.HolderUID}):"
                            + $"\n{dropableWrapper.ToInfoString()}");
            }
            yield break;
        }
    }
}
