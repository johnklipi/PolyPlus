using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using Polytopia.Data;

namespace PolyPlus
{
    public class Movement
    {
        private static MoveAction.MoveReason? lastEmbarkReason = null;
        private static bool hasAttackedPrePush = false;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.IsTileAccessible))]
        private static void PathFinder_IsTileAccessible(ref bool __result, TileData tile, TileData origin, PathFinderSettings settings)
        {
            if (
                PlayerExtensions.HasAbility(settings.playerState, EnumCache<PlayerAbility.Type>.GetType("waterembark"), settings.gameState)
                && tile.IsWater && !origin.IsWater && settings.unit != null
            )
            {
                if ((tile.terrain == Polytopia.Data.TerrainData.Type.Water && settings.allowedTerrain.Contains(Polytopia.Data.TerrainData.Type.Water))
                    || (tile.terrain == Polytopia.Data.TerrainData.Type.Ocean && settings.allowedTerrain.Contains(Polytopia.Data.TerrainData.Type.Ocean)))
                    __result = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.GetMoveOptions))]
        private static void PathFinder_GetMoveOptions(
            ref Il2CppSystem.Collections.Generic.List<WorldCoordinates> __result, GameState gameState, WorldCoordinates start, int maxCost, UnitState unit
        )
        {
            PlayerState playerState;
            TileData startTile = GameManager.GameState.Map.GetTile(start);
            Il2CppSystem.Collections.Generic.List<WorldCoordinates> options = __result;
            List<WorldCoordinates> toRemove = new List<WorldCoordinates>();
            if (gameState.TryGetPlayer(unit.owner, out playerState))
            {
                if (
                    PlayerExtensions.HasAbility(
                        playerState,
                        EnumCache<PlayerAbility.Type>.GetType("waterembark"),
                        gameState
                    ) && !unit.HasAbility(UnitAbility.Type.Fly, gameState)
                )
                {
                    foreach (WorldCoordinates destination in options)
                    {
                        if (!startTile.IsWater)
                        {
                            Il2CppSystem.Collections.Generic.List<WorldCoordinates> path =
                                PathFinder.GetPath(gameState, start, destination, maxCost, unit);
                            path.Reverse();
                            bool hadWater = false;
                            foreach (WorldCoordinates pathTile in path)
                            {
                                TileData tile = gameState.Map.GetTile(pathTile);
                                if (hadWater || !tile.GetExplored(playerState.Id))
                                    toRemove.Add(tile.coordinates);
                                if (
                                    tile.terrain == Polytopia.Data.TerrainData.Type.Water
                                    || tile.terrain == Polytopia.Data.TerrainData.Type.Ocean
                                )
                                {
                                    if (tile.improvement != null)
                                    {
                                        if (gameState.GameLogicData.TryGetData(tile.improvement.type, out ImprovementData imrovementData))
                                        {
                                            if (!imrovementData.HasAbility(ImprovementAbility.Type.Bridge))
                                                hadWater = true;
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
            if (gameState.TryGetUnit(__instance.UnitId, out unitState)
                && gameState.TryGetPlayer(__instance.PlayerId, out playerState)
                && gameState.GameLogicData.TryGetData(unitState.type, out unitData))
            {
                WorldCoordinates worldCoordinates = __instance.Path[0];
                TileData tile2 = gameState.Map.GetTile(worldCoordinates);
                tile2.SetUnit(unitState);
                unitState.coordinates = worldCoordinates;
                bool hasNoBridge = true;
                if (tile2.improvement != null)
                {
                    if (gameState.GameLogicData.TryGetData(tile2.improvement.type, out ImprovementData imrovementData))
                    {
                        if (imrovementData.HasAbility(ImprovementAbility.Type.Bridge))
                            hasNoBridge = false;
                    }
                }
                if (
                    hasNoBridge
                    && !unitData.IsAquatic()
                    && !unitState.HasAbility(UnitAbility.Type.Fly, gameState)
                    && tile2.IsWater
                    && PlayerExtensions.HasAbility(
                        playerState,
                        EnumCache<PlayerAbility.Type>.GetType("waterembark"),
                        gameState
                    )
                )
                {
                    lastEmbarkReason = __instance.Reason;
                    gameState.ActionStack.Add(new EmbarkAction(__instance.PlayerId, worldCoordinates));
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EmbarkAction), nameof(EmbarkAction.Execute))]
        private static bool EmbarkAction_ExecuteDefault_Prefix(EmbarkAction __instance, GameState gameState)
        {
            PlayerState playerState;
            if (gameState.TryGetPlayer(__instance.PlayerId, out playerState))
            {
                TileData tile = gameState.Map.GetTile(__instance.Coordinates);
                UnitState unitState = tile.unit;
                hasAttackedPrePush = unitState.attacked;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(EmbarkAction), nameof(EmbarkAction.Execute))]
        private static void EmbarkAction_ExecuteDefault_Postfix(EmbarkAction __instance, GameState gameState)
        {
            PlayerState playerState;
            if (gameState.TryGetPlayer(__instance.PlayerId, out playerState))
            {
                TileData tile = gameState.Map.GetTile(__instance.Coordinates);
                UnitState unitState = tile.unit;
                if (PlayerExtensions.HasAbility(playerState, EnumCache<PlayerAbility.Type>.GetType("dashembark"), gameState)
                    && lastEmbarkReason != MoveAction.MoveReason.Attack && lastEmbarkReason != null)
                {
                    if (!(lastEmbarkReason == MoveAction.MoveReason.Push && hasAttackedPrePush))
                    {
                        lastEmbarkReason = null;
                        hasAttackedPrePush = false;
                        unitState.moved = false;
                        unitState.attacked = false;
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TileData), nameof(TileData.GetMovementCost))]
        private static void TileData_GetMovementCost(ref int __result, TileData __instance, MapData map, TileData fromTile, PathFinderSettings settings)
        {
            UnitState unit = settings.unit;
            if (unit != null && __instance.terrain == Polytopia.Data.TerrainData.Type.Ice && settings.unitData.HasAbility(EnumCache<UnitAbility.Type>.GetType("slide")))
                __result = 5;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.CanExplode))]
        private static bool UnitDataExtensions_CanExplode(ref bool __result, UnitState unit, GameState gameState)
        {
            __result = unit.CanAttack() && unit.owner == gameState.CurrentPlayer && unit.HasAbility(UnitAbility.Type.Explode, gameState);
            return false;
        }
    }
}