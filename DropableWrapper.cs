using SideLoader;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OutwardGuaranteedDrop = GuaranteedDrop;

namespace Nm1fiOutward.Drops
{
    public class DropableWrapper
    {
        public int Index { get; private set; } = -1;
        public bool Simulated { get; private set; } = false;
        public Dropable Real { get; private set; }
        public string Name => Real?.name;
        public string UID => Real?.UID;
        public bool HasChanges { get; private set; } = false;
        public List<GuaranteedDropWrapper> GuaranteedDroppers { get; private set; }
        public List<DropTableWrapper> ChanceDroppers { get; private set; }

        public DropableWrapper() : this(-1, null, true) { }

        public DropableWrapper(Dropable real, bool simulate = false) : this(-1, real, simulate) { }

        public DropableWrapper(int index, Dropable real, bool simulate = false)
        {
            Index = index;
            Real = real;
            Simulated = simulate;

            if (real != null && real.GetComponentsInChildren<OutwardGuaranteedDrop>() is OutwardGuaranteedDrop[] guaranteed && guaranteed.Any())
                GuaranteedDroppers = guaranteed.Select((dropper, i) => new GuaranteedDropWrapper(i, dropper, this, simulate)).ToList();
            else
                GuaranteedDroppers = new List<GuaranteedDropWrapper>();

            if (real != null && real.GetComponentsInChildren<DropTable>() is DropTable[] chance && chance.Any())
                ChanceDroppers = chance.Select((dropper, i) => new DropTableWrapper(i, dropper, this, simulate)).ToList();
            else
                ChanceDroppers = new List<DropTableWrapper>();
        }

        public static implicit operator Dropable(DropableWrapper wrapped)
            => wrapped.Real;

        public void SetHasChanges() => HasChanges = true;

        public GuaranteedDropWrapper NewGuaranteedDropper(string name = null)
        {
            var dropper = new GuaranteedDropWrapper(GuaranteedDroppers.Count, null, this, Simulated);
            dropper.ItemGenatorName = name;
            GuaranteedDroppers.Add(dropper);
            return dropper;
        }

        public DropTableWrapper NewChanceDropper(string name = null)
        {
            var dropper = new DropTableWrapper(ChanceDroppers.Count, null, this, Simulated);
            dropper.ItemGenatorName = name;
            ChanceDroppers.Add(dropper);
            return dropper;
        }

        public override string ToString()
        {
            var builder = new StringBuilder("Dropable");
            if (Index >= 0)
                builder.Append(" ").Append(Index.ToString());
            builder.Append(": ").Append(Name ?? "");
            if (HasChanges)
                builder.Append(" (altered)");
            return builder.ToString();
        }

        public StringBuilder ToInfoString(int indent = 0, StringBuilder builder = null)
        {
            if (builder == null)
                builder = new StringBuilder();
            builder.Append(' ', indent*2).Append(this).Append(":\n");
            indent++;
            if (GuaranteedDroppers is List<GuaranteedDropWrapper> guaranteedDroppers)
                foreach (var dropper in guaranteedDroppers)
                    dropper.ToInfoString(indent, builder);
            if (ChanceDroppers is List<DropTableWrapper> chanceDroppers)
                foreach (var dropper in chanceDroppers)
                    dropper.ToInfoString(indent, builder);
            return builder;
        }
    }

    public abstract class ItemDropperWrapper {
        private DropableWrapper parent;

        protected abstract string TypeName { get; }

        public int Index { get; private set; } = -1;
        public bool Simulated { get; private set; } = false;
        public ItemDropper Real { get; private set; }

        public bool HasChanges { get; private set; } = false;

        private string itemGeneratorName;
        public string ItemGenatorName {
            get {
                return Real != null ? Real.ItemGenatorName : itemGeneratorName;
            }
            set {
                itemGeneratorName = value;
            }
        }

        public abstract HashSet<int> Items { get; }

        public ItemDropperWrapper() { }

        public ItemDropperWrapper(int index, ItemDropper real, DropableWrapper parent, bool simulate = false)
        {
            Index = index;
            Simulated = simulate;
            Real = real;
            this.parent = parent;
        }

        public virtual void SetHasChanges() {
            parent.SetHasChanges();
            HasChanges = true;
        }

        public abstract bool Contains(int itemID);

        public override string ToString()
        {
            var builder = new StringBuilder(TypeName);
            if (Index >= 0)
                builder.Append(" ").Append(Index.ToString());
            builder.Append(": ").Append(ItemGenatorName ?? "?");
            if (Real == null)
                builder.Append(" (additional)");
            else if (HasChanges)
                builder.Append(" (altered)");
            return builder.ToString();
        }

