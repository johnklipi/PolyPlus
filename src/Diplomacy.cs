using System.Globalization;
using HarmonyLib;
using Polytopia.Data;
using PolytopiaBackendBase.Common;

namespace PolyPlus
{
    public static class Diplomacy
    {
        private static bool unrobCity = false;
        private static bool denyCloakAttackIncome = false;

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
            if (unrobCity)
            {
                if (__result && effect == ImprovementEffect.robbed)
                    __result = false;
                unrobCity = false;
            }
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
            if (denyCloakAttackIncome)
            {
                denyCloakAttackIncome = false;
                __result = 0;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerDiplomacyExtensions), nameof(PlayerDiplomacyExtensions.GetIncomeFromEmbassy))]
        private static void PlayerDiplomacyExtensions_GetIncomeFromEmbassy(ref int __result, PlayerState playerState, PlayerState otherPlayer, GameState gameState)
        {
            if (playerState.HasPeaceWith(otherPlayer.Id))
                __result /= 2;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetAttackOptionsAtPosition))]
        private static void GetAttackOptionsAtPosition(
            ref Il2CppSystem.Collections.Generic.List<WorldCoordinates> __result,
            GameState gameState, byte playerId, WorldCoordinates position, int range,
            bool includeHiddenTiles, UnitState customUnitState, bool ignoreDiplomacyRelation
        )
        {
            Il2CppSystem.Collections.Generic.List<WorldCoordinates> list =
                new Il2CppSystem.Collections.Generic.List<WorldCoordinates>();
            UnitState unitState = customUnitState ?? gameState.Map.GetTile(position).unit;
            Il2CppSystem.Collections.Generic.List<TileData> area = gameState.Map.GetArea(
                position,
                range,
                true,
                false
            );
            if (
                unitState != null
                && unitState.HasAbility(UnitAbility.Type.Infiltrate, gameState)
                && gameState.TryGetPlayer(playerId, out PlayerState playerState)
            )
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
                            if (ignoreDiplomacyRelation != false)
                            {
                                isInPeace = false;
                            }
                            else
                            {
                                isInPeace = PlayerDiplomacyExtensions.HasPeaceWith(
                                    playerState,
                                    tileData.owner
                                );
                                if (!isInPeace)
                                {
                                    isInPeace = PlayerDiplomacyExtensions.HasBrokenPeaceWith(
                                        playerState,
                                        tileData.owner
                                    );
                                }
                            }

                            if (
                                tileData.HasImprovement(ImprovementData.Type.City)
                                && tileData.owner != 0
                                && tileData.owner != unitState.owner
                                && !isInPeace
                            )
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
        [HarmonyPatch(typeof(InfiltrationRewardReaction), nameof(InfiltrationRewardReaction.Execute))]
        private static bool InfiltrationRewardReaction_Execute(InfiltrationRewardReaction __instance, Il2CppSystem.Action onComplete)
        {
            TileData tileData = GameManager.GameState.Map.GetTile(__instance.action.Coordinates);
            Tile tile = MapRenderer.Current.GetTileInstance(__instance.action.Coordinates);
            if (tile.IsHidden)
            {
                onComplete?.Invoke();
                return false;
            }
            AudioManager.PlaySFX(SFXTypes.Explode, SkinType.Default, 1f, 1f, 0f);
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
            GameManager.GameState.TryGetPlayer(
                __instance.action.PlayerId,
                out PlayerState playerState
            );
            string rebellionDescription = string.Empty;
            if (GameManager.LocalPlayer.Id == __instance.action.PlayerId)
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
                        myTI.ToTitleCase(
                            EnumCache<UnitData.Type>.GetName(__instance.action.UnitType)
                        ),
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
            onComplete?.Invoke();
            return false;
        }
    }
}