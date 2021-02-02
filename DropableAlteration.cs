using SideLoader;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;

namespace Nm1fiOutward.Drops
{
    // Abstract classes

    [SL_Serialized]
    public abstract class DropableAlteration : ITemplateListItem
    {
        public virtual void ApplyActualTemplate() { }

        public abstract IList<string> Validate();
    }

    public abstract class ItemDropperAlteration : DropableAlteration
    {
        [XmlArray("ItemDropperMatchAny"), DefaultValue(null)]
        [XmlArrayItem("ItemGenatorName", typeof(ItemGenatorNameMatcher))]
        public List<ItemDropperMatcher> ItemDropperMatcher = null;

        [XmlElement("ModifyFirstMatchOnly"), DefaultValue(true)]
        public bool ModifyFirstMatchOnly = true;

        public string ItemDropperMatcherToString()
        {
            if (ItemDropperMatcher == null || !ItemDropperMatcher.Any())
                return ModifyFirstMatchOnly ? "first changed dropper" : "all droppers";
            else if (ItemDropperMatcher.Count == 1)
                return $"{(ModifyFirstMatchOnly ? "first" : "all")} matching '{ItemDropperMatcher[0]}'";
            else
                return $"{(ModifyFirstMatchOnly ? "first" : "all")} matching {ItemDropperMatcher.Count} matchers";
        }

        public virtual bool IsMatch(DropableWrapper dropable, ItemDropperWrapper dropper)
            => ItemDropperMatcher == null
                || !ItemDropperMatcher.Any()
                || ItemDropperMatcher.Any(matcher => matcher.IsMatch(dropper));

        public abstract bool Apply(DropableWrapper dropable, ItemDropperWrapper dropper);

        public bool TryApply(DropableWrapper dropable, ItemDropperWrapper dropper)
        {
            if (IsMatch(dropable, dropper))
                return Apply(dropable, dropper);
            return false;
        }

        public override void ApplyActualTemplate()
        {
            ItemDropperMatcher = ItemDropperMatcher.Distinct(new ItemDropperMatcherSameComparer()).ToList();

            base.ApplyActualTemplate();
            foreach (var matcher in ItemDropperMatcher)
                matcher.ApplyActualTemplate();
        }

        public override IList<string> Validate()
        {
            var errors = new List<string>();
            foreach (var matcher in ItemDropperMatcher)
                if (matcher.Validate() is IList<string> matcherErrors)
                    errors.AddRange(matcherErrors);
            return errors.Any() ? errors : null;
        }
    }

    public abstract class GuaranteedDropperAlteration : ItemDropperAlteration
    {
        public override string ToString()
            => $"Guaranteed: {ItemDropperMatcherToString()}";
    }

    public abstract class ChanceDropperAlteration : ItemDropperAlteration
    {
        public override string ToString()
            => $"Chance: {ItemDropperMatcherToString()}";
    }

    public abstract class AdditionalDropperAlteration : DropableAlteration
    {
        public override string ToString()
            => $"Additional:";

        public abstract void GenerateAdditionalItems(Dropable dropable, Transform container);

        public abstract void AddAdditionalDroppers(DropableWrapper dropable);
    }


    // Concrete classes: GuaranteedDrop dropper alteration


    public class AddGuaranteedDrops : GuaranteedDropperAlteration
    {
        [XmlArray("Drops")]
        [XmlArrayItem("GuaranteedDrop", typeof(GuaranteedDrop))]
        public List<GuaranteedDrop> Drops = new List<GuaranteedDrop>();

        public override string ToString()
            => $"{base.ToString()}, {Drops.Count} added drops";

        public override bool Apply(DropableWrapper dropable, ItemDropperWrapper itemDropper)
        {
            if (itemDropper is GuaranteedDropWrapper dropper
                && Drops != null
                && Drops.Any())
            {
                var changes = false;
                foreach (var drop in Drops)
                    if (!dropper.Contains(drop.ItemID))
                    {
                        dropper.Drops.Add(drop.New());
                        changes = true;
                    }
                if (changes)
                    dropper.SetHasChanges();
                return true;
            }
            return false;
        }