        public abstract StringBuilder ToInfoString(int indent = 0, StringBuilder builder = null);
    }

    public class GuaranteedDropWrapper : ItemDropperWrapper
    {
        protected override string TypeName => "GuaranteedDrop";

        public List<BasicItemDrop> Drops;

        public bool HasDrops => Drops != null && Drops.Any();

        private HashSet<int> items;
        public override HashSet<int> Items {
            get {
                if (items == null)
                    items = new HashSet<int>(Drops.Select(x => (int)x));
                return items;
            }
        }

        public GuaranteedDropWrapper(int index, OutwardGuaranteedDrop real, DropableWrapper parent, bool simulate = false)
            : base(index, real, parent, simulate)
        {
            if (real != null && At.GetField(real, "m_itemDrops") is List<BasicItemDrop> itemDrops)
            {
                if (simulate)
                {
                    Drops = itemDrops.Select(drop => new BasicItemDrop() {
                        ItemID = drop.ItemID,
                        ItemRef = drop.ItemRef,
                        MinDropCount = drop.MinDropCount,
                        MaxDropCount = drop.MaxDropCount,
                    }).ToList();
                }
                else
                    Drops = itemDrops;
            }
            else
                Drops = new List<BasicItemDrop>();
        }

        public override void SetHasChanges()
        {
            base.SetHasChanges();
            items = null;
        }

        public override bool Contains(int itemID)
            => Items.Contains(itemID);

        public override StringBuilder ToInfoString(int indent = 0, StringBuilder builder = null)
        {
            if (builder == null)
                builder = new StringBuilder();
            builder.Append(' ', indent * 2).Append($"- {this}:\n");
            indent++;
            if (Drops is List<BasicItemDrop> itemDrops)
                foreach (var drop in itemDrops)
                {
                    var count = drop.MaxDropCount > drop.MinDropCount
                        ? $"{drop.MinDropCount}-{drop.MaxDropCount}"
                        : drop.MinDropCount.ToString();
                    builder.Append(' ', indent * 2);
                    builder.Append($"- x{count,-6} {drop.DroppedItem?.Name ?? "<unknown>"} ({(int)drop})\n");
                }
            return builder;
        }
    }

    public class DropTableWrapper : ItemDropperWrapper
    {
        protected override string TypeName => "DropTable";

        public List<ItemDropChance> Drops;

        public bool HasDrops => Drops != null && Drops.Any();

        public float AverageDropChance { get; private set; } = 1;

        private HashSet<int> items;
        public override HashSet<int> Items {
            get {
                if (items == null)
                    items = new HashSet<int>(Drops.Select(x => (int)x));
                return items;
            }
        }

        public DropTableWrapper(int index, DropTable real, DropableWrapper parent, bool simulate = false)
            : base(index, real, parent, simulate)
        {
            if (real != null && At.GetField(real, "m_itemDrops") is List<ItemDropChance> itemDrops)
            {
                if (simulate)
                {
                    Drops = itemDrops.Select(drop => new ItemDropChance() {
                        ItemID = drop.ItemID,
                        ItemRef = drop.ItemRef,
                        MinDropCount = drop.MinDropCount,
                        MaxDropCount = drop.MaxDropCount,
                        DropChance = drop.DropChance,
                        ChanceRegenDelay = drop.ChanceRegenDelay,
                    }).ToList();
                }
                else
                    Drops = itemDrops;
                AverageDropChance = (float)itemDrops.Sum(x => x.DropChance) / itemDrops.Count;
            }
            else
                Drops = new List<ItemDropChance>();
        }

        public override void SetHasChanges()
        {
            base.SetHasChanges();
            if (Real != null)
                ((DropTable)Real).Reset(false);
            items = null;
        }

        public override bool Contains(int itemID)
            => Items.Contains(itemID);

        public override StringBuilder ToInfoString(int indent = 0, StringBuilder builder = null)
        {
            if (builder == null)
                builder = new StringBuilder();
            builder.Append(' ', indent * 2).Append($"- {this}:\n");
            indent++;
            if (Drops is List<ItemDropChance> itemDrops)
                foreach (var drop in itemDrops)
                {
                    var count = drop.MaxDropCount > drop.MinDropCount
                                ? $"{drop.MinDropCount}-{drop.MaxDropCount}"
                                : drop.MinDropCount.ToString();
                    builder.Append(' ', indent * 2);
                    builder.Append($"- x{count,-6} Chance={drop.DropChance,-2} {drop.DroppedItem?.Name ?? "<unknown>"} ({(int)drop})\n");
                }
            return builder;
        }
    }
}
