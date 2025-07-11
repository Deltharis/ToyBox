﻿// Copyright < 2021 > Narria (github user Cabarius) - License: MIT
using Kingmaker;
using Kingmaker.AI.BehaviourTrees;
using Kingmaker.AreaLogic.Etudes;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Area;
using Kingmaker.Cheats;
using Kingmaker.Code.UI.MVVM.VM.Vendor;
using Kingmaker.Controllers.Rest;
using Kingmaker.Designers;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.DialogSystem.Blueprints;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Kingmaker.Globalmap.View;
using Kingmaker.Items;
using Kingmaker.Mechanics.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.Models.UnitSettings;
using Kingmaker.UI.Selection;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using Kingmaker.View;
using Kingmaker.View.MapObjects;
using Kingmaker.View.Spawners;
using ModKit;
using ModKit.Utility;
using Newtonsoft.Json;
using Owlcat.Runtime.Core.Physics.PositionBasedDynamics.Forces;
using Owlcat.Runtime.Core.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ToyBox.BagOfPatches;
using UnityEngine;
using UnityModManagerNet;
using UnityUIControls;
using static Kingmaker.Designers.EventConditionActionSystem.Actions.OpenVendorSelectingWindow;
using static UnityModManagerNet.UnityModManager;
namespace ToyBox {
    public static partial class Actions {
        public static Settings settings => Main.Settings;

        public static void RestSelected() {
            foreach (var selectedUnit in UIAccess.SelectionManager.SelectedUnits) {
                PartHealth.RestUnit(selectedUnit);
            }
        }

        public static void SpawnEnemyUnderCursor(
            BlueprintUnit bp = null,
            BlueprintFaction factionBp = null,
            Vector3 position = default(Vector3)) {
            Vector3 position1 = position != new Vector3() ? position : Game.Instance.ClickEventsController.WorldPosition;
            if (bp == null)
                bp = Game.Instance.BlueprintRoot.Cheats.Enemy;
            Mod.Log("Summoning: " + Kingmaker.Cheats.Utilities.GetBlueprintPath((BlueprintScriptableObject)bp));
            BaseUnitEntity baseUnitEntity = Game.Instance.EntitySpawner.SpawnUnit(bp, position1, Quaternion.identity, Game.Instance.State.LoadedAreaState.MainState);
            if (factionBp == null)
                return;
            baseUnitEntity.Faction.Set(factionBp);
        }

        public static void SpawnUnit(BlueprintUnit unit, int count) {
            var worldPosition = Game.Instance.ClickEventsController.WorldPosition;
            //           var worldPosition = Game.Instance.Player.MainCharacter.Value.Position;
            if (!(unit == null)) {
                for (var i = 0; i < count; i++) {
                    var offset = 5f * UnityEngine.Random.insideUnitSphere;
                    Vector3 spawnPosition = new(
                        worldPosition.x + offset.x,
                        worldPosition.y,
                        worldPosition.z + offset.z);
                    Game.Instance.EntitySpawner.SpawnUnit(unit, spawnPosition, Quaternion.identity, Game.Instance.State.LoadedAreaState.MainState);
                }
            }
        }
        public static void HandleChangeParty() {
            if (Game.Instance.CurrentMode == GameModeType.GlobalMap) {
                var partyCharacters = Game.Instance.Player.Party.Select(u => new UnitReference(u)).ToList(); ;
                if ((partyCharacters != null ? (partyCharacters.Select(r => r.Entity).SequenceEqual(Game.Instance.Player.Party) ? 1 : 0) : 1) != 0)
                    return;
            } else {
                foreach (var temp in Game.Instance.Player.RemoteCompanions.ToTempList())
                    temp.IsInGame = false;
                Game.Instance.Player.FixPartyAfterChange();
                UIAccess.SelectionManager.UpdateSelectedUnits();
                var tempList = Game.Instance.Player.Party.Select(character => character.View).ToTempList<UnitEntityView>();
                if (UIAccess.SelectionManager is SelectionManagerPC selectionManager)
                    selectionManager.MultiSelect((IEnumerable<UnitEntityView>)tempList);
            }
        }
        public static void ChangeParty() {
            var currentMode = Game.Instance.CurrentMode;

            if (currentMode == GameModeType.Default || currentMode == GameModeType.Pause || currentMode == GameModeType.GlobalMap) {
                if (Main.IsModGUIShown) UnityModManager.UI.Instance.ToggleWindow();
                EventBus.RaiseEvent<IGroupChangerHandler>(h => h.HandleCall(new Action(HandleChangeParty), (Action)null, true));
            }
        }
        public static void ApplyTimeScale() {
            var timeScale = settings.useAlternateTimeScaleMultiplier
                                ? settings.alternateTimeScaleMultiplier
                                : settings.timeScaleMultiplier;
            Game.Instance.TimeController.DebugTimeScale = timeScale;
        }