        public override IList<string> Validate()
        {
            var errors = new List<string>();
            if (base.Validate() is IList<string> baseErrors)
                errors.AddRange(baseErrors);
            if (Drops == null || !Drops.Any())
                errors.Add($"Empty list of Drops for {GetType().Name}!");
            return errors.Any() ? errors : null;
        }
    }

    public class AddGuaranteedFromDropTable : GuaranteedDropperAlteration
    {
        [XmlArray("DropTableUIDsToAdd")]
        [XmlArrayItem("UID")]
        public ObservableCollection<string> DropTableUIDsToAdd = new ObservableCollection<string>();

        private IList<GuaranteedDrop> m_cachedDrops;

        private void ClearCache(object sender, NotifyCollectionChangedEventArgs e)
        {
            m_cachedDrops = null;
            DropTableUIDsToAdd.CollectionChanged -= ClearCache;
        }

        private IList<string> UpdateCache()
        {
            var errors = new List<string>();
            var seenItems = new HashSet<int>();
            m_cachedDrops = new List<GuaranteedDrop>();
            DropTableUIDsToAdd.CollectionChanged += ClearCache;
            if (DropTableUIDsToAdd != null
                && DropTableUIDsToAdd.Any()
                && At.GetField<SL_DropTable>("s_registeredTables") is Dictionary<string, SL_DropTable> dropTables)
            {
                foreach (var uid in DropTableUIDsToAdd)
                {
                    if (dropTables.TryGetValue(uid, out SL_DropTable table))
                    {
                        if (table.GuaranteedDrops != null)
                            foreach (var drop in table.GuaranteedDrops)
                                if (!seenItems.Contains(drop.DroppedItemID))
                                {
                                    m_cachedDrops.Add(new GuaranteedDrop {
                                        ItemID = drop.DroppedItemID,
                                        MinDropCount = drop.MinQty,
                                        MaxDropCount = drop.MaxQty
                                    });
                                    seenItems.Add(drop.DroppedItemID);
                                }
                    }
                    else
                    {
                        errors.Add($"Unable to find SL_DropTable with UID '{uid}'!");
                    }
                }
            }
            return errors;
        }

        private IList<GuaranteedDrop> Drops {
            get {
                if (m_cachedDrops == null)
                    UpdateCache();
                return m_cachedDrops;
            }
        }

        public override string ToString()
            => $"{base.ToString()}, {DropTableUIDsToAdd.Count} DropTables -> {Drops.Count} drops";

        public override bool Apply(DropableWrapper dropable, ItemDropperWrapper itemDropper)
        {
            if (itemDropper is GuaranteedDropWrapper dropper
                && Drops != null
                && Drops.Any())
            {
                var changes = false;
                foreach (var drop in Drops)
                    if (!dropper.Contains(drop.ItemID))
                    {
                        dropper.Drops.Add(drop.New());
                        changes = true;
                    }
                if (changes)
                    dropper.SetHasChanges();
                return true;
            }
            return false;
        }

        public override IList<string> Validate()
        {
            var errors = new List<string>();
            if (base.Validate() is IList<string> baseErrors)
                errors.AddRange(baseErrors);
            if (UpdateCache() is IList<string> cacheErrors)
                errors.AddRange(cacheErrors);
            if (Drops == null || !Drops.Any())
                errors.Add($"Empty list of Drops for {GetType().Name}!");
            return errors.Any() ? errors : null;
        }
    }

    public class ModifyGuaranteedDrops : GuaranteedDropperAlteration
    {
        [XmlArray("ModifiedDrops")]
        [XmlArrayItem("GuaranteedDrop", typeof(GuaranteedDrop))]
        public List<GuaranteedDrop> ModifiedDrops = new List<GuaranteedDrop>();

        public override string ToString()
            => $"{base.ToString()}, {ModifiedDrops.Count} modified drops";

