﻿namespace PermanentCookoutKit
{
    using Microsoft.Xna.Framework;
    using StardewModdingAPI;
    using StardewModdingAPI.Events;
    using StardewValley;
    using StardewValley.GameData.Machines;
    using StardewValley.Locations;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using StardewObject = StardewValley.Object;

    public class PermanentCookoutKit : Mod
    {
        public PermanentCookoutKitConfig Config { get; set; }

        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<PermanentCookoutKitConfig>();

            PermanentCookoutKitConfig.VerifyConfigValues(Config, this);

            Helper.Events.GameLoop.GameLaunched += delegate { PermanentCookoutKitConfig.SetUpModConfigMenu(Config, this); };

            Helper.Events.GameLoop.DayEnding += delegate { SaveCookingKits(); };

            Helper.Events.GameLoop.DayStarted += delegate { ManageLinusFire(); };

            Helper.Events.Content.AssetRequested += OnAssetRequested;

            Patcher.PatchAll(this);
        }

        private static readonly Vector2 linusCampfireLocation = new(29f, 9f);

        private void ManageLinusFire()
        {
            if (!Config.LinusTakesCareOfHisFire)
            {
                return;
            }

            Utility.ForEachLocation(delegate (GameLocation location)
            {
                if (location is not Mountain)
                {
                    return true;
                }

                if (location.Objects.TryGetValue(linusCampfireLocation, out var campfire))
                {
                    TurnOffSingleCookingKitInTheRain(campfire, location.IsRainingHere());
                }

                return true;
            });
        }

        private void TurnOffSingleCookingKitInTheRain(StardewObject item, bool isRaining)
        {
            if (item is Torch torch && torch.bigCraftable.Value && torch.QualifiedItemId == "(BC)146" && torch.Fragility == 2)
            {
                if (!Config.OutdoorRainPreventsIgniting)
                {
                    if (!torch.IsOn)
                    {
                        torch.checkForAction(null);
                    }
                }
                else
                {
                    if ((torch.IsOn && isRaining) || (!torch.IsOn && !isRaining))
                    {
                        torch.checkForAction(null);
                    }
                }
            }
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
            {
                e.Edit((asset) =>
                {
                    IDictionary<string, string> data = asset.AsDictionary<string, string>().Data;

                    var cookoutKitRecipeName = "Cookout Kit";

                    if (data.TryGetValue(cookoutKitRecipeName, out var val))
                    {
                        var index = val.IndexOf('/');

                        // "388 15 771 10 382 3/Field/926/false/Foraging 3/"

                        if (index > 0)
                        {
                            data[cookoutKitRecipeName] = "390 10 388 10 771 10 382 3 335 1" + val[index..];
                        }
                    }
                }, AssetEditPriority.Late);
            }

            if (e.NameWithoutLocale.IsEquivalentTo("Data/Machines"))
            {
                e.Edit((asset) =>
                {
                    IDictionary<string, MachineData> data = asset.AsDictionary<string, MachineData>().Data;

                    var charcoalKilnId = "(BC)114";

                    if (data.TryGetValue(charcoalKilnId, out var machineData))
                    {
                        var driftwoodRuleID = ModManifest.UniqueID + "_Driftwood";
                        var hardwoodRuleID = ModManifest.UniqueID + "_Hardwood";

                        machineData.OutputRules.RemoveAll((r) => r.Id == driftwoodRuleID || r.Id == hardwoodRuleID);

                        var defaultRule = machineData.OutputRules.FirstOrDefault((r) => r.Id == "Default");

                        if (defaultRule != null)
                        {
                            defaultRule.MinutesUntilReady = Config.CharcoalKilnTimeNeeded;

                            var defaultTrigger = defaultRule.Triggers.FirstOrDefault((t) => t.Id == "ItemPlacedInMachine");

                            if (defaultTrigger != null)
                            {
                                defaultTrigger.RequiredCount = Config.CharcoalKilnWoodNeeded;

                                if (Config.DriftwoodMultiplier > 0)
                                {
                                    var driftwoodRule = CreateOutputRuleFromDefault(defaultRule, driftwoodRuleID);
                                    var driftwoodTriggerRule = CreateTriggerRule(driftwoodRule, Patcher.DriftwoodID, Config.DriftwoodMultiplier);
                                    driftwoodRule.Triggers = new List<MachineOutputTriggerRule>() { driftwoodTriggerRule };
                                    machineData.OutputRules.Add(driftwoodRule);
                                }

                                if (Config.HardwoodMultiplier > 0)
                                {
                                    var hardwoodRule = CreateOutputRuleFromDefault(defaultRule, hardwoodRuleID);
                                    var hardwoodTriggerRule = CreateTriggerRule(hardwoodRule, Patcher.HardwoodID, Config.HardwoodMultiplier);
                                    hardwoodRule.Triggers = new List<MachineOutputTriggerRule>() { hardwoodTriggerRule };
                                    machineData.OutputRules.Add(hardwoodRule);
                                }
                            }
                        }

                        data[charcoalKilnId] = machineData;
                    }
                }, AssetEditPriority.Late);
            }
        }

        private static MachineOutputRule CreateOutputRuleFromDefault(MachineOutputRule sourceRule, string ruleID)
        {
            var newRule = new MachineOutputRule
            {
                Id = ruleID,
                OutputItem = sourceRule.OutputItem,
                InvalidCountMessage = sourceRule.InvalidCountMessage,
                UseFirstValidOutput = sourceRule.UseFirstValidOutput,
                MinutesUntilReady = sourceRule.MinutesUntilReady,
                DaysUntilReady = sourceRule.DaysUntilReady,
                RecalculateOnCollect = sourceRule.RecalculateOnCollect
            };

            return newRule;
        }

        private MachineOutputTriggerRule CreateTriggerRule(MachineOutputRule sourceRule, string requiredItemId, float multiplier)
        {
            return new MachineOutputTriggerRule
            {
                Id = sourceRule.Id + "_ItemPlacedInMachine",
                Trigger = MachineOutputTrigger.ItemPlacedInMachine,
                RequiredItemId = requiredItemId,
                RequiredCount = Patcher.CountWithMultiplier(Config.CharcoalKilnWoodNeeded, multiplier)
            };
        }

        public void DebugLog(object o)
        {
            Monitor.Log(o == null ? "null" : o.ToString(), LogLevel.Debug);
        }

        public void ErrorLog(object o, Exception e = null)
        {
            string baseMessage = o == null ? "null" : o.ToString();

            string errorMessage = e == null ? string.Empty : $"\n{e.Message}\n{e.StackTrace}";

            Monitor.Log(baseMessage + errorMessage, LogLevel.Error);
        }

        private static void SaveCookingKits()
        {
            Utility.ForEachLocation(delegate (GameLocation location)
            {
                foreach (var item in location.Objects.Values)
                {
                    SaveSingleKit(item);
                }

                return true;
            });
        }

        private static void SaveSingleKit(StardewObject item)
        {
            if (item.IsCookoutKit())
            {
                // extinguishes the fire, does not truly remove the object
                item.performRemoveAction();

                item.destroyOvernight = false;
            }
        }
    }

    public static class ExtensionMethods
    {
        public static bool IsCookoutKit(this StardewObject o)
        {
            return o != null && o.QualifiedItemId == "(BC)278";
        }

        public static bool IsCookoutKitSpawnItem(this StardewObject o)
        {
            return o != null && o.QualifiedItemId == "(O)926";
        }
    }

    /// <summary>
    /// Extension methods for IGameContentHelper.
    /// </summary>
    public static class GameContentHelperExtensions
    {
        /// <summary>
        /// Invalidates both an asset and the locale-specific version of an asset.
        /// </summary>
        /// <param name="helper">The game content helper.</param>
        /// <param name="assetName">The (string) asset to invalidate.</param>
        /// <returns>if something was invalidated.</returns>
        public static bool InvalidateCacheAndLocalized(this IGameContentHelper helper, string assetName)
            => helper.InvalidateCache(assetName)
                | (helper.CurrentLocaleConstant != LocalizedContentManager.LanguageCode.en && helper.InvalidateCache(assetName + "." + helper.CurrentLocale));
    }
}