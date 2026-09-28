namespace HairSalon
{
    /// <summary>
    /// Public player-scoped gateway kept as extension methods so the legacy
    /// one-argument reflection contract on SalonGameModel remains unambiguous.
    /// </summary>
    public static class SalonGameModelCoopExtensions
    {
        public static bool BeginWashFoamHold(this SalonGameModel game, int playerId,
            CustomerModel customer)
        {
            return game != null && game.BeginWashFoamHoldForPlayer(playerId, customer);
        }

        public static bool FinishWashRinse(this SalonGameModel game, int playerId,
            CustomerModel customer)
        {
            return game != null && game.FinishWashRinseForPlayer(playerId, customer);
        }

        public static bool BeginHaircutAction(this SalonGameModel game, int playerId,
            CustomerModel customer, SalonTool selectedTool, HaircutConfig config)
        {
            return game != null && game.BeginHaircutActionForPlayer(
                playerId, customer, selectedTool, config);
        }

        public static HaircutResult CompleteHaircutAction(this SalonGameModel game, int playerId,
            CustomerModel customer, float elapsedTime, bool interrupted)
        {
            return game == null ? HaircutResult.None : game.CompleteHaircutActionForPlayer(
                playerId, customer, elapsedTime, interrupted);
        }

        public static bool EndActiveOperation(this SalonGameModel game, int playerId,
            CustomerModel customer)
        {
            return game != null && game.EndActiveOperationForPlayer(playerId, customer);
        }
    }
}
