using HarmonyLib;
using Polytopia.Data;
using UnityEngine;

namespace PolyPlus {
    public class PolyPlusPatcher
    {
        private static string version = "0.0.12";
        private static string branch = "waterembark";
        internal static readonly string BASE_PATH = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "..");
        public static void Load()
        {
            Console.WriteLine("Loading PolyPlus Polyscript of version " + version + " of branch " + branch + "...");
            CreateEnumCaches();
            Harmony.CreateAndPatchAll(typeof(PolyPlusPatcher));
            Console.WriteLine("PolyPlus Polyscript Loaded!");
        }

        internal static void CreateEnumCaches()
        {
            Console.Write("Created mapping for PlayerAbility.Type with id waterembark and index " + PolyMod.ModLoader.autoidx);
            EnumCache<PlayerAbility.Type>.AddMapping("waterembark", (PlayerAbility.Type)PolyMod.ModLoader.autoidx);
			EnumCache<PlayerAbility.Type>.AddMapping("waterembark", (PlayerAbility.Type)PolyMod.ModLoader.autoidx);
            PolyMod.ModLoader.autoidx++;
            Console.Write("Created mapping for PlayerAbility.Type with id polyplusstatic and index " + PolyMod.ModLoader.autoidx);
            EnumCache<UnitAbility.Type>.AddMapping("polyplusstatic", (UnitAbility.Type)PolyMod.ModLoader.autoidx);
			EnumCache<UnitAbility.Type>.AddMapping("polyplusstatic", (UnitAbility.Type)PolyMod.ModLoader.autoidx);
            PolyMod.ModLoader.autoidx++;
            int tribeBonusAutoidx = (int)Enum.GetValues(typeof(TribeData.BonusEnum)).Cast<TribeData.BonusEnum>().Last();
            tribeBonusAutoidx++;
            Console.Write("Created mapping for TribeData.BonusEnum with id citypark and index " + tribeBonusAutoidx);
            EnumCache<TribeData.BonusEnum>.AddMapping("citypark", (TribeData.BonusEnum)tribeBonusAutoidx);
			EnumCache<TribeData.BonusEnum>.AddMapping("citypark", (TribeData.BonusEnum)tribeBonusAutoidx);
        }