        // called when changing highlight settings so they take immediate effect
        public static void UpdateHighlights(bool on) {
            foreach (var mapObjectEntityData in Game.Instance.State.MapObjects) {
                mapObjectEntityData.View.UpdateHighlight();
            }
            foreach (var unitEntityData in Game.Instance.State.AllUnits) {
                unitEntityData.View.UpdateHighlight(false);
            }
        }
        public static void resetClassLevel(this BaseUnitEntity ch) {
            var level = 55;
            var xp = ch.Descriptor().Progression.Experience;
            var xpTable = ch.Descriptor().Progression.ExperienceTable;

            for (var i = 55; i >= 1; i--) {
                var xpBonus = xpTable.GetBonus(i);

                Mod.Trace(i + ": " + xpBonus + " | " + xp);

                if ((xp - xpBonus) >= 0) {
                    Mod.Trace(i + ": " + (xp - xpBonus));
                    level = i;
                    break;
                }
            }
            ch.Descriptor().Progression.m_CharacterLevel = level;
        }
        public static void RunPerceptionTriggers() {
            // On the local map
            foreach (var obj in Game.Instance.State.MapObjects) {
                obj.LastAwarenessRollRank = new Dictionary<UnitReference, int>();
            }
            Tweaks.UnitEntityDataCanRollPerceptionExtension.TriggerReroll = true;
        }

        public static void RerollInteractionSkillChecks() {
            foreach (var obj in Game.Instance.State.MapObjects) {
                foreach (var part in obj.Parts.GetAll<InteractionSkillCheckPart>()) {
                    if (part.AlreadyUsed && !part.CheckPassed) {
                        part.AlreadyUsed = false;
                        part.Enabled = true;
                    }
                }
            }
        }

