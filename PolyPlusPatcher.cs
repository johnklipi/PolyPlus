using System.Globalization;
using HarmonyLib;
using Polytopia.Data;
using Steamworks.Data;
using UnityEngine;

namespace PolyPlus
{
    public static class PolyPlusPatcher
    {
        private static bool unlockRoutes = false;
        private static bool unrobCity = false;
        private static bool denyCloakAttackIncome = false;
        private static MoveAction.MoveReason? lastEmbarkReason = null;
        private static bool hasAttackedPrePush = false;

        public static void Load()
        {
            Harmony.CreateAndPatchAll(typeof(PolyPlusPatcher));
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
            Vector2 anchoredPosition = __instance.iconContainer.anchoredPosition;
            string improvementName = string.Empty;
            if (
                __instance.Unit != null
                && GameManager.GameState != null
                && GameManager.GameState.Map != null
            )
            {
                TileData tile = GameManager.GameState.Map.GetTile(__instance.Unit.UnitState.home);
                if (tile != null && tile.HasImprovement(ImprovementData.Type.City))
                {
                    improvementName = tile.improvement.name;
                }
            }
            string unitDescription = string.IsNullOrEmpty(improvementName)
                ? string.Empty
                : string.Format(
                    "{0}\n",
                    Localization.Get(
                        "world.unit.info.from",
                        new Il2CppSystem.Object[] { improvementName }
                    )
                );
            string unitProgressText;
            int killCount = (int)(__instance.Unit ? __instance.Unit!.UnitState.xp : 0);
            if (
                UIManager.Instance.CurrentScreen != UIConstants.Screens.TechTree
                && __instance.unit != null
                && __instance.unit.unitData.HasAbility(
                    EnumCache<UnitAbility.Type>.GetType("staticplus")
                )
            )
            {
                unitProgressText = Localization.Get(
                    "polyplus.unit.veteran.static.progress",
                    new Il2CppSystem.Object[] { killCount.ToString() }
                );
                __instance.Description = string.Format("{0}{1}", unitDescription, unitProgressText);
                anchoredPosition.x = 48f;
            }
        }

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
                                if (hadWater)
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerDiplomacyExtensions), nameof(PlayerDiplomacyExtensions.GetIncomeFromEmbassy))]
        private static void PlayerDiplomacyExtensions_GetIncomeFromEmbassy(ref int __result, PlayerState playerState, PlayerState otherPlayer, GameState gameState)
        {
            if (playerState.HasPeaceWith(otherPlayer.Id))
                __result /= 2;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TileData), nameof(TileData.GetMovementCost))]
        private static void TileData_GetMovementCost(ref int __result, TileData __instance, MapData map, TileData fromTile, PathFinderSettings settings)
        {
            UnitState unit = settings.unit;
            if (unit != null && __result == 5 && settings.unitData.HasAbility(UnitAbility.Type.Skate))
                __result = 10;
            if (unit != null && __instance.terrain == Polytopia.Data.TerrainData.Type.Ice && settings.unitData.HasAbility(EnumCache<UnitAbility.Type>.GetType("slide")))
                __result = 5;
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

            if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("embarkmanual")))
            {
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
                if (!gameState.GameLogicData.CanBuild(gameState, tile, player, improvementData))
                    continue;
                if (improvementData.HasAbility(ImprovementAbility.Type.Manual) && !unit.CanBuild() && !unit.CanDisembark(gameState)
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
                else if (improvementData.HasAbility(ImprovementAbility.Type.Flood) && tile.unit.moved && !tile.unit.attacked)
                {
                    CommandUtils.AddCommand(gameState, __result, new BuildCommand(player.Id, improvementData.type, tile.coordinates), includeUnavailable);
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
        [HarmonyPatch(typeof(StartMatchAction), nameof(StartMatchAction.ExecuteDefault))]
        private static void StartMatchAction_ExecuteDefault(GameState gameState)
        {
            if (gameState.PlayerStates != null && gameState.PlayerStates.Count > 0)
            {
                foreach (var playerState in gameState.PlayerStates)
                {
                    if (playerState.tribe == TribeData.Type.Aquarion && playerState.startTile != WorldCoordinates.NULL_COORDINATES)
                    {
                        TileData startingTile = gameState.Map.GetTile(playerState.startTile);
                        startingTile.Flood(playerState);
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CaptureCityAction), nameof(CaptureCityAction.ExecuteDefault))]
        private static void CaptureCityAction_ExecuteDefault(CaptureCityAction __instance, GameState gameState)
        {
            if (!gameState.TryGetPlayer(__instance.PlayerId, out PlayerState playerState))
                return;
            if (playerState.tribe == TribeData.Type.Aquarion && playerState.startTile != WorldCoordinates.NULL_COORDINATES)
            {
                TileData tile = gameState.Map.GetTile(__instance.Coordinates);
                tile.Flood(playerState);
            }
        }

        // [HarmonyPrefix]
        // [HarmonyPatch(typeof(StartMatchAction), nameof(StartMatchAction.ExecuteDefault))]
        // private static bool StartMatchAction_ExecuteDefault(StartMatchAction __instance) // Will be used for changing player currency amount based on the tribe, disabled for now
        // {
        //     if (GameManager.Client.clientType == ClientBase.ClientType.Local || GameManager.Client.clientType == ClientBase.ClientType.PassAndPlay)
        //     {
        //         foreach (PlayerState playerState in GameManager.GameState.PlayerStates)
        //         {
        //             if (playerState.tribe == TribeData.Type.Luxidoor)
        //                 playerState.Currency = 10;
        //         }
        //     }
        //     return true;
        // }
    }
}
