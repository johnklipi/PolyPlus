using HarmonyLib;
using Polytopia.Data;

namespace PolyPlus
{
    public class Movement
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.IsTileAccessible))] // PROPERLY TEST THE CHANGE AS IT MAY BREAK WHOLE MOVEMENT.
        private static void PathFinder_IsTileAccessible(ref bool __result, TileData tile, TileData origin, PathFinderSettings settings) // if it works correctly its gonna be perfect optimisation!
        {
            if(settings.unit != null)
            {
                if(settings.unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("revolt")))
                {
                    if(tile.HasImprovement(ImprovementData.Type.City))
                    {
                        __result = false;
                        return;
                    }
                }
                if (PlayerExtensions.HasAbility(settings.playerState, EnumCache<PlayerAbility.Type>.GetType("waterembark"), settings.gameState)
                    && settings.allowedTerrain.Contains(tile.terrain) && tile.GetExplored(settings.playerState.Id))
                {
                    if(tile.IsWater && !origin.IsWater)
                    {
                        __result = !origin.IsWater || origin.HasImprovement(ImprovementData.Type.Bridge);
                        return;
                    }
                    if(origin.IsWater && !tile.IsWater && settings.unit.HasAbility(UnitAbility.Type.Land))
                    {
                        __result = false;
                        return;
                    }
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
                if (hasNoBridge && !unitData.IsAquatic() && !unitState.HasAbility(UnitAbility.Type.Fly, gameState) && tile2.IsWater
                    && PlayerExtensions.HasAbility(playerState, EnumCache<PlayerAbility.Type>.GetType("waterembark"),gameState))
                {
                    gameState.ActionStack.Add(new EmbarkAction(__instance.PlayerId, worldCoordinates));
                }
            }
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
                if (unitState != null && playerState.HasAbility(EnumCache<PlayerAbility.Type>.GetType("dashembark"), gameState))
                {
                    unitState.moved = false;
                    unitState.attacked = false;
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

        // I removed logic which was checking whether unit has attacked before being pushed on water tile. Too complicated.
        // I will fix other stuff and then come up with better solution.
    }
}