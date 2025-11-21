namespace PolyPlus.Utils
{
    public static class CommandsUtils
    {
        public enum CheckType
        {
            None,
            Turn,
            Ever
        }

        public static CommandBase? GetCommandOnCoordinate(Il2CppSystem.Collections.Generic.List<CommandBase> commandStack, CommandType type, WorldCoordinates coordinates, CheckType checkType)
        {
            if(checkType != CheckType.None)
            {
                for (int i = commandStack.Count - 1; i >= 0; i--)
                {
                    var command = commandStack[i];
                    var commandType = command.GetCommandType();

                    if (commandType == CommandType.EndTurn && checkType == CheckType.Turn)
                    {
                        return null;
                    }

                    if (commandType == CommandType.StartMatch && checkType == CheckType.Ever)
                    {
                        return null;
                    }

                    if (commandType != type)
                    {
                        continue;
                    }

                    if (GetCoordinates(command) == coordinates)
                    {
                        return command;
                    }
                }
            }
            return null;
        }

        public static WorldCoordinates GetCoordinates(CommandBase command)
        {
            switch (command.GetCommandType())
            {
                case CommandType.Build:
                    return command.Cast<BuildCommand>().Coordinates;
                case CommandType.Attack:
                    return command.Cast<AttackCommand>().Origin;
                case CommandType.Train:
                    return command.Cast<TrainCommand>().Coordinates;
                case CommandType.Move:
                    return command.Cast<MoveCommand>().From;
                case CommandType.Disembark:
                    return command.Cast<DisembarkCommand>().Coordinates;
            }
            return WorldCoordinates.NULL_COORDINATES;
        }
    }
}