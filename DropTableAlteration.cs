using SideLoader;
using SideLoader.Model;
using SideLoader.SLPacks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using UnityEngine;

// For renaming fields [XmlElement(ElementName = "Members")] or [XmlElement("Members")]
// https://docs.microsoft.com/en-us/dotnet/api/system.xml.serialization.xmlelementattribute
// Ignore field when reading/writíng the xml [XmlIgnore]
// https://docs.microsoft.com/en-us/dotnet/api/system.xml.serialization.xmlattributes.xmlignore

// Migration
// 1. minor release:
//    - add migrations
//    - set [XmlElement("OldFieldName"), DefaultValue(null), EditorBrowsable(EditorBrowsableState.Never)]
//    - or  [XmlArray("OldListName"), DefaultValue(null), EditorBrowsable(EditorBrowsableState.Never)]
//           ^- define xml element    ^- default values are not exported  ^- hides from SL editor
//    - append _DEPRECATED to C# field
//    - make dummy classes when structure is about to change
// 2. 1st major release: no changes
// 3. 2nd major release: drop migrations, set [XmlIgnore]
// 3. 3rd major release: drop fields

namespace Nm1fiOutward.Drops
{
    [SL_Serialized]
    public class DropTableAlteration : ContentTemplate
    {
        public enum ItemSourceType
        {
            /// <summary>This alteration is used in merchant inventories.</summary>
            Merchant,
            /// <summary>This alteration is used in loot containers, chests, junk piles and so on.</summary>
            LootContainer,
            /// <summary>This alteration is used in bandit and monster drops.</summary>
            EnemyOrMonster,
            /// <summary>This alteration is used in Gatherables.</summary>
            //Gatherable
        };

        // Abstract/Interface

        public override ITemplateCategory PackCategory => SLPackManager.GetCategoryInstance<DropTableAlterationCategory>();
        public override string DefaultTemplateName => "Untitled DropTableAlteration";
        public string UID => $"{SerializedSLPackName}.{SerializedFilename}";

        // User defined

        [XmlElement("TargetType")]
        public ItemSourceType TargetType;

        [XmlElement("MatchAny", typeof(AnyConstraint))]
        [XmlElement("MatchAll", typeof(AllConstraint))]
        public ConstraintGroup Matcher = new AllConstraint();

        [XmlArray("Alterations")]
        [XmlArrayItem("AddGuaranteedDrops", typeof(AddGuaranteedDrops))]
        [XmlArrayItem("ModifyGuaranteedDrops", typeof(ModifyGuaranteedDrops))]
        [XmlArrayItem("RemoveGuaranteedDrops", typeof(RemoveGuaranteedDrops))]
        [XmlArrayItem("AddGuaranteedFromDropTable", typeof(AddGuaranteedFromDropTable))]
        [XmlArrayItem("AddChanceDrops", typeof(AddChanceDrops))]
        [XmlArrayItem("ModifyChanceDrops", typeof(ModifyChanceDrops))]
        [XmlArrayItem("RemoveChanceDrops", typeof(RemoveChanceDrops))]
        [XmlArrayItem("AdditionalDropTablesByUID", typeof(AdditionalDropTablesByUID))]
        public List<DropableAlteration> Alterations = new List<DropableAlteration>();

        public bool HasAlterations => HasGuaranteedDropperAlterations || HasChanceDropperAlterations;
        public bool HasGuaranteedDropperAlterations => GuaranteedDropperAlterations.Any();
        public bool HasChanceDropperAlterations => ChanceDropperAlterations.Any();
        public bool HasAdditionalDrops => AdditionalDropTableAlterations.Any();

        public override void ApplyActualTemplate()
        {
            ClearCaches();
            // run migrations here

            if (Alterations.Count == 0)
            {
                DropsPlugin.LogWarning($"No Alterations configured for DropTableAlteration '{UID}'!");
                return;
            }

            if (Matcher != null)
            {
                var ownerUIDHint = ((Func<OwnerUIDConstraint.OwnerHint>)delegate () {
                    switch (TargetType)
                    {
                        case ItemSourceType.Merchant: return OwnerUIDConstraint.OwnerHint.Merchant;
                        case ItemSourceType.LootContainer: return OwnerUIDConstraint.OwnerHint.TreasureChest;
                        default: return OwnerUIDConstraint.OwnerHint.None;
                    }
                })();

                foreach (var constraint in Matcher.IterateTree())
                    if (constraint is OwnerUIDConstraint ownerUIDConstraint)
                        ownerUIDConstraint.Hint = ownerUIDHint;

                Matcher.ApplyActualTemplate();
            }

            if (Alterations != null)
                foreach (var alteration in Alterations)
                    alteration.ApplyActualTemplate();

            SLPackManager.AddLateApplyListener(OnLateApply);

            DropsPlugin.Log($"Registered DropTableAlteration '{UID}'");
        }

