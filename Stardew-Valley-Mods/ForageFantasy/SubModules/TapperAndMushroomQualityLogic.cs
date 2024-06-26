﻿namespace ForageFantasy
{
    using StardewModdingAPI;
    using StardewValley;
    using StardewValley.TerrainFeatures;
    using System;
    using StardewObject = StardewValley.Object;

    public static class ExtensionMethods
    {
        public static bool IsMushroomBox(this StardewObject o)
        {
            return o != null && o.QualifiedItemId == "(BC)128";
        }
    }

    internal class TapperAndMushroomQualityLogic
    {
        public static int GetTapperProductValueForDaysNeeded(int daysNeeded)
        {
            return (int)Math.Round(daysNeeded * (150f / 7f), MidpointRounding.AwayFromZero);
        }

        public static void IncreaseTreeAges(ForageFantasy mod)
        {
            if (!Context.IsMainPlayer)
            {
                return;
            }

            Utility.ForEachLocation(delegate (GameLocation location)
            {
                foreach (var terrainfeature in location.terrainFeatures.Pairs)
                {
                    if (terrainfeature.Value is Tree tree)
                    {
                        IncreaseTreeAge(mod, tree);
                    }
                }

                return true;
            });
        }

        public static void IncreaseTreeAge(ForageFantasy mod, Tree tree)
        {
            if (tree.growthStage.Value < 5)
            {
                return;
            }

            tree.modData.TryGetValue($"{mod.ModManifest.UniqueID}/treeAge", out string moddata);

            if (!string.IsNullOrEmpty(moddata))
            {
                int age = int.Parse(moddata);
                tree.modData[$"{mod.ModManifest.UniqueID}/treeAge"] = (age + 1).ToString();
            }
            else
            {
                tree.modData[$"{mod.ModManifest.UniqueID}/treeAge"] = 1.ToString();
            }
        }

        public static int DetermineTapperQuality(ForageFantasyConfig config, Farmer player, Tree tree)
        {
            switch (config.TapperQualityOptions)
            {
                case 1:
                case 2:
                    // has tapper profession or it's not required
                    if (!config.TapperQualityRequiresTapperPerk || player.professions.Contains(Farmer.tapper))
                    {
                        Random r = Utility.CreateDaySaveRandom(tree.Tile.X, tree.Tile.Y * 777f);
                        return ForageFantasy.DetermineForageQuality(player, r, config.TapperQualityOptions == 1);
                    }
                    break;

                case 3:
                case 4:
                    // quality increase once a year
                    return DetermineTreeQuality(config, tree);
            }

            // tapper perk required but doesn't have it or invalid option
            return 0;
        }

        public static int DetermineTreeQuality(ForageFantasyConfig config, Tree tree)
        {
            tree.modData.TryGetValue($"{ForageFantasy.Manifest.UniqueID}/treeAge", out string moddata);

            if (!string.IsNullOrEmpty(moddata))
            {
                int age = int.Parse(moddata);

                bool useMonths = config.TapperQualityOptions == 3;

                int timeForLevelUp = useMonths ? 28 : 28 * 4;

                if (age < timeForLevelUp)
                {
                    return 0;
                }
                else if (age < timeForLevelUp * 2)
                {
                    return 1;
                }
                else if (age < timeForLevelUp * 3)
                {
                    return 2;
                }
                else
                {
                    return 4;
                }
            }

            return 0;
        }
    }
}