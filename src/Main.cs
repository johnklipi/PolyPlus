using System.Globalization;
using HarmonyLib;
using Polytopia.Data;
using UnityEngine;
using PolytopiaBackendBase.Common;
using Steamworks.Data;

namespace PolyPlus
{
    public static class Main
    {
        private static bool unlockRoutes = false;
        private static Color32 bloomColor = new Color32(255, 105, 225, 255);

        public static void Load()
        {
            PolyMod.Loader.AddPatchDataType("tileEffect", typeof(TileData.EffectType));
            Harmony.CreateAndPatchAll(typeof(Main));
            Harmony.CreateAndPatchAll(typeof(ApiHandler));
            Harmony.CreateAndPatchAll(typeof(ApiParser));
            Harmony.CreateAndPatchAll(typeof(Diplomacy));
            Harmony.CreateAndPatchAll(typeof(Generation));
            Harmony.CreateAndPatchAll(typeof(Movement));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UnitData), nameof(UnitData.getPromotionLimit))]
        private static void UnitData_getPromotionLimit(ref int __result, UnitData __instance, PlayerState player, GameState gameState)
        {
            if (__instance.unitAbilities.Contains(EnumCache<UnitAbility.Type>.GetType("staticplus")))
                __result = 0;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UnitPopup), nameof(UnitPopup.UnitData), MethodType.Setter)]
        private static void UnitPopup_UnitData_Set(UnitPopup __instance)
        {
            if (
                UIManager.Instance.CurrentScreen != UIConstants.Screens.TechTree
                && __instance.unit != null
                && __instance.unit.unitData.HasAbility(
                    EnumCache<UnitAbility.Type>.GetType("staticplus")
                )
            )
            {
                int killCount = (int)(__instance.Unit ? __instance.Unit!.UnitState.xp : 0);
                string oldProgressText = Localization.Get(
                    "world.unit.veteran.progress",
                    new Il2CppSystem.Object[] { killCount.ToString(), 0 }
                );
                string unitProgressText = Localization.Get(
                    "polyplus.unit.veteran.static.progress",
                    new Il2CppSystem.Object[] { killCount.ToString() }
                );
                Console.Write(oldProgressText);
                __instance.Description = __instance.Description.Replace(oldProgressText, unitProgressText);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ExamineRuinsAction), nameof(ExamineRuinsAction.ExecuteDefault))]
        private static bool ExamineRuinsAction_ExecuteDefault(ExamineRuinsAction __instance, GameState gameState)
        {
            if (__instance.Reward == RuinsReward.City)
            {
                RuinsReward[] excludedValues = new RuinsReward[]
                {
                    RuinsReward.None,
                    RuinsReward.City,
                    RuinsReward.SuperUnit,
                    RuinsReward.Battleship,
                    RuinsReward.Seamonster,
                };
                Array values = Enum.GetValues(typeof(RuinsReward));

                var filteredValues = values
                    .Cast<RuinsReward>()
                    .Where(v => !excludedValues.Contains(v))
                    .ToArray();
                System.Random random = new System.Random(gameState.Seed);
                __instance.Reward = filteredValues[random.Next(filteredValues.Length)];
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionUtils), nameof(ActionUtils.CalculateImprovementLevel))]
        private static void ActionUtils_CalculateImprovementLevel(ref int __result, GameState gameState, TileData tile)
        {
            if (tile.improvement == null)
                return;
            if (!gameState.GameLogicData.TryGetData(tile.improvement.type, out ImprovementData improvementData))
                return;
            if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("halved")))
                __result /= 2;
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
        private static void GameLogicData_GetMovementsWithUnlockedTeck(
            ref Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData> __result, GameLogicData __instance, Il2CppSystem.Collections.Generic.List<TechData> tech
        )
        {
            if (unlockRoutes)
            {
                Array values = Enum.GetValues(typeof(Polytopia.Data.TerrainData.Type));
                Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData> terrains =
                    new Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData>();
                foreach (var item in values)
                {
                    if (
                        __instance.TryGetData(
                            (Polytopia.Data.TerrainData.Type)item,
                            out Polytopia.Data.TerrainData data
                        )
                    )
                    {
                        terrains.Add(data);
                    }
                }
                unlockRoutes = false;
                __result = terrains;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetDefenceBonus))]
        private static void UnitDataExtensions_GetDefenceBonus(ref int __result, UnitState unit, GameState gameState)
        {
            if (__result == 15 && !unit.HasAbility(UnitAbility.Type.Fortify))
            {
                __result = 10;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
        private static void GameLogicData_CanBuild(ref bool __result, GameLogicData __instance, GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement)
        {
            if (tile.unit == null)
                return;

            if (!__instance.TryGetData(tile.unit.type, out UnitData tileUnit))
                return;

            var embarkActionType = EnumCache<ImprovementAbility.Type>.GetType("embarkmanual");
            if (!improvement.HasAbility(embarkActionType))
                return;

            bool isLandBound = tileUnit.IsLandBound();
            bool canWaterEmbark = playerState.HasAbility(EnumCache<PlayerAbility.Type>.GetType("waterembark"), gameState);

            if (__result)
            {
                if (!isLandBound || !canWaterEmbark)
                {
                    __result = false;
                }
            }
            else if (tile.improvement != null && __instance.TryGetData(tile.improvement.type, out ImprovementData tileImprovement))
            {
                bool hasBridge = tileImprovement.HasAbility(ImprovementAbility.Type.Bridge);
                bool isFlooded = tile.HasEffect(TileData.EffectType.Flooded);

                if (isLandBound && canWaterEmbark && (hasBridge || isFlooded))
                {
                    __result = true;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildAction), nameof(BuildAction.Execute))]
        private static void BuildAction_Execute(BuildAction __instance, GameState gameState)
        {
            if (gameState.GameLogicData.TryGetData(__instance.Type, out ImprovementData improvementData))
            {
                if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("embarkmanual")))
                {
                    gameState.ActionStack.Add(new EmbarkAction(__instance.PlayerId, __instance.Coordinates));
                }

                if(improvementData.creates != null && improvementData.creates.Count > 0)
                {
                    foreach (var item in improvementData.creates)
                    {
                        if(item.effect == EnumCache<TileData.EffectType>.GetType("blooming"))
                        {
                            Tile tile = MapRenderer.instance.GetTileInstance(__instance.Coordinates);
                            tile.Render();
                            break;
                        }
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CommandUtils), nameof(CommandUtils.GetUnitActions))]
        private static void CommandUtils_GetUnitActions(ref Il2CppSystem.Collections.Generic.List<CommandBase> __result, GameState gameState, PlayerState player, TileData tile, bool includeUnavailable)
        {
            UnitState unit = tile.unit;
            if (unit == null)
            {
                return;
            }
            if (unit.owner != player.Id)
            {
                return;
            }
            UnitData unitData;
            if (!gameState.GameLogicData.TryGetData(unit.type, out unitData))
            {
                return;
            }
            foreach (ImprovementData improvementData in gameState.GameLogicData.GetUnlockedImprovements(player))
            {
                if (improvementData.HasAbility(ImprovementAbility.Type.Manual)
                    && !unit.CanBuild()
                    && !unit.CanDisembark(gameState)
                    && gameState.GameLogicData.CanBuild(gameState, tile, player, improvementData)
                    && improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("embarkmanual")))
                {
                    var stack = gameState.CommandStack;
                    for (int i = stack.Count - 1; i >= 0; i--)
                    {
                        var command = stack[i];
                        var commandType = command.GetCommandType();

                        if (commandType == CommandType.EndTurn)
                        {
                            CommandUtils.AddCommand(gameState, __result, new BuildCommand(player.Id, improvementData.type, tile.coordinates), includeUnavailable);
                            return;
                        }

                        if (command.GetCommandType() == CommandType.Disembark)
                        {
                            DisembarkCommand disembarkCommand = command.Cast<DisembarkCommand>();
                            if (disembarkCommand.Coordinates == tile.coordinates)
                            {
                                return;
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.CanDisembark))]
        private static void UnitDataExtensions_CanDisembark(ref bool __result, UnitState unitState, GameState state)
        {
            if (!__result) return;

            var stack = state.CommandStack;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                var command = stack[i];
                var commandType = command.GetCommandType();

                if (commandType == CommandType.EndTurn)
                {
                    return;
                }

                if (commandType == CommandType.Build)
                {
                    var buildCommand = command.Cast<BuildCommand>();

                    if (buildCommand.Coordinates != unitState.coordinates) continue;

                    if (state.GameLogicData.TryGetData(buildCommand.Type, out var improvementData) &&
                        improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("embarkmanual")))
                    {
                        __result = false;
                        return;
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ClearTileEffectAction), nameof(ClearTileEffectAction.Execute))]
        private static void ClearTileEffectAction_Execute(ClearTileEffectAction __instance, GameState gameState)
        {
            TileData tile = gameState.Map.GetTile(__instance.Target);
            if(__instance.Effect == TileData.EffectType.Algae && tile != null && tile.HasEffect(EnumCache<TileData.EffectType>.GetType("blooming")))
            {
                tile.RemoveEffect(EnumCache<TileData.EffectType>.GetType("blooming"));
                if(tile.owner != 0)
                {
                    TileData city = gameState.Map.GetTile(tile.rulingCityCoordinates);
                    if(city.HasImprovement(ImprovementData.Type.City))
                    {
                        gameState.ActionStack.Add(new DecreasePopulationAction(__instance.PlayerId, city.coordinates, 200));
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Tile), nameof(Tile.Render))]
        private static void Tile_Render(Tile __instance)
        {
            if(__instance.data.HasEffect(EnumCache<TileData.EffectType>.GetType("blooming")))
            {
                if(__instance.algaeRenderer != null)
                {
                    __instance.algaeRenderer.color = bloomColor;

                    if(__instance.algaeRenderer.spriteRenderer != null)
                    {
                        __instance.algaeRenderer.spriteRenderer.color = bloomColor;
                    }
                }
            }
        }
    }
}