        public static void GoToTradeWindow() {

            //Trade window should not be available in the Dark City and in Chapter 5. The game already disables it in the prologue.
            string[] blockedEtudes = ["725db1ff1322445c8185506f4f6d242e", "6571856eb6c0459cba30e13adc5c6314"];
            var etudes = EtudesTreeModel.Instance.loadedEtudes;

            foreach (var etude in Game.Instance.Player.EtudesSystem.Etudes.RawFacts) {
                if (blockedEtudes.Contains(etude.Blueprint.AssetGuid)) {
                    if (etude.IsPlaying) {
                        return;
                    }
                }
            }

            var area = Game.Instance.State.LoadedAreaState;
            var currentArea = area.Blueprint;
            var bridgeArea = ResourcesLibrary.TryGetBlueprint<BlueprintArea>("255859109cec4a042ade1613d80b25a4");
            var factotum = ResourcesLibrary.TryGetBlueprint<BlueprintAnswer>("d9307ba41f354ad2be00085eca5d0264");

            if (currentArea == bridgeArea) {
                (factotum.OnSelect.Actions[0] as Conditional).IfTrue.Actions[0].Run();
            } else {
                List<Entity> entities = new();
                List<UnitSpawnerBase.MyData> spawners = new();
                List<AbstractUnitEntity> units = new();
                var action = factotum.ElementsArray.OfType<OpenVendorSelectingWindow>().FirstOrDefault();

                try {
                    var areaState = ResourcesLibrary.TryGetBlueprint<BlueprintArea>("255859109cec4a042ade1613d80b25a4");
                    AreaPersistentState state = Game.Instance.State.GetStateForArea(areaState);
                    AreaPersistentState areaPersistentState;
                    using (var jsonStreamForArea = AreaDataStash.GetJsonStreamForArea(state, state.MainState)) {
                        areaPersistentState = AreaDataStash.Serializer.Deserialize<AreaPersistentState>(jsonStreamForArea);
                    }

                    var vendorScene = state.GetStateForScene("VoidshipBridge_Vendors_Mechanics");
                    {
                        using (JsonTextReader jsonStreamForArea2 = AreaDataStash.GetJsonStreamForArea(state, vendorScene)) {
                            if (jsonStreamForArea2 != null) {
                                try {
                                    SceneEntitiesState deserializedSceneState = AreaDataStash.Serializer.Deserialize<SceneEntitiesState>(jsonStreamForArea2);
                                    areaPersistentState.SetDeserializedSceneState(deserializedSceneState);
                                    spawners.AddRange(deserializedSceneState.AllEntityData.OfType<UnitSpawnerBase.MyData>());
                                    units.AddRange(deserializedSceneState.AllEntityData.OfType<AbstractUnitEntity>());
                                } catch (IOException ex) {
                                    Mod.Log($"Exception occured while loading area state: {area.Blueprint.AssetGuidThreadSafe} {vendorScene.SceneName} " + ex);
                                }
                            }
                        }
                    }

                    foreach (var data in spawners) {
                        var reference = data.SpawnedUnit;
                        var Id = reference.GetId();
                        var unit = units.FirstOrDefault(u => u.UniqueId == Id);
                        reference.m_Proxy.Entity = unit;
                        if (unit != null) {
                            var parts = unit.Parts;
                            if (parts != null && parts.Owner == null) {
                                parts.Owner = unit;
                            }
                            var vendor = unit.Parts.GetOptional<PartVendor>();
                            vendor.SetSharedInventory(vendor.m_SharedInventory);

                            var uiSettings = unit.Parts.GetOptional<PartUnitUISettings>();
                            (uiSettings as EntityPart).Owner = unit;
                        }
                        area.MainState.AllEntityData.Add(data);
                        entities.Add(data);
                    }
                    action.Run();

                } catch (Exception ex) {
                    Mod.Log(ex.ToString());
                }
                finally {
                    foreach (var data in spawners.NotNull())
                        area.MainState.m_EntityData.Remove(data);
                }
            }
            UnityModManager.UI.Instance.ToggleWindow(false);
        }

#if false
        public static void ClearActionBar() {
            var selectedChar = Game.Instance?.SelectionCharacter?.CurrentSelectedCharacter;
            var uiSettings = selectedChar?.UISettings;
            uiSettings?.CleanupSlots();
            uiSettings.Dirty = true;
        }
        public static bool HasAbility(this UnitEntityData ch, BlueprintAbility ability) {
            if (ability.IsSpell) {
                if (PartyEditor.IsOnPartyEditor() && PartyEditor.SelectedSpellbook.TryGetValue(ch.HashKey(), out var selectedSpellbook)) {
                    return UIUtilityUnit.SpellbookHasSpell(selectedSpellbook, ability);
                }
            }
            return ch.Spellbooks.Any(spellbook => spellbook.IsKnown(ability)) || ch.Descriptor().Abilities.HasFact(ability);
        }
        public static bool CanAddAbility(this UnitEntityData ch, BlueprintAbility ability) {
            if (ability.IsSpell) {
                if (PartyEditor.SelectedSpellbook.TryGetValue(ch.HashKey(), out var selectedSpellbook)) {
                    return !selectedSpellbook.IsKnown(ability) &&
                           (ability.IsInSpellList(selectedSpellbook.Blueprint.SpellList) || Main.Settings.showFromAllSpellbooks || PartyEditor.selectedSpellbookLevel == (selectedSpellbook.Blueprint.MaxSpellLevel + 1));
                }

                foreach (var spellbook in ch.Spellbooks) {
                    if (spellbook.IsKnown(ability)) return false;
                    var spellbookBP = spellbook.Blueprint;
                    var maxLevel = spellbookBP.MaxSpellLevel;
                    for (var level = 0; level <= maxLevel; level++) {
                        var learnable = spellbookBP.SpellList.GetSpells(level);
                        if (learnable.Contains(ability)) {
                            //                            Logger.Log($"found spell {ability.Name} in {learnable.Count()} level {level} spells");
                            return true; ;
                        }
                    }
                }
            }
            else {
                if (!ch.Descriptor().Abilities.HasFact(ability)) return true;
            }
            return false;
        }
        public static void AddAbility(this UnitEntityData ch, BlueprintAbility ability) {
            if (ability.IsSpell) {
                if (CanAddAbility(ch, ability)) {
                    if (PartyEditor.IsOnPartyEditor() && PartyEditor.SelectedSpellbook.TryGetValue(ch.HashKey(), out var selectedSpellbook)) {
                        var level = PartyEditor.selectedSpellbookLevel;
                        if (level == selectedSpellbook.Blueprint.MaxSpellLevel + 1)
                            level = PartyEditor.newSpellLvl;
                        selectedSpellbook.AddKnown(level, ability);
                        return;
                    }
                }

                Mod.Trace($"adding spell: {ability.Name}");
                foreach (var spellbook in ch.Spellbooks) {
                    var spellbookBP = spellbook.Blueprint;
                    var maxLevel = spellbookBP.MaxSpellLevel;
                    Mod.Trace($"checking {spellbook.Blueprint.Name} maxLevel: {maxLevel}");
                    for (var level = 0; level <= maxLevel; level++) {
                        var learnable = spellbookBP.SpellList.GetSpells(level);
                        var allowsSpell = learnable.Contains(ability);
                        var allowText = allowsSpell ? "FOUND" : "did not find";
                        Mod.Trace($"{allowText} spell {ability.Name} in {learnable.Count()} level {level} spells");
                        if (allowsSpell) {
                            Mod.Trace($"spell level = {level}");
                            spellbook.AddKnown(level, ability);
                        }
                    }
                }
            }
            else {
                ch.Descriptor().AddFact(ability);
            }
        }
        public static bool CanAddSpellAsAbility(this UnitEntityData ch, BlueprintAbility ability) => ability.IsSpell && !ch.Descriptor().HasFact(ability) && !PartyEditor.IsOnPartyEditor();
        public static void AddSpellAsAbility(this UnitEntityData ch, BlueprintAbility ability) => ch.Descriptor().AddFact(ability);
        public static void RemoveAbility(this UnitEntityData ch, BlueprintAbility ability) {
            if (ability.IsSpell) {
                if (PartyEditor.IsOnPartyEditor() && PartyEditor.SelectedSpellbook.TryGetValue(ch.HashKey(), out var selectedSpellbook)) {
                    if (UIUtilityUnit.SpellbookHasSpell(selectedSpellbook, ability)) {
                        selectedSpellbook.RemoveSpell(ability);
                        return;
                    }
                }
                foreach (var spellbook in ch.Spellbooks) {
                    if (UIUtilityUnit.SpellbookHasSpell(spellbook, ability)) {
                        spellbook.RemoveSpell(ability);
                    }
                }
            }
            var abilities = ch.Descriptor().Abilities;
            if (abilities.HasFact(ability)) abilities.RemoveFact(ability);
        }
        public static void ResetMythicPath(this UnitEntityData ch) {
            //            ch.Descriptor().Progression.RemoveMythicLevel
        }
        public static void resetClassLevel(this UnitEntityData ch) {
            var level = ch.Descriptor().Progression.MaxCharacterLevel;
            var xp = ch.Descriptor().Progression.Experience;
            var xpTable = ch.Descriptor().Progression.ExperienceTable;

            for (var i = ch.Descriptor().Progression.MaxCharacterLevel; i >= 1; i--) {
                var xpBonus = xpTable.GetBonus(i);

                Mod.Trace(i + ": " + xpBonus + " | " + xp);

                if ((xp - xpBonus) >= 0) {
                    Mod.Trace(i + ": " + (xp - xpBonus));
                    level = i;
                    break;
                }
            }
            ch.Descriptor().Progression.CharacterLevel = level;
        }