        public override bool IsMatch(DropableWrapper dropable, ItemDropperWrapper itemDropper)
            => itemDropper is GuaranteedDropWrapper dropper
                && ModifiedDrops != null && ModifiedDrops.Any()
                && base.IsMatch(dropable, itemDropper)
                && dropper.HasDrops
                && dropper.Items.Overlaps(ModifiedDrops.Select(x => x.ItemID));

        public override bool Apply(DropableWrapper dropable, ItemDropperWrapper itemDropper)
        {
            if (itemDropper is GuaranteedDropWrapper dropper
                && ModifiedDrops.Any()
                && dropper.HasDrops)
            {
                var modifications = ModifiedDrops.ToDictionary(x => x.ItemID);
                var changes = false;
                foreach (var drop in dropper.Drops)
                    if (modifications.TryGetValue(drop, out var modification))
                        if (modification.Modify(drop))
                            changes = true;
                if (changes)
                    dropper.SetHasChanges();
                return changes;
            }
            return false;
        }

        public override IList<string> Validate()
        {
            var errors = new List<string>();
            if (base.Validate() is IList<string> baseErrors)
                errors.AddRange(baseErrors);
            if (ModifiedDrops == null || !ModifiedDrops.Any())
                errors.Add($"Empty list of ModifiedDrops for {GetType().Name}!");
            return errors.Any() ? errors : null;
        }
    }

    public class RemoveGuaranteedDrops : GuaranteedDropperAlteration
    {
        [XmlArray("ItemsToRemove")]
        [XmlArrayItem("ItemID", typeof(int))]
        public List<int> ItemsToRemove = new List<int>();

        private HashSet<int> m_removedItemsHashSet;

        private HashSet<int> RemovedItemsAsSet {
            get {
                if (m_removedItemsHashSet == null)
                    m_removedItemsHashSet = new HashSet<int>(ItemsToRemove);
                return m_removedItemsHashSet;
            }
        }

        public override string ToString()
            => $"{base.ToString()}, {ItemsToRemove.Count} items to remove";

        public override bool IsMatch(DropableWrapper dropable, ItemDropperWrapper itemDropper)
            => itemDropper is GuaranteedDropWrapper
                && ItemsToRemove != null && ItemsToRemove.Any()
                && base.IsMatch(dropable, itemDropper);

        public override bool Apply(DropableWrapper dropable, ItemDropperWrapper itemDropper)
        {
            if (itemDropper is GuaranteedDropWrapper dropper
                && ItemsToRemove.Any()
                && dropper.HasDrops)
            {
                var removeItems = RemovedItemsAsSet;
                var removedCount = dropper.Drops.RemoveAll(drop => removeItems.Contains(drop));
                if (removedCount > 0)
                {
                    dropper.SetHasChanges();
                    return true;
                }
            }
            return false;
        }

        public override void ApplyActualTemplate()
        {
            base.ApplyActualTemplate();
            m_removedItemsHashSet = null;
        }

        public override IList<string> Validate()
        {
            var errors = new List<string>();
            if (base.Validate() is IList<string> baseErrors)
                errors.AddRange(baseErrors);
            if (ItemsToRemove == null || !ItemsToRemove.Any())
                errors.Add($"Empty list of ItemsToRemove for {GetType().Name}!");
            // TODO: check that item ids exist in the game
            return errors.Any() ? errors : null;
        }
    }


    // Concrete classes: DropTable dropper alteration


    public class AddChanceDrops : ChanceDropperAlteration
    {
        [XmlArray("Drops")]
        [XmlArrayItem("Absolute", typeof(AbsoluteRandomDrop))]
        [XmlArrayItem("Relative", typeof(RelativeRandomDrop))]
        public List<RandomDrop> Drops = new List<RandomDrop>();

        public override string ToString()
            => $"{base.ToString()}, {Drops.Count} added drops";

        public override bool Apply(DropableWrapper dropable, ItemDropperWrapper itemDropper)
        {
            if (itemDropper is DropTableWrapper dropper
                && Drops != null
                && Drops.Any())
            {
                var changes = false;
                foreach (var drop in Drops)
                    if (!dropper.Contains(drop.ItemID))
                    {
                        dropper.Drops.Add(drop.New(dropper.AverageDropChance));
                        changes = true;
                    }
                if (changes)
                    dropper.SetHasChanges();
                return changes;
            }
            return false;
        }

