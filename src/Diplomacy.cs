using System.Globalization;
using HarmonyLib;
using PolyPlus.Utils;
using Polytopia.Data;

namespace PolyPlus
{
    public static class Diplomacy
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerDiplomacyExtensions), nameof(PlayerDiplomacyExtensions.GetIncomeFromEmbassy))]
        private static bool PlayerDiplomacyExtensions_GetIncomeFromEmbassy(ref int __result, PlayerState playerState, PlayerState otherPlayer, GameState gameState)
        {
            int multiplier = playerState.HasPeaceWith(otherPlayer.Id) ? Parser.diplomacyDataPlus.peaceMultiplier : 1;
            __result = 0;
            if (otherPlayer.HasActiveEmbassyWith(playerState, gameState))
            {
                __result += gameState.GameLogicData.DiplomacyData.embassyIncome * otherPlayer.GetEmbassyLevel(playerState) * multiplier;
            }
            return false;
        }

        public static List<byte> GetEstablishedEmbassiesByPlayer(PlayerState player, GameState gameState)
        {
            List<byte> list = new List<byte>(gameState.PlayerStates.Count);
            foreach (PlayerState playerState in gameState.PlayerStates)
            {
                if (player.HasEmbassyWith(playerState))
                {
                    list.Add(playerState.Id);
                }
            }
            return list;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiplomacyData), nameof(DiplomacyData.GetEmbassyCostForPlayer))]
        private static bool DiplomacyData_GetEmbassyCostForPlayer(ref int __result, byte playerId, GameState gameState)
        {
            if(!gameState.TryGetPlayer(playerId, out PlayerState playerState))
                return true;

            Console.Write("GetEmbassyCostForPlayer");
            int amountOfEmbassies = GetEstablishedEmbassiesByPlayer(playerState, gameState).Count;
            Console.Write(amountOfEmbassies);
            __result = gameState.GameLogicData.DiplomacyData.embassyCost + (amountOfEmbassies * Parser.diplomacyDataPlus.embassyCostModifierPerEmbassy);
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetAttackOptionsAtPosition))]
        private static void GetAttackOptionsAtPosition(
            ref Il2CppSystem.Collections.Generic.List<WorldCoordinates> __result,
            GameState gameState, byte playerId, WorldCoordinates position, int range,
            bool includeHiddenTiles, UnitState customUnitState, bool ignoreDiplomacyRelation
        )
        {
            TileData unitTile = gameState.Map.GetTile(position);
            if(unitTile == null)
                return;

            UnitState unitState = customUnitState ?? unitTile.unit;

            if(unitState == null)
                return;

            Il2CppSystem.Collections.Generic.List<TileData> area = gameState.Map.GetArea(position, range, true, false);

            if (unitState.HasAbility(EnumCache<UnitAbility.Type>.GetType("revolt"), gameState)
                && gameState.TryGetPlayer(playerId, out PlayerState playerState))
            {
                Il2CppSystem.Collections.Generic.List<WorldCoordinates> list = new Il2CppSystem.Collections.Generic.List<WorldCoordinates>();
                if (area != null && area.Count > 0 && (unitState.HasAbility(UnitAbility.Type.Hide) == unitState.HasEffect(UnitEffect.Invisible)))
                {
                    for (int i = 0; i < area.Count; i++)
                    {
                        TileData tileData = area[i];
                        if (tileData != null)
                        {
                            bool isInPeace;
                            if (ignoreDiplomacyRelation != false)
                            {
                                isInPeace = false;
                            }
                            else
                            {
                                isInPeace = PlayerDiplomacyExtensions.HasPeaceWith(playerState, tileData.owner);
                                if (!isInPeace)
                                {
                                    isInPeace = PlayerDiplomacyExtensions.HasBrokenPeaceWith(playerState, tileData.owner);
                                }
                            }

                            if (tileData.HasImprovement(ImprovementData.Type.City) && tileData.owner != 0
                                && tileData.owner != unitState.owner && !isInPeace)
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
        [HarmonyPatch(typeof(AttackAction), nameof(AttackAction.Execute))]
        private static bool AttackAction_Execute(AttackAction __instance, GameState state)
        {
            TileData unitTile = state.Map.GetTile(__instance.Origin);
            TileData cityTile = state.Map.GetTile(__instance.Target);

            if(unitTile == null || cityTile == null
                || unitTile.unit == null || !unitTile.unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("revolt"))
                || !Parser.rebellionUnits.ContainsKey(unitTile.unit.type))
            {
                return true;
            }
            if(!cityTile.HasImprovement(ImprovementData.Type.City) || !state.TryGetPlayer(unitTile.unit.owner, out PlayerState playerState))
            {
                return true;
            }

            List<TileData> list = new List<TileData>();
            foreach (TileData tileData in state.Map.GetArea(cityTile.coordinates, (int)cityTile.improvement.borderSize, true, false))
            {
                if (tileData.rulingCityCoordinates == cityTile.coordinates && tileData.unit == null && tileData.CanBeAccessedByPlayer(state, playerState))
                {
                    list.Add(tileData);
                }
            }
            list = GetRandomTiles(list, Math.Min(Math.Min((int)cityTile.improvement.level, Parser.diplomacyDataPlus.maxRebelCount), list.Count), state.Seed,
                (int)state.CurrentTurn, cityTile.coordinates);
            Console.Write(0);
            if(EnumCache<UnitData.Type>.TryGetType(Parser.rebellionUnits[unitTile.unit.type], out UnitData.Type rebelType)
                && state.GameLogicData.TryGetData(rebelType, out UnitData rebelData))
            {
                Console.Write(1);
                bool hasEmbarkOverride = Parser.embarkUnits.ContainsKey(rebelType);
                Console.Write(2);
                foreach (var item in list)
                {
                    Console.Write(3);
                    state.ActionStack.Add(new TrainAction(playerState.Id, rebelType, item.coordinates, 0, cityTile.coordinates));
                    Console.Write(4);
                    if(hasEmbarkOverride && item.IsWater && rebelData.unitAbilities.Contains(UnitAbility.Type.Land))
                    {
                        Console.Write(5);
                        state.ActionStack.Add(new EmbarkAction(playerState.Id, item.coordinates));
                        Console.Write(6);
                    }
                    Console.Write(7);
                }
                Console.Write(8);
                VisualizeRebellion(cityTile, playerState);
                Console.Write(9);
            }
            return true;
        }

        public static void VisualizeRebellion(TileData tileData, PlayerState playerState)
        {
            Tile tile = MapRenderer.Current.GetTileInstance(tileData.coordinates);
            if (tile.IsHidden)
            {
                return;
            }
            AudioManager.PlaySFX(SFXTypes.Explode, playerState.skinType, 1f, 1f, 0f);
            tile.SpawnExplosion();
            tile.SpawnDarkPuff();
            tile.SpawnEmbers(1f);
            tile.Improvement.UpdateObject();
            if (tile.Unit != null)
            {
                tile.Unit.Sway();
                tile.Unit.UpdateObject();
            }
            TextInfo myTI = new CultureInfo("en-US", false).TextInfo;
            string rebellionDescription = string.Empty;
            if (GameManager.LocalPlayer.Id == playerState.Id)
            {
                rebellionDescription = Localization.Get(
                    "world.rebellion.attackerdescription",
                    new Il2CppSystem.Object[] { tileData.improvement.name }
                );
            }
            else if (GameManager.LocalPlayer.Id == tileData.owner)
            {
                rebellionDescription = Localization.Get(
                    "world.rebellion.description",
                    new Il2CppSystem.Object[]
                    {
                        myTI.ToTitleCase(playerState.GetLocalizedTribeName(GameManager.GameState)),
                        tileData.improvement.name,
                    }
                );
            }
            if (rebellionDescription != string.Empty)
            {
                NotificationManager.Notify(rebellionDescription, Localization.Get(
                    "world.rebellion.title",
                    new Il2CppSystem.Object[] { tileData.improvement.name }
                ), null, GameManager.LocalPlayer);
            }
        }
        public static List<TileData> GetRandomTiles(List<TileData> tiles, int count, int seed, int turnCount, WorldCoordinates coordinates)
        {
            int combinedSeed = SafeHash(seed, turnCount, coordinates);
            Random rng = new Random(combinedSeed);
            var shuffled = new List<TileData>(tiles);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            return shuffled.Take(count).ToList();
        }

        private static int SafeHash(int seed, int turnCount, WorldCoordinates coordinates)
        {
            long hash = 17;
            hash = (hash * 31 + seed) % int.MaxValue;
            hash = (hash * 31 + turnCount) % int.MaxValue;
            hash = (hash * 31 + coordinates.x) % int.MaxValue;
            hash = (hash * 31 + coordinates.y) % int.MaxValue;

            return (int)Math.Abs(hash);
        }
    }
}