        public static void CreateArmy(BlueprintArmyPreset bp, bool friendlyorhostile) {
            var playerPosition = Game.Instance.Player.GlobalMap.CurrentPosition;
            if (friendlyorhostile) {
                Game.Instance.Player.GlobalMap.LastActivated.CreateArmy(ArmyFaction.Crusaders, bp, playerPosition);
            }
            else {
                Game.Instance.Player.GlobalMap.LastActivated.CreateArmy(ArmyFaction.Demons, bp, playerPosition);
            }
        }

        public static void AddSkillToLeader(BlueprintLeaderSkill bp) {
            var selectedArmy = Game.Instance.GlobalMapController.SelectedArmy;
            if (selectedArmy == null || selectedArmy.Data.Leader == null) {
                Mod.Trace($"Choose an army with a leader!");
                return;
            }
            var leader = selectedArmy.Data.Leader;
            leader.AddSkill(bp, true);
        }

        public static void RemoveSkillFromLeader(BlueprintLeaderSkill bp) {
            var selectedArmy = Game.Instance.GlobalMapController.SelectedArmy;
            if (selectedArmy == null || selectedArmy.Data.Leader == null) {
                Mod.Trace($"Choose an army with a leader!");
                return;
            }
            var leader = selectedArmy.Data.Leader;
            leader.RemoveSkill(bp);
        }

