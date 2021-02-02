using SideLoader;

namespace Nm1fiOutward.Drops
{
    interface IItemDrop
    {
        string ItemName { get; }
        Item ItemRef { get; }
        string DropCountText { get; }
        string ToString();
    }

    [SL_Serialized]
    public abstract class ItemDrop : IItemDrop
    {
        public int ItemID;
        public int MinDropCount = 1;
        public int MaxDropCount = 1;

        public int ActualMaxDropCount => MaxDropCount > MinDropCount ? MaxDropCount : MinDropCount;

        public string ItemName
            => ItemRef?.Name ?? $"<unknown {ItemID}>";

        public Item ItemRef
            => ResourcesPrefabManager.Instance.GetItemPrefab(ItemID);

        public string DropCountText
            => MaxDropCount > MinDropCount ? $"{MinDropCount}-{MaxDropCount}" : MinDropCount.ToString();

        public override string ToString()
           => $"{DropCountText}x {ItemName} ({ItemID})";
    }

    public class GuaranteedDrop : ItemDrop
    {
        public BasicItemDrop New()
        {
            return new BasicItemDrop()
            {
                ItemID = ItemID,
                ItemRef = ItemRef,
                MinDropCount = MinDropCount,
                MaxDropCount = ActualMaxDropCount,
            };
        }

        public bool Modify(BasicItemDrop existing)
        {
            bool differs = false;
            if (existing.MinDropCount != MinDropCount)
            {
                existing.MinDropCount = MinDropCount;
                differs = true;
            }
            var maxDropCount = ActualMaxDropCount;
            if (existing.MaxDropCount != maxDropCount)
            {
                existing.MaxDropCount = maxDropCount;
                differs = true;
            }
            return differs;
        }
    }

    public abstract class RandomDrop : ItemDrop
    {
        protected ItemDropChance New(int dropChance)
        {
            return new ItemDropChance()
            {
                // int DropChance;
                // int ChanceReduction;
                // float ChanceRegenDelay;
                // int ChanceRegenQty;
                ItemID = ItemID,
                ItemRef = ItemRef,
                MinDropCount = MinDropCount,
                MaxDropCount = ActualMaxDropCount,
                DropChance = dropChance > 0 ? dropChance : 1,
            };
        }

        public abstract ItemDropChance New(float averageChance);
    }

    public class AbsoluteRandomDrop : RandomDrop
    {
        public int DropChance = 1;

        public override string ToString()
           => $"Chance: {DropChance}, {base.ToString()}";

        public override ItemDropChance New(float averageChance)
        {
            return base.New(DropChance);
        }

        public bool Modify(ItemDropChance existing)
        {
            bool differs = false;
            if (existing.MinDropCount != MinDropCount)
            {
                existing.MinDropCount = MinDropCount;
                differs = true;
            }
            var maxDropCount = ActualMaxDropCount;
            if (existing.MaxDropCount != maxDropCount)
            {
                existing.MaxDropCount = maxDropCount;
                differs = true;
            }
            if (existing.DropChance != DropChance)
            {
                existing.DropChance = DropChance;
                differs = true;
            }
            return differs;
        }
    }

    public class RelativeRandomDrop : RandomDrop
    {
        public float RelativeChance = 1f;

        public override string ToString()
           => $"Rel.Chance: {RelativeChance:n2}, {base.ToString()}";

        public override ItemDropChance New(float averageChance)
        {
            int chance = (int)(averageChance * RelativeChance + .5f);
            return base.New(chance);
        }
    }
}
