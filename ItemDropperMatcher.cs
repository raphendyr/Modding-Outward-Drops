using SideLoader;
using System.Collections.Generic;

namespace Nm1fiOutward.Drops
{
    [SL_Serialized]
    public abstract class ItemDropperMatcher : ITemplateListItem
    {
        public abstract bool IsMatch(ItemDropperWrapper dropper);

        public virtual IList<string> Validate() => null;

        public virtual void ApplyActualTemplate() { }
    }

    public class ItemGenatorNameMatcher : ItemDropperMatcher
    {
        public enum MatchType
        {
            Exact,
            Contains,
        }

        public string Name = "";
        public MatchType Match = MatchType.Exact;

        public override string ToString() => $"{Match} '{Name}'";

        public override bool IsMatch(ItemDropperWrapper dropper)
        {
            var name = dropper.ItemGenatorName;
            if (string.IsNullOrEmpty(name))
                return false;

            switch (Match)
            {
                case MatchType.Exact: return Name == name;
                case MatchType.Contains: return name.Contains(Name);
                default: return false;
            }
        }
    }


    internal class ItemDropperMatcherSameComparer : IEqualityComparer<ItemDropperMatcher>
    {
        public bool Equals(ItemDropperMatcher a, ItemDropperMatcher b)
        {
            return a != null && b != null
                && a.GetType() == b.GetType()
                && a.ToString() == b.ToString();
        }

        public int GetHashCode(ItemDropperMatcher matcher)
        {
            if (matcher == null) return 0;
            return matcher.GetType().GetHashCode() ^ matcher.ToString().GetHashCode();
        }
    }
}