        public static bool LeaderHasSkill(BlueprintLeaderSkill bp) {
            var selectedArmy = Game.Instance.GlobalMapController.SelectedArmy;
            if (selectedArmy == null || selectedArmy.Data.Leader == null) {
                Mod.Trace($"Choose an army with a leader!");
                return false;
            }
            var leader = selectedArmy.Data.Leader;
            return leader.m_Skills.Contains(bp);
        }

        public static bool LeaderSelected(BlueprintLeaderSkill bp) {
            var selectedArmy = Game.Instance.GlobalMapController.SelectedArmy;
            if (selectedArmy == null || selectedArmy.Data.Leader == null) {
                return false;
            }
            return true;
        }

        // can potentially go back in time but some parts of the game don't expect it
        public static void KingdomTimelineAdvanceDays(int days) {
            var kingdom = KingdomState.Instance;
            var timelineManager = kingdom.TimelineManager;

            // from KingdomState.SkipTime
            foreach (var kingdomTask in kingdom.ActiveTasks) {
                if (!kingdomTask.IsFinished && !kingdomTask.IsStarted && !kingdomTask.NeedsCommit && kingdomTask.HasAssignedLeader) {
                    kingdomTask.Start(true);
                }
            }

            // from KingdomTimelineManager.Advance
            if (!KingdomTimelineManager.CanAdvanceTime()) {
                return;
            }
            Game.Instance.AdvanceGameTime(TimeSpan.FromDays(days));
            if (Game.Instance.IsModeActive(GameModeType.Kingdom)) {
                foreach (var unitEntityData in Game.Instance.Player.AllCharacters) {
                    RestController.ApplyRest(unitEntityData.Descriptor);
                }
            }

            timelineManager.UpdateTimeline();
        }
#endif
    }
}
