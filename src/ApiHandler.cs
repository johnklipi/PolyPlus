using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using Polytopia.Data;
using PolyPlus.Data;

namespace PolyPlus
{
    public static class ApiHandler
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.MeetsRequirement))]
        public static bool MeetsRequirement(ref bool __result, GameLogicData __instance, TileData tile, ImprovementData improvement, PlayerState playerState, GameState gameState)
		{
            bool hasResourceRequirement = false;
			bool meetsTerrainRequirement = false;

            bool hasTerrainRequirement = false;
			bool meetsResourceRequirement = false;

            bool hasImprovementRequirement = false;
			bool meetsImprovementRequirement = false;

            bool hasEffectRequirement = false;
			bool meetsEffectRequirement = false;

            List<TerrainRequirementsPlus> terrainRequirementsPlus= new();
            if(ApiParser.improvementTerrainReq.ContainsKey(improvement.type))
            {
                terrainRequirementsPlus = ApiParser.improvementTerrainReq[improvement.type];
            }
			if (improvement.terrainRequirements != null && improvement.terrainRequirements.Count > 0)
			{
				foreach (TerrainRequirements terrainRequirements in improvement.terrainRequirements)
				{
					if (terrainRequirements.resource != null && terrainRequirements.resource.type != ResourceData.Type.None)
					{
						hasResourceRequirement = true;
						if (tile.resource != null && tile.resource.type == terrainRequirements.resource.type)
						{
							meetsResourceRequirement = true;
						}
					}
					if (terrainRequirements.terrain != null && terrainRequirements.terrain.type != TerrainData.Type.None)
					{
						hasTerrainRequirement = true;
						if (tile.terrain == terrainRequirements.terrain.type)
						{
							meetsTerrainRequirement = true;
						}
						else
						{
							if (tile.IsWater && terrainRequirements.terrain.type == TerrainData.Type.Field && improvement.type != ImprovementData.Type.Road && playerState.HasAbility(PlayerAbility.Type.Pontoon, gameState))
							{
								meetsTerrainRequirement = true;
							}
							if (tile.terrain == TerrainData.Type.Forest && terrainRequirements.terrain.type == TerrainData.Type.Field && playerState.HasAbility(PlayerAbility.Type.Treehouse, gameState))
							{
								meetsTerrainRequirement = true;
							}
						}
					}
				}
			}
            if(terrainRequirementsPlus.Count > 0)
            {
                foreach (TerrainRequirementsPlus terrainRequirements in terrainRequirementsPlus)
                {
                    if (terrainRequirements.improvement != ImprovementData.Type.None)
					{
						hasImprovementRequirement = true;
						if (tile.improvement != null && tile.improvement.type == terrainRequirements.improvement)
						{
							meetsImprovementRequirement = true;
						}
					}
                    if (terrainRequirements.effect != TileData.EffectType.None)
					{
						hasEffectRequirement = true;
						if (tile.HasEffect(terrainRequirements.effect))
						{
							meetsEffectRequirement = true;
						}
					}
                }
            }
			bool meetsReq = (!hasTerrainRequirement || meetsTerrainRequirement) && (!hasResourceRequirement || meetsResourceRequirement)
                && (!hasImprovementRequirement || meetsImprovementRequirement) && (!hasEffectRequirement || meetsEffectRequirement);

			bool meetsAdjReq = true;

			if(ApiParser.improvementAdjacencyReq.ContainsKey(improvement.type))
			{
				List<AdjacencyRequirementsPlus> adjacencyRequirementsPlus = ApiParser.improvementAdjacencyReq[improvement.type];
				if(adjacencyRequirementsPlus.Count > 0)
                {
					meetsAdjReq = false;
					foreach (TileData tileData in gameState.Map.GetTileNeighbors(tile.coordinates))
					{
						foreach (AdjacencyRequirementsPlus adjacencyRequirements in adjacencyRequirementsPlus)
						{
							if (adjacencyRequirements.effect != TileData.EffectType.None && tileData.HasEffect(adjacencyRequirements.effect))
							{
								meetsAdjReq = true;
								break;
							}
						}
						if(meetsAdjReq)
                        {
                            break;
                        }
					}
                }

			}

			__result = meetsReq && meetsAdjReq;
            return false;
		}

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionUtils), nameof(ActionUtils.CheckSurroundingArea))]
        public static void CheckSurroundingArea(GameState gameState, byte playerId, TileData tile)
        {
			List<TileData> area = gameState.Map.GetArea(tile.coordinates, 1, true, true).ToArray().ToList();
			if (area == null || area.Count == 0)
			{
				return;
			}
			for (int i = 0; i < area.Count; i++)
			{
				TileData tileData = area[i];
				if (tileData != null && tileData.improvement != null)
				{
					ImprovementData.Type type = tileData.improvement.type;
					if(ApiParser.improvementAdjacencyImp.ContainsKey(type) && ApiParser.improvementAdjacencyImp[type].Count > 0)
                    {
                        ActionUtils.UpdateImprovementLevel(gameState, playerId, tileData);
                    }
				}
			}
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionUtils), nameof(ActionUtils.CalculateImprovementLevel))]
        private static void ActionUtils_CalculateImprovementLevel(ref int __result, GameState gameState, TileData tile)
        {
            if (tile.improvement == null)
                return;
            if (!gameState.GameLogicData.TryGetData(tile.improvement.type, out ImprovementData improvementData))
                return;

			ImprovementData.Type type = tile.improvement.type;
            if (ApiParser.improvementAdjacencyImp.ContainsKey(type) && ApiParser.improvementAdjacencyImp[type].Count > 0)
            {
                __result = GetAdjacencyBonusAtPlus(gameState, tile, improvementData);
            }
        }

		public static int GetAdjacencyBonusAtPlus(GameState gameState, TileData tile, ImprovementData improvementData)
		{
			int calculatedLevel = 0;
			List<TileData> area = gameState.Map.GetArea(tile.coordinates, 1, true, false).ToArray().ToList();
			if (area == null || area.Count == 0)
			{
				return calculatedLevel;
			}

			for (int i = 0; i < area.Count; i++)
			{
				TileData tileData = area[i];
				if (tileData != null && tileData.owner == tile.owner)
				{
					foreach (AdjacencyImprovementsPlus adjacencyImprovements in ApiParser.improvementAdjacencyImp[improvementData.type])
					{
						if (adjacencyImprovements.effect != TileData.EffectType.None && tileData.HasEffect(adjacencyImprovements.effect))
						{
							calculatedLevel += 1;
						}
						if (adjacencyImprovements.terrain != TerrainData.Type.None && tileData.terrain == adjacencyImprovements.terrain)
						{
							calculatedLevel += 1;
						}
					}
				}
			}
			return calculatedLevel;
		}

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TileDataExtensions), nameof(TileDataExtensions.CalculateRawProduction))]
		public static void CalculateRawProduction(ref int __result, TileData tile, GameState gameState)
		{
			if (tile.improvement != null && gameState.GameLogicData.TryGetData(tile.improvement.type, out ImprovementData improvementData))
			{
				if(improvementData.work > 0 && ApiParser.improvementAdjacencyImp.ContainsKey(improvementData.type)
					&& ApiParser.improvementAdjacencyImp[improvementData.type].Count > 0)
                {
                    __result = Math.Max(improvementData.work * tile.improvement.level, 0);
                }
			}
		}

    }
}