using HarmonyLib;
using Polytopia.Data;
using UnityEngine;

namespace PolyPlus {
    public class PolyPlusPatcher
    {
        public static void Load()
        {
            CreateEnumCaches();
            Harmony.CreateAndPatchAll(typeof(PolyPlusPatcher));
        }

        internal static void CreateEnumCaches()
        {
            Console.Write("Created mapping for ImprovementAbility.Type with id waterembark and index " + PolyMod.ModLoader.autoidx);
            EnumCache<ImprovementAbility.Type>.AddMapping("halved", (ImprovementAbility.Type)PolyMod.ModLoader.autoidx);
			EnumCache<ImprovementAbility.Type>.AddMapping("halved", (ImprovementAbility.Type)PolyMod.ModLoader.autoidx);
            PolyMod.ModLoader.autoidx++;
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
            if(__instance.unitAbilities.Contains(EnumCache<UnitAbility.Type>.GetType("polyplusstatic")))
            {
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.IsTileAccessible))]
        private static void PathFinder_IsTileAccessible(ref bool __result, TileData tile, TileData origin, PathFinderSettings settings)
	    {
            if(PlayerExtensions.HasAbility(settings.playerState, EnumCache<PlayerAbility.Type>.GetType("waterembark"), settings.gameState) && tile.IsWater && !origin.IsWater && settings.unit != null){
                if((tile.terrain == Polytopia.Data.TerrainData.Type.Water && settings.allowedTerrain.Contains(Polytopia.Data.TerrainData.Type.Water)) || (tile.terrain == Polytopia.Data.TerrainData.Type.Ocean && settings.allowedTerrain.Contains(Polytopia.Data.TerrainData.Type.Ocean)))
                {
                    __result = true;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.GetMoveOptions))]
        private static void PathFinder_GetMoveOptions(ref Il2CppSystem.Collections.Generic.List<WorldCoordinates> __result,  GameState gameState, WorldCoordinates start, int maxCost, UnitState unit)
        {
            PlayerState playerState;
            TileData startTile = GameManager.GameState.Map.GetTile(start);
            Il2CppSystem.Collections.Generic.List<WorldCoordinates> options = __result;
            List<WorldCoordinates> toRemove = new List<WorldCoordinates>();
            if(gameState.TryGetPlayer(unit.owner, out playerState))
            {
                if(PlayerExtensions.HasAbility(playerState, EnumCache<PlayerAbility.Type>.GetType("waterembark"), gameState) && !unit.HasAbility(UnitAbility.Type.Fly, gameState))
                {
                    foreach (WorldCoordinates destination in options)
                    {
                        if(!startTile.IsWater)
                        {
                            Il2CppSystem.Collections.Generic.List<WorldCoordinates> path = PathFinder.GetPath(gameState, start, destination, maxCost, unit);
                            path.Reverse();
                            bool hadWater = false;
                            foreach (WorldCoordinates pathTile in path)
                            {
                                TileData tile = gameState.Map.GetTile(pathTile);
                                if(hadWater)
                                {
                                    toRemove.Add(tile.coordinates);
                                }
                                if(tile.terrain == Polytopia.Data.TerrainData.Type.Water || tile.terrain == Polytopia.Data.TerrainData.Type.Ocean)
                                {
                                    if(tile.improvement != null)
                                    {
                                        if(gameState.GameLogicData.TryGetData(tile.improvement.type, out ImprovementData imrovementData))
                                        {
                                            if(!imrovementData.HasAbility(ImprovementAbility.Type.Bridge))
                                            {
                                                hadWater = true;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        hadWater = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            foreach (var item in toRemove)
            {
                options.Remove(item);
            }
            __result = options;
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionUtils), nameof(ActionUtils.CalculateImprovementLevel))]
        private static void ActionUtils_CalculateImprovementLevel(ref int __result, GameState gameState, TileData tile)
        {
            if(tile.improvement != null)
            {
                if (gameState.GameLogicData.TryGetData(tile.improvement.type, out ImprovementData improvementData))
                {
                    if(improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("halved")))
                    {
                        __result = __result / 2;
                        return;
                    }
                }
            }
        }

	[HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerDiplomacyExtensions), nameof(PlayerDiplomacyExtensions.GetIncomeFromEmbassy))]
	private static void PlayerDiplomacyExtensions_GetIncomeFromEmbassy(ref int __result, PlayerState playerState, PlayerState otherPlayer, GameState gameState)
	{
		if(playerState.HasPeaceWith(otherPlayer.Id))
			__result = __result / 2
	}

	[HarmonyPostfix]
        [HarmonyPatch(typeof(TileData), nameof(TileData.GetMovementCost))]
	private static void TileData_GetMovementCost(ref int __result, MapData map, TileData fromTile, PathFinderSettings settings)
	{
		UnitState unit = settings.unit;
		if (unit != null && __result == 5 && settings.unitData.HasAbility(UnitAbility.Type.Skate))
		{
			__result = 10;
		}
	}
    }
}
