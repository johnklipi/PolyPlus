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
    }
}