using SideLoader.Model;
using SideLoader.SLPacks;
using System.Collections.Generic;
using System.Linq;

namespace Nm1fiOutward.Drops
{
    public class DropTableAlterationCategory : SLPackTemplateCategory<DropTableAlteration>
    {
        public override string FolderName => DropsPlugin.NAME;

        public override int LoadOrder => 25;

        public override void ApplyTemplate(ContentTemplate template)
        {
            (template as DropTableAlteration).ApplyActualTemplate();
        }

        private static Dictionary<DropTableAlteration.ItemSourceType, List<DropTableAlteration>> alterationsPerType
            = new Dictionary<DropTableAlteration.ItemSourceType, List<DropTableAlteration>>();
        private static Dictionary<DropTableAlteration.ItemSourceType, List<DropTableAlteration>> additionalDropsPerType
            = new Dictionary<DropTableAlteration.ItemSourceType, List<DropTableAlteration>>();

        protected override void OnHotReload()
        {
            alterationsPerType.Clear();
            additionalDropsPerType.Clear();
        }

        public static IEnumerable<DropTableAlteration> GetAlterationsFor(DropTableAlteration.ItemSourceType type)
        {
            if (alterationsPerType.TryGetValue(type, out var alterations))
                return alterations;
            alterations = AllCurrentTemplates.Where(template => template.TargetType == type && template.HasAlterations).ToList();
            alterationsPerType.Add(type, alterations);
            return alterations;
        }

        public static IEnumerable<DropTableAlteration> GetAdditionalDropsFor(DropTableAlteration.ItemSourceType type)
        {
            if (additionalDropsPerType.TryGetValue(type, out var additionals))
                return additionals;
            additionals = AllCurrentTemplates.Where(template => template.TargetType == type && template.HasAdditionalDrops).ToList();
            additionalDropsPerType.Add(type, additionals);
            return additionals;
        }
    }
}
