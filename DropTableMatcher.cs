using SideLoader;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Nm1fiOutward.Drops
{
    [SL_Serialized]
    public abstract class DropTableMatcher : ITemplateListItem
    {
        public abstract bool IsMatch(Dropable dropable);

        public virtual void ApplyActualTemplate() { }

        public virtual IList<string> Validate() => null;
    }

    // Container matchers

    public abstract class ConstraintGroup : DropTableMatcher
    {
        [XmlElement("Any", typeof(AnyConstraint))]
        [XmlElement("All", typeof(AllConstraint))]
        [XmlElement("DropableName", typeof(DropableNameConstraint))]
        [XmlElement("MerchantDropNameName", typeof(MerchantDropNameConstraint))]
        [XmlElement("OwnerUID", typeof(OwnerUIDConstraint))]
        [XmlElement("Scene", typeof(SceneConstraint))]
        [XmlElement("Region", typeof(RegionConstraint))]
        public readonly List<DropTableMatcher> Constraints = new List<DropTableMatcher>();

        protected abstract string Name { get; }

        public override string ToString()
            => $"{Name}({string.Join(", ", Constraints)})";

        public override void ApplyActualTemplate()
        {
            foreach (var constraint in Constraints)
                constraint.ApplyActualTemplate();
        }

        public override IList<string> Validate()
        {
            var errors = new List<string>();
            var i = 0;
            foreach (var constraint in Constraints)
            {
                if (constraint.Validate() is IList<string> constraintrErrors && constraintrErrors.Any())
                    foreach (var error in constraintrErrors)
                        errors.Add($"Match{Name}[{i}]: {error}");
                i++;
            }
            return errors.Any() ? errors : null;
        }

        public IEnumerable<DropTableMatcher> IterateTree()
        {
            foreach (var child in Constraints)
            {
                yield return child;
                if (child is ConstraintGroup group)
                    foreach (var grantChild in group.IterateTree())
                        yield return grantChild;
            }
            yield break;
        }
    }

    public class AnyConstraint : ConstraintGroup
    {
        protected override string Name => "Any";

        public override bool IsMatch(Dropable dropable)
            => !Constraints.Any() || Constraints.Any(constraint => constraint.IsMatch(dropable));
    }

    public class AllConstraint : ConstraintGroup
    {
        protected override string Name => "All";

        public override bool IsMatch(Dropable dropable)
            => Constraints.Any() && Constraints.TrueForAll(constraint => constraint.IsMatch(dropable));
    }

    // Leaf constraints

    public abstract class LeafConstraint : DropTableMatcher { }

    public abstract class SingleValueXMLConstraint : LeafConstraint // , IXmlSerializable
    {
        protected abstract string XmlData { get; set; }

        protected virtual bool RequiresValue => true;

        public override IList<string> Validate()
        {
            if (RequiresValue)
            {
                var val = XmlData.Trim();
                XmlData = val;
                if (string.IsNullOrEmpty(val))
                    return new[] { $"{GetType().Name} should not be empty! Leave matcher list empty to match any." };
            }
            return null;
        }

        #region IXmlSerializable

        public XmlSchema GetSchema() => null;

        public void ReadXml(XmlReader reader) => XmlData = reader.ReadElementContentAsString();

        public void WriteXml(XmlWriter writer) => writer.WriteString(XmlData);

        #endregion
    }

    public class DropableNameConstraint : SingleValueXMLConstraint, IXmlSerializable
    {
        public string DropableName = "";

        public override string ToString() => $"name={DropableName}";

        protected override string XmlData { get => DropableName; set => DropableName = value; }

        public override bool IsMatch(Dropable dropable)
            => dropable != null
                && dropable.name is string name
                && !string.IsNullOrEmpty(name)
                && name.Contains(DropableName);
    }

    public class MerchantDropNameConstraint : SingleValueXMLConstraint, IXmlSerializable
    {
        public string MerchantDropName = "";

        public override string ToString() => $"name={MerchantDropName}";

        protected override string XmlData { get => MerchantDropName; set => MerchantDropName = value; }

        public override bool IsMatch(Dropable dropable)
            => dropable != null
                && dropable.UID is string uid
                && !string.IsNullOrEmpty(uid)
                && uid.StartsWith("MerchantDrop_")
                && uid.Contains(MerchantDropName);
    }

    public class OwnerUIDConstraint : SingleValueXMLConstraint, IXmlSerializable
    {
        public enum OwnerHint
        {
            None,
            TreasureChest,
            Merchant,
        }

        public string UID = "";

        // Class containing these constraints, should set this hint, if it knows target types
        [EditorBrowsable(EditorBrowsableState.Never)]
        public OwnerHint Hint = OwnerHint.None;

        public override string ToString() => $"holder-uid={UID}";

        protected override string XmlData { get => UID; set => UID = value; }

        public override bool IsMatch(Dropable dropable)
        {
            if (dropable == null)
                return false;
            if ((Hint == OwnerHint.None || Hint == OwnerHint.TreasureChest)
                && dropable.transform.parent.parent.GetComponent<TreasureChest>() is TreasureChest chest)
                return UID == chest.HolderUID;
            else if ((Hint == OwnerHint.None || Hint == OwnerHint.Merchant)
                && dropable.transform.parent.GetComponent<Merchant>() is Merchant merchant)
                return UID == merchant.HolderUID;

            return false;
        }
    }

    public class SceneConstraint : SingleValueXMLConstraint, IXmlSerializable
    {
        public string SceneName;

        public SceneConstraint() : base()
        {
            if (AreaManager.Instance.CurrentArea is Area currentArea)
            {
                SceneName = SceneManagerHelper.ActiveSceneName;
            }
        }

        public override string ToString() => $"scene={SceneName}";

        protected override string XmlData { get => SceneName; set => SceneName = value; }

        public override bool IsMatch(Dropable dropable)
            => dropable != null
                && dropable.gameObject.scene.name is string scene
                && !string.IsNullOrEmpty(scene)
                && scene.Contains(SceneName);
    }

    public class RegionConstraint : SingleValueXMLConstraint, IXmlSerializable
    {
        public enum Regions
        {
            Abrassar,
            AntiquePlateau,
            Caldera,
            Chersonese,
            EnmerkarForest,
            HallowedMarsh,
        };

        private static readonly Dictionary<string, Regions> areaFamiliesToRegion = new Dictionary<string, Regions> {
            { "Levant", Regions.Abrassar },
            { "Harmattan", Regions.AntiquePlateau },
            { "Sirocco", Regions.Caldera },
            { "Cierzo", Regions.Chersonese },
            { "Berg", Regions.EnmerkarForest },
            { "Monsoon", Regions.HallowedMarsh }
        };

        private static Dictionary<string, Regions?> sceneToRegionCache = new Dictionary<string, Regions?>();

        private static Regions? RegionFromScene(string scene)
        {
            {
                if (sceneToRegionCache.TryGetValue(scene, out var region))
                    return region;
            }
            foreach (var areaFamily in AreaManager.AreaFamilies)
                foreach (var keyword in areaFamily.FamilyKeywords)
                    if (scene.Contains(keyword))
                    {
                        if (areaFamiliesToRegion.TryGetValue(areaFamily.FamilyName, out var region))
                        {
                            sceneToRegionCache.Add(scene, region);
                            return region;
                        }
                        break;
                    }
            sceneToRegionCache.Add(scene, null);
            return null;
        }


        public Regions Region;

        public RegionConstraint() : base()
        {
            var region = RegionFromScene(SceneManagerHelper.ActiveSceneName);
            Region = region ?? Regions.Chersonese;
        }

        public override string ToString() => $"region={Region}";

        protected override string XmlData {
            get => Region.ToString();
            set {
                if (Enum.TryParse(value, out Regions region))
                    Region = region;
            }
        }

        public override bool IsMatch(Dropable dropable)
            => dropable?.gameObject?.scene.name is string scene
                && !string.IsNullOrEmpty(scene)
                && RegionFromScene(scene) is var region
                && region != null
                && region == Region;
    }
}
