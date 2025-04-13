using HarmonyLib;
using Polytopia.Data;
using UI.Popups;
using UnityEngine;

namespace PolyPlus {
    public static class PolyPlusPatcher
    {
        private static bool unlockRoutes = false;
        private static bool unrobCity = false;
        private static bool denyCloakAttackIncome = false;
        public static void Load()
        {
            Harmony.CreateAndPatchAll(typeof(PolyPlusPatcher));
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
                if (tribeData.tribeAbilities.Contains(EnumCache<TribeAbility.Type>.GetType("citypark")))
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(EmbarkAction), nameof(EmbarkAction.Execute))]
        private static void EmbarkAction_ExecuteDefault(EmbarkAction __instance, GameState gameState)
	    {
            PlayerState playerState;
            if (gameState.TryGetPlayer(__instance.PlayerId, out playerState))
            {
                TileData tile = gameState.Map.GetTile(__instance.Coordinates);
                UnitState unitState = tile.unit;
                if(PlayerExtensions.HasAbility(playerState, EnumCache<PlayerAbility.Type>.GetType("dashembark"), gameState))
                {
                    unitState.moved = false;
                    unitState.attacked = false;
                    // LevelManager.GetClientInteraction().SelectUnit(MapRenderer.Current.GetUnitInstance(unitState.id)); // That did not work
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
                __result /= 2;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TileData), nameof(TileData.GetMovementCost))]
        private static void TileData_GetMovementCost(ref int __result, TileData __instance, MapData map, TileData fromTile, PathFinderSettings settings)
        {
            UnitState unit = settings.unit;
            if (unit != null && __result == 5 && settings.unitData.HasAbility(UnitAbility.Type.Skate))
            {
                __result = 10;
            }
            if (unit != null &&
                __instance.terrain == Polytopia.Data.TerrainData.Type.Ice &&
                settings.unitData.HasAbility(EnumCache<UnitAbility.Type>.GetType("glide")))
            {
                __result = 5;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapDataExtensions), nameof(MapDataExtensions.UpdateRoutes))]
        private static bool MapDataExtensions_UpdateRoutes_Prefix(GameState gameState, Il2CppSystem.Collections.Generic.List<TileData> changedTiles)
        {
            unlockRoutes = true;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.GetMovementsWithUnlockedTeck))]
        private static void GameLogicData_GetMovementsWithUnlockedTeck(ref Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData> __result, GameLogicData __instance, Il2CppSystem.Collections.Generic.List<TechData> tech)
        {
            if(unlockRoutes)
            {
                Array values = Enum.GetValues(typeof(Polytopia.Data.TerrainData.Type));
                Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData> terrains = new Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData>();
                foreach (var item in values)
                {
                    if(__instance.TryGetData((Polytopia.Data.TerrainData.Type)item, out Polytopia.Data.TerrainData data))
                    {
                        terrains.Add(data);
                    }
                }
                __result = terrains;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MapDataExtensions), nameof(MapDataExtensions.UpdateRoutes))]
        private static void MapDataExtensions_UpdateRoutes_Postfix(GameState gameState, Il2CppSystem.Collections.Generic.List<TileData> changedTiles)
        {
            unlockRoutes = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetAttackOptionsAtPosition))]
        private static void GetAttackOptionsAtPosition(ref Il2CppSystem.Collections.Generic.List<WorldCoordinates> __result, GameState gameState, byte playerId, WorldCoordinates position, int range, bool includeHiddenTiles, UnitState customUnitState, bool ignoreDiplomacyRelation)
        {
            Il2CppSystem.Collections.Generic.List<WorldCoordinates> list = new Il2CppSystem.Collections.Generic.List<WorldCoordinates>();
            UnitState unitState = customUnitState ?? gameState.Map.GetTile(position).unit;
            Il2CppSystem.Collections.Generic.List<TileData> area = gameState.Map.GetArea(position, range, true, false);
            if (unitState != null && unitState.HasAbility(UnitAbility.Type.Infiltrate, gameState) && gameState.TryGetPlayer(playerId, out PlayerState playerState))
            {
                if (area != null && area.Count > 0)
                {
                    list = new Il2CppSystem.Collections.Generic.List<WorldCoordinates>();
                    for (int i = 0; i < area.Count; i++)
                    {
                        TileData tileData = area[i];
                        if (tileData != null)
                        {
                            bool isInPeace;
                            if (ignoreDiplomacyRelation != false) {
                                isInPeace = false;
                            }
                            else
                            {
                                isInPeace = PlayerDiplomacyExtensions.HasPeaceWith(playerState, tileData.owner);
                                if (!isInPeace) {
                                    isInPeace = PlayerDiplomacyExtensions.HasBrokenPeaceWith(playerState, tileData.owner);
                                }

                            }

                            if (tileData.HasImprovement(ImprovementData.Type.City) && tileData.owner != 0 && tileData.owner != unitState.owner && !isInPeace)
                            {
                                list.Add(tileData.coordinates);
                            }
                        }
                    }
                }
                __result = list;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TileDataExtensions), nameof(TileDataExtensions.CalculateWork), typeof(TileData), typeof(GameState), typeof(int))]
        public static bool TileDataExtensions_CalculateWork_Prefix(ref int __result, TileData tile, GameState gameState, int improvementLevel)
        {
            unrobCity = true;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ImprovementState), nameof(ImprovementState.HasEffect))]
        public static void ImprovementState_HasEffect(ref bool __result, ImprovementState __instance, ImprovementEffect effect)
        {
            if(__result && effect == ImprovementEffect.robbed && unrobCity)
                __result = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TileDataExtensions), nameof(TileDataExtensions.CalculateWork), typeof(TileData), typeof(GameState), typeof(int))]
        public static void TileDataExtensions_CalculateWork_Postfix(ref int __result, TileData tile, GameState gameState, int improvementLevel)
        {
            unrobCity = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InfiltrationRewardAction), nameof(InfiltrationRewardAction.ExecuteDefault))]
        private static bool InfiltrationRewardAction_ExecuteDefault_Prefix(InfiltrationRewardAction __instance, GameState gameState)
        {
            denyCloakAttackIncome = true;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TileDataExtensions), nameof(TileDataExtensions.CalculateRawProduction))]
        private static void TileDataExtensions_CalculateRawProduction(ref int __result, TileData tile, GameState gameState)
        {
            if(denyCloakAttackIncome)
                __result = 0;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InfiltrationRewardAction), nameof(InfiltrationRewardAction.ExecuteDefault))]
        private static void InfiltrationRewardAction_ExecuteDefault_Postfix(InfiltrationRewardAction __instance, GameState gameState)
        {
            denyCloakAttackIncome = false;
        }
    }
}