		[HarmonyPostfix]
		[HarmonyPatch(typeof(UnitData), nameof(UnitData.getPromotionLimit))]
        private static void UnitData_getPromotionLimit(ref int __result, UnitData __instance, PlayerState player, GameState gameState)
		{
            if(__instance.unitAbilities.Contains(EnumCache<UnitAbility.Type>.GetType("polyplusstatic"))){
                __result = 0;
            }
		}

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UnitPopup), nameof(UnitPopup.UnitData), MethodType.Setter)]
        private static void UnitPopup_UnitData_Set(UnitPopup __instance)
        {
            Vector2 anchoredPosition = __instance.iconContainer.anchoredPosition;
            string improvementName = string.Empty;
            if (__instance.Unit != null && GameManager.GameState != null && GameManager.GameState.Map != null)
            {
                TileData tile = GameManager.GameState.Map.GetTile(__instance.Unit.UnitState.home);
                if (tile != null && tile.HasImprovement(ImprovementData.Type.City))
                {
                    improvementName = tile.improvement.name;
                }
            }
            string unitDescription = string.IsNullOrEmpty(improvementName) ? string.Empty : string.Format("{0}\n", Localization.Get("world.unit.info.from", new Il2CppSystem.Object[] { improvementName }));
            string unitProgressText;
            int killCount = (int)(__instance.Unit ? __instance.Unit.UnitState.xp : 0);
            if (UIManager.Instance.CurrentScreen != UIConstants.Screens.TechTree && __instance.unit != null && __instance.unit.unitData.HasAbility(EnumCache<UnitAbility.Type>.GetType("polyplusstatic")))
            {
                unitProgressText = Localization.Get("polyplus.unit.veteran.static.progress", new Il2CppSystem.Object[]
                {
                    killCount.ToString(),
                });
                __instance.Description = string.Format("{0}{1}", unitDescription, unitProgressText);
                anchoredPosition.x = 48f;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.SetTileAsCapital))]
        private static void MapGenerator_SetTileAsCapital(GameState gameState, PlayerState playerState, TileData tile)
	    {
            TribeData tribeData;
            if (tile != null && gameState.GameLogicData.TryGetData(playerState.tribe, out tribeData))
            {
                if (tribeData.bonus == EnumCache<TribeData.BonusEnum>.GetType("citypark"))
                {
                    tile.improvement.production = 2;
                    tile.improvement.baseScore += 250;
                    tile.improvement.AddReward(CityReward.Park);
                }
            }
        }

        //[HarmonyPostfix]
        //[HarmonyPatch(typeof(PathFinderSettings), nameof(PathFinderSettings.CreateForUnit))]
        private static void PathFinder_CreateForUnit(ref PathFinderSettings __result, UnitState unit, GameState gameState)
	    {
            PlayerState player;
            if(gameState.TryGetPlayer(unit.owner, out player))
            {
                if(PlayerExtensions.HasAbility(player, EnumCache<PlayerAbility.Type>.GetType("waterembark"), gameState))
                {
                    __result.isRequiredToUsePortToGoIntoWater = false;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.IsTileAccessible))]
        private static void PathFinder_IsTileAccessible(ref bool __result, TileData tile, TileData origin, PathFinderSettings settings)
	    {
            if(PlayerExtensions.HasAbility(settings.playerState, EnumCache<PlayerAbility.Type>.GetType("waterembark"), settings.gameState) && tile.IsWater && !origin.IsWater && settings.unit != null){
                if((tile.terrain == Polytopia.Data.TerrainData.Type.Water && settings.allowedTerrain.Contains(Polytopia.Data.TerrainData.Type.Water)) || (tile.terrain == Polytopia.Data.TerrainData.Type.Ocean && settings.allowedTerrain.Contains(Polytopia.Data.TerrainData.Type.Ocean)))
                {
                    __result = true;
                }
                else
                {
                    __result = false;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MoveAction), nameof(MoveAction.ExecuteDefault))]
        private static void MoveAction_ExecuteDefault(MoveAction __instance, GameState gameState)
	    {
            UnitState unitState;
            PlayerState playerState;
            UnitData unitData;
            if (gameState.TryGetUnit(__instance.UnitId, out unitState) && gameState.TryGetPlayer(__instance.PlayerId, out playerState) && gameState.GameLogicData.TryGetData(unitState.type, out unitData))
            {
                WorldCoordinates worldCoordinates = __instance.Path[0];
                TileData tile2 = gameState.Map.GetTile(worldCoordinates);
                tile2.SetUnit(unitState);
                unitState.coordinates = worldCoordinates;
                bool hasNoBridge = true;
                if(tile2.improvement != null)
                {
                    if(gameState.GameLogicData.TryGetData(tile2.improvement.type, out ImprovementData imrovementData))
                    {
                        if(imrovementData.HasAbility(ImprovementAbility.Type.Bridge))
                        {
                            hasNoBridge = false;
                        }
                    }
                }
                if(hasNoBridge && !unitData.IsAquatic() && !unitState.HasAbility(UnitAbility.Type.Fly, gameState) && tile2.IsWater && PlayerExtensions.HasAbility(playerState, EnumCache<PlayerAbility.Type>.GetType("waterembark"), gameState))
                {
                    gameState.ActionStack.Add(new EmbarkAction(__instance.PlayerId, worldCoordinates));
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ExamineRuinsAction), nameof(ExamineRuinsAction.ExecuteDefault))]
        private static bool ExecuteDefault(ExamineRuinsAction __instance, GameState gameState)
        {
            if(__instance.Reward == RuinsReward.City)
            {
                RuinsReward[] excludedValues = new RuinsReward[]{ RuinsReward.None, RuinsReward.City, RuinsReward.SuperUnit, RuinsReward.Battleship, RuinsReward.Seamonster};
                Array values = Enum.GetValues(typeof(RuinsReward));

                var filteredValues = values.Cast<RuinsReward>().Where(v => !excludedValues.Contains(v)).ToArray();
                System.Random random = new System.Random();
                __instance.Reward = filteredValues[random.Next(filteredValues.Length)];
            }
            return true;
        }
    }
}