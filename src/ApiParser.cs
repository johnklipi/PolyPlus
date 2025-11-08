
using HarmonyLib;
using Il2CppSystem.IO;
using Newtonsoft.Json.Linq;
using Polytopia.Data;
using PolyPlus.Data;
namespace PolyPlus
{
    public static class ApiParser
    {
        internal static Dictionary<ImprovementData.Type, List<TerrainRequirementsPlus>> improvementTerrainReq = new();
        internal static Dictionary<ImprovementData.Type, List<AdjacencyRequirementsPlus>> improvementAdjacencyReq = new();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
        private static void GameLogicData_AddGameLogicPlaceholders(GameLogicData __instance, JObject rootObject)
        {
            foreach (JToken jtoken in rootObject.SelectTokens("$.*.*").ToArray())
            {
                JObject? token = jtoken.TryCast<JObject>();
                if (token != null)
                {
                    string dataType = GetJTokenName(token, 2);
                    if(dataType == "improvementData")
                    {
                        JToken terrainRequirements = token["terrainRequirements"];
                        if(terrainRequirements != null && terrainRequirements.Type == JTokenType.Array)
                        {
                            JArray array = terrainRequirements.Cast<JArray>();

                            List<TerrainRequirementsPlus> list = new();
                            foreach (var item in array._values)
                            {
                                if (item["improvement"] != null)
                                {
                                    string improvementValue = item["improvement"].ToString();
                                    var requirement = new TerrainRequirementsPlus
                                    {
                                        improvement = EnumCache<ImprovementData.Type>.GetType(improvementValue)
                                    };
                                    list.Add(requirement);
                                }
                                if(item["effect"] != null)
                                {
                                    string effectValue = item["effect"].ToString();
                                    var requirement = new TerrainRequirementsPlus
                                    {
                                        effect = EnumCache<TileData.EffectType>.GetType(effectValue)
                                    };
                                    list.Add(requirement);
                                }
                            }
                            int idx = (int)token["idx"];
                            improvementTerrainReq[(ImprovementData.Type)idx] = list;
                        }

                        JToken adjacencyRequirements = token["adjacencyRequirements"];
                        if(adjacencyRequirements != null && adjacencyRequirements.Type == JTokenType.Array)
                        {
                            JArray array = adjacencyRequirements.Cast<JArray>();

                            List<AdjacencyRequirementsPlus> list = new();
                            foreach (var item in array._values)
                            {
                                if(item["effect"] != null)
                                {
                                    string effectValue = item["effect"].ToString();
                                    var requirement = new AdjacencyRequirementsPlus
                                    {
                                        effect = EnumCache<TileData.EffectType>.GetType(effectValue)
                                    };
                                    list.Add(requirement);
                                }
                            }
                            int idx = (int)token["idx"];
                            improvementAdjacencyReq[(ImprovementData.Type)idx] = list;
                        }
                    }
                }
            }
        }

        internal static string GetJTokenName(JToken token, int n = 1)
        {
            return token.Path.Split('.')[^n];
        }
    }
}