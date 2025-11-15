using HarmonyLib;
using Polytopia.Data;

namespace PolyPlus
{
    public static class Routes
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapDataExtensions), nameof(MapDataExtensions.UpdateRoutes))]
        private static bool MapDataExtensions_UpdateRoutes(GameState gameState, Il2CppSystem.Collections.Generic.List<TileData> changedTiles)
        {
            UpdateRoutesV2(gameState, changedTiles);
            return false;
        }

        public static void UpdateRoutesV2(GameState gameState, Il2CppSystem.Collections.Generic.List<TileData> changedTiles)
        {
            if (gameState == null || gameState.Map == null)
                return;

            var map = gameState.Map;

            Il2CppSystem.Collections.Generic.List<TileData> list = new Il2CppSystem.Collections.Generic.List<TileData>();
            map.ResetRoutes(list);

            var empireTiles = new Il2CppSystem.Collections.Generic.List<TileData>();
            var playerRouteOpeners = new Il2CppSystem.Collections.Generic.List<TileData>();
            var routeOpeners = new Il2CppSystem.Collections.Generic.List<TileData>();

            foreach (PlayerState player in gameState.PlayerStates)
            {
                if (player == null || player.Id == 255)
                    continue;

                map.GetPlayerEmpireTiles(player.Id, empireTiles);
                playerRouteOpeners.Clear();
                routeOpeners.Clear();
                
                foreach (TileData tile in empireTiles)
                {
                    if (tile.improvement != null && gameState.GameLogicData.TryGetData(tile.improvement.type, out ImprovementData improvementData))
                    {
                        if (improvementData.IsRouteOpener())
                        {
                            playerRouteOpeners.Add(tile);
                        }
                        if (improvementData.type == ImprovementData.Type.City)
                        {
                            routeOpeners.Add(tile);
                        }
                    }
                }
                for (int i = 0; i < playerRouteOpeners.Count; i++)
                    {
                        TileData sourceTile = playerRouteOpeners[i];
                        if (gameState.GameLogicData.TryGetData(sourceTile.improvement.type, out ImprovementData improvementData2))
                        {
                            for (int j = i + 1; j < playerRouteOpeners.Count; j++)
                            {
                                TileData targetTile = playerRouteOpeners[j];
                                FindPathBetweenRouters(sourceTile, targetTile, player, gameState, changedTiles);
                            }
                            if (improvementData2.HasAbility(ImprovementAbility.Type.Network))
                            {
                                ushort num = 0;
                                for (int k = 0; k < routeOpeners.Count; k++)
                                {
                                    TileData tile = routeOpeners[k];
                                    if (FindPathBetweenRouters(sourceTile, tile, player, gameState, changedTiles))
                                    {
                                        num += 1;
                                    }
                                }
                            }
                        }
                    }
            }

            foreach (var tile in list)
            {
                if (!tile.hasRoute && !changedTiles.Contains(tile))
                {
                    changedTiles.Add(tile);
                }

                tile.hadRoute = false;
            }
        }

        public static bool FindPathBetweenRouters(
            TileData origin,
            TileData destination,
            PlayerState player,
            GameState gameState,
            Il2CppSystem.Collections.Generic.List<TileData> changedTiles
        )
        {
            if (origin == null || destination == null || gameState == null)
                return false;

            var logicData = gameState.GameLogicData;
            if (logicData == null || origin.improvement == null || destination.improvement == null)
                return false;

            if (!logicData.TryGetData(origin.improvement.type, out var originImp) ||
                !logicData.TryGetData(destination.improvement.type, out var destImp))
                return false;

            int distance = MapDataExtensions.ChebyshevDistance(origin.coordinates, destination.coordinates);

            if (originImp.range < distance)
                return false;

            var usableTerrain = new Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData>();
            if (destImp.type != ImprovementData.Type.City)
            {
                foreach (var terrain in originImp.routes)
                {
                    if (destImp.routes.Contains(terrain))
                        usableTerrain.Add(terrain);
                }

                if (usableTerrain.Count == 0)
                    return false;
            }
            else
            {
                usableTerrain = originImp.routes;
            }

            var settings = PathFinderSettings.CreateRouterSettings(player, usableTerrain, gameState.Version, gameState);

            Il2CppSystem.Collections.Generic.List<WorldCoordinates> path = PathFinder.GetPath(
                gameState.Map,
                origin.coordinates,
                destination.coordinates,
                originImp.range,
                settings
            );

            if (path == null || path.Count < 1)
                return false;

            foreach (var coord in path)
            {
                int idx = MapDataExtensions.GetTileIndex(gameState.Map, coord);
                if (idx < 0)
                    continue;

                var tile = gameState.Map.Tiles[idx];
                if (tile == null)
                    continue;

                if (!tile.hasRoute)
                {
                    tile.hasRoute  = true;
                    if (!changedTiles.Contains(tile))
                        changedTiles.Add(tile);
                }
            }
            return true;
        }
    }
}