using HarmonyLib;
using Polytopia.Data;

namespace PolyPlus
{
    public class Generation
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.SetTileAsCapital))]
        private static void MapGenerator_SetTileAsCapital(GameState gameState, PlayerState playerState, TileData tile)
        {
            TribeData tribeData;
            if (tile != null && gameState.GameLogicData.TryGetData(playerState.tribe, out tribeData))
            {
                if (tribeData.HasAbility(EnumCache<TribeAbility.Type>.GetType("citypark")))
                {
                    tile.improvement.production = 2;
                    tile.improvement.baseScore += 250;
                    tile.improvement.AddReward(CityReward.Park);
                }
            }
        }
    }
}