        public override IList<string> Validate()
        {
            var errors = new List<string>();
            if (base.Validate() is IList<string> baseErrors)
                errors.AddRange(baseErrors);
            if (Drops == null || !Drops.Any())
                errors.Add($"Empty list of Drops for {GetType().Name}!");
            return errors.Any() ? errors : null;
        }
    }

    public class ModifyChanceDrops : ChanceDropperAlteration
    {
        [XmlArray("ModifiedDrops")]
        [XmlArrayItem("Absolute", typeof(AbsoluteRandomDrop))]
        public List<AbsoluteRandomDrop> ModifiedDrops = new List<AbsoluteRandomDrop>();

        public override string ToString()
            => $"{base.ToString()}, {ModifiedDrops.Count} modified drops";

        public override bool IsMatch(DropableWrapper dropable, ItemDropperWrapper itemDropper)
            => itemDropper is DropTableWrapper dropper
                && ModifiedDrops != null && ModifiedDrops.Any()
                && base.IsMatch(dropable, itemDropper)
                && dropper.HasDrops
                && dropper.Items.Overlaps(ModifiedDrops.Select(x => x.ItemID));

        public override bool Apply(DropableWrapper dropable, ItemDropperWrapper itemDropper)
        {
            if (itemDropper is DropTableWrapper dropper
                && ModifiedDrops != null && ModifiedDrops.Any()
                && dropper.HasDrops)
            {
                var modifications = ModifiedDrops.ToDictionary(x => x.ItemID);
                var changes = false;
                foreach (var drop in dropper.Drops)
                    if (modifications.TryGetValue(drop, out var modification))
                        if (modification.Modify(drop))
                            changes = true;
                if (changes)
                    dropper.SetHasChanges();
                return changes;
            }
            return false;
        }

        public override IList<string> Validate()
        {
            var errors = new List<string>();
            if (base.Validate() is IList<string> baseErrors)
                errors.AddRange(baseErrors);
            if (ModifiedDrops == null || !ModifiedDrops.Any())
                errors.Add($"Empty list of ModifiedDrops for {GetType().Name}!");
            return errors.Any() ? errors : null;
        }
    }

    public class RemoveChanceDrops : ChanceDropperAlteration
    {
        [XmlArray("ItemsToRemove")]
        [XmlArrayItem("ItemID", typeof(int))]
        public List<int> ItemsToRemove = new List<int>();

        private HashSet<int> m_removedItemsHashSet;

        private HashSet<int> RemovedItemsAsSet {
            get {
                if (m_removedItemsHashSet == null)
                    m_removedItemsHashSet = new HashSet<int>(ItemsToRemove);
                return m_removedItemsHashSet;
            }
        }

        public override string ToString()
            => $"{base.ToString()}, {ItemsToRemove.Count} items to remove";

        public override bool IsMatch(DropableWrapper dropable, ItemDropperWrapper itemDropper)
            => itemDropper is DropTableWrapper
                && ItemsToRemove != null && ItemsToRemove.Any()
                && base.IsMatch(dropable, itemDropper);

        public override bool Apply(DropableWrapper dropable, ItemDropperWrapper itemDropper)
        {
            if (itemDropper is DropTableWrapper dropper
                && ItemsToRemove != null && ItemsToRemove.Any()
                && dropper.HasDrops)
            {
                var removeItems = RemovedItemsAsSet;
                var removedCount = dropper.Drops.RemoveAll(drop => removeItems.Contains(drop));
                if (removedCount > 0)
                {
                    dropper.SetHasChanges();
                    return true;
                }
            }
            return false;
        }

        public override void ApplyActualTemplate()
        {
            base.ApplyActualTemplate();
            m_removedItemsHashSet = null;
        }

        public override IList<string> Validate()
        {
            var errors = new List<string>();
            if (base.Validate() is IList<string> baseErrors)
                errors.AddRange(baseErrors);
            if (ItemsToRemove == null || !ItemsToRemove.Any())
                errors.Add($"Empty list of ItemsToRemove for {GetType().Name}!");
            // TODO: check that item ids exist in the game
            return errors.Any() ? errors : null;
        }
    }


    // Concrete classes, additional ItemDroppers


    public class AdditionalDropTablesByUID : AdditionalDropperAlteration
    {
        [XmlElement("UID")]
        public List<string> DropTableUIDs = new List<string>();

        public override string ToString()
        {
            if (DropTableUIDs == null || !DropTableUIDs.Any())
                return $"{base.ToString()} no SL_DropTable references";
            else if (DropTableUIDs.Count == 1)
                return $"{base.ToString()} SL_DropTable {DropTableUIDs[0]}";
            else
                return $"{base.ToString()} {DropTableUIDs.Count} SL_DropTable referencess";
        }

        public override void GenerateAdditionalItems(Dropable dropable, Transform container)
        {
            if (DropTableUIDs != null
                && DropTableUIDs.Any()
                && At.GetField<SL_DropTable>("s_registeredTables") is Dictionary<string, SL_DropTable> dropTables
                && dropTables.Any())
            {
                foreach (var uid in DropTableUIDs)
                {
                    if (dropTables.TryGetValue(uid, out var table))
                        table.GenerateDrops(container);
                    else
                        DropsPlugin.LogError($"Unable to find SL_DropTable with UID '{uid}'!");
                }
            }
        }

        public override void AddAdditionalDroppers(DropableWrapper dropable)
        {
            if (DropTableUIDs != null
                && DropTableUIDs.Any()
                && At.GetField<SL_DropTable>("s_registeredTables") is Dictionary<string, SL_DropTable> dropTables
                && dropTables.Any())
            {
                foreach (var uid in DropTableUIDs)
                {
                    if (dropTables.TryGetValue(uid, out var table))
                    {
                        if (table.GuaranteedDrops != null && table.GuaranteedDrops.Any())
                        {
                            var guaranteed = dropable.NewGuaranteedDropper($"SL_DropTable - {uid}");
                            foreach (var drop in table.GuaranteedDrops)
                                guaranteed.Drops.Add(new BasicItemDrop {
                                    ItemID = drop.DroppedItemID,
                                    MinDropCount = drop.MinQty,
                                    MaxDropCount = drop.MaxQty,
                                });
                            guaranteed.SetHasChanges();
                        }
                        if (table.RandomTables != null && table.RandomTables.Any())
                        {
                            var i = 0;
                            foreach (var randomGenerator in table.RandomTables)
                            {
                                var chance = dropable.NewChanceDropper($"SL_DropTable Random {i} - {uid}");
                                foreach (var drop in randomGenerator.Drops)
                                    chance.Drops.Add(new ItemDropChance {
                                        ItemID = drop.DroppedItemID,
                                        MinDropCount = drop.MinQty,
                                        MaxDropCount = drop.MaxQty,
                                        DropChance = drop.DiceValue,
                                    });
                                chance.SetHasChanges();
                                i++;
                            }
                        }
                    }
                    else
                        DropsPlugin.LogError($"Unable to find SL_DropTable with UID '{uid}'!");
                }
            }
        }

        public override IList<string> Validate()
        {
            if (DropTableUIDs != null
                && DropTableUIDs.Any()
                && At.GetField<SL_DropTable>("s_registeredTables") is Dictionary<string, SL_DropTable> dropTables)
            {
                var errors = new List<string>();
                var i = 0;
                foreach (var uid in DropTableUIDs)
                {
                    if (!dropTables.TryGetValue(uid, out var table))
                        errors.Add($"string[{i}] Unable to find SL_DropTable with UID '{uid}'!");
                    else if (!(table.GuaranteedDrops != null && table.GuaranteedDrops.Any())
                        && !(table.RandomTables != null && table.RandomTables.Any()))
                        errors.Add($"string[{i}] SL_DropTable with UID '{uid}' does not contain any drops!");
                    i++;
                }
                return errors.Any() ? errors : null;
            }
            return null;
        }
    }
}