        private void OnLateApply(object[] args)
        {
            var errorMessage = new StringBuilder();
            {
                if (Matcher != null && Matcher.Validate() is IList<string> errors)
                    foreach (var error in errors)
                        errorMessage.Append($"\n  - {error}");
            }
            foreach (var (i, alteration) in Alterations.Select((alteration, i) => (i, alteration)))
            {
                if (alteration.Validate() is IList<string> errors)
                    foreach (var error in errors)
                        errorMessage.Append($"\n  - Alterations[{i}]: {error}");
            }
            if (errorMessage.Length > 0)
            {
                DropsPlugin.LogWarning($"Following errors were reported when validating fields for {UID}:{errorMessage}");
                return;
            }
        }

        // Internal

        IList<(GuaranteedDropperAlteration, int)> m_guaranteedAlterationsCache;
        IList<(ChanceDropperAlteration, int)> m_chanceAlterationsCache;
        IList<(AdditionalDropperAlteration, int)> m_additionalAlterationsCache;

        private void ClearCaches()
        {
            m_guaranteedAlterationsCache = null;
            m_chanceAlterationsCache = null;
            m_additionalAlterationsCache = null;
        }
        private void UpdateCaches()
        {
            m_guaranteedAlterationsCache = new List<(GuaranteedDropperAlteration, int)>();
            m_chanceAlterationsCache = new List<(ChanceDropperAlteration, int)>();
            m_additionalAlterationsCache = new List<(AdditionalDropperAlteration, int)>();

            if (Alterations != null)
            {
                int i = 0;
                foreach (var alteration in Alterations)
                {
                    if (alteration is GuaranteedDropperAlteration guaranteedAlteration)
                        m_guaranteedAlterationsCache.Add((guaranteedAlteration, i));
                    else if (alteration is ChanceDropperAlteration chanceAlteration)
                        m_chanceAlterationsCache.Add((chanceAlteration, i));
                    else if (alteration is AdditionalDropperAlteration additionalAlteration)
                        m_additionalAlterationsCache.Add((additionalAlteration, i));
                    else
                        DropsPlugin.LogError($"Unknown alteration type {alteration.GetType().Name} at index {i} in {UID}");
                    i++;
                }
            }
        }

        private IList<(GuaranteedDropperAlteration, int)> GuaranteedDropperAlterations {
            get {
                if (m_guaranteedAlterationsCache == null)
                    UpdateCaches();
                return m_guaranteedAlterationsCache;
            }
        }

        private IList<(ChanceDropperAlteration, int)> ChanceDropperAlterations {
            get {
                if (m_chanceAlterationsCache == null)
                    UpdateCaches();
                return m_chanceAlterationsCache;
            }
        }

        private IList<(AdditionalDropperAlteration, int)> AdditionalDropTableAlterations {
            get {
                if (m_additionalAlterationsCache == null)
                    UpdateCaches();
                return m_additionalAlterationsCache;
            }
        }

        public bool TryUpdate(DropableWrapper dropable)
        {
            var guaranteedAlterations = GuaranteedDropperAlterations;
            var chanceAlterations = ChanceDropperAlterations;
            if ((guaranteedAlterations.Any() || chanceAlterations.Any()) && Matcher != null && Matcher.IsMatch(dropable))
            {
                if (guaranteedAlterations.Any())
                {
                    if (dropable.GuaranteedDroppers is List<GuaranteedDropWrapper> droppers)
                    {
                        foreach (var (alteration, i) in guaranteedAlterations)
                        {
                            var found = false;
                            foreach (var dropper in droppers)
                                if (alteration.TryApply(dropable, dropper))
                                {
                                    found = true;
                                    if (alteration.ModifyFirstMatchOnly)
                                        break;
                                }
                            if (!found)
                                DropsPlugin.LogWarning($"DropTableAlteration '{UID}' was unable to find ItemDropper for Alterations[{i}] {alteration.GetType().Name} for {dropable}");
                        }
                    }
                }
                if (chanceAlterations.Any())
                {
                    if (dropable.ChanceDroppers is List<DropTableWrapper> droppers)
                    {
                        foreach (var (alteration, i) in chanceAlterations)
                        {
                            var found = false;
                            foreach (var dropper in droppers)
                                if (alteration.TryApply(dropable, dropper))
                                {
                                    found = true;
                                    if (alteration.ModifyFirstMatchOnly)
                                        break;
                                }
                            if (!found)
                                DropsPlugin.LogWarning($"DropTableAlteration '{UID}' was unable to find ItemDropper for Alterations[{i}] {alteration.GetType().Name} for {dropable}");
                        }
                    }
                }
            }
            return dropable.HasChanges;
        }

        public bool TryGenerateAdditionalItems(Dropable dropable, Transform container)
        {
            var alterations = AdditionalDropTableAlterations;
            if (alterations.Any() && Matcher != null && Matcher.IsMatch(dropable))
            {
                foreach (var (alteration, _) in alterations)
                    alteration.GenerateAdditionalItems(dropable, container);
                return true;
            }
            return false;
        }

        public void AddAdditionalDroppersTo(DropableWrapper dropable)
        {
            var alterations = AdditionalDropTableAlterations;
            if (alterations.Any() && Matcher != null && Matcher.IsMatch(dropable))
            {
                foreach (var (alteration, _) in alterations)
                    alteration.AddAdditionalDroppers(dropable);
            }
        }
    }
}
