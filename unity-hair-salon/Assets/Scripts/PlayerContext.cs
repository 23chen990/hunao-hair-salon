using System;

namespace HairSalon
{
    /// <summary>
    /// Per-player interaction state. This is intentionally separate from shared customer and
    /// workstation state so another local or remote player can keep an independent focus.
    /// </summary>
    [Serializable]
    public sealed class PlayerContext
    {
        public int PlayerId { get; }
        public int SelectedCustomerId { get; private set; } = -1;
        public int FocusedStationId { get; private set; } = -1;
        public SalonTool? SelectedTool { get; set; }
        public ActiveServiceAction ActiveAction { get; set; } = ActiveServiceAction.None;
        public int SelectedToolOwnerCustomerId { get; private set; } = -1;
        public int SelectedToolStationId { get; private set; } = -1;
        public int SelectedServiceStep { get; private set; } = -1;
        public WashStage SelectedWashStage { get; private set; } = WashStage.Dry;
        public SalonViewState ViewState { get; private set; } = SalonViewState.Overview;

        public PlayerContext(int playerId)
        {
            if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            PlayerId = playerId;
        }

        internal void FocusCustomer(int customerId, int stationId)
        {
            if (SelectedCustomerId != customerId || FocusedStationId != stationId)
                ClearInteractionContext();
            SelectedCustomerId = customerId;
            FocusedStationId = stationId;
            ViewState = SalonViewState.WorkstationFocus;
        }

        public void SelectServiceAction(
            CustomerModel customer, ActiveServiceAction action, SalonTool? tool)
        {
            if (customer == null)
            {
                ClearInteractionContext();
                return;
            }
            SelectedTool = tool;
            ActiveAction = action;
            SelectedToolOwnerCustomerId = customer.Id;
            SelectedToolStationId = customer.Station;
            SelectedServiceStep = customer.Step;
            SelectedWashStage = customer.WashStage;
        }

        public bool OwnsSelection(CustomerModel customer)
        {
            if (customer == null || (SelectedTool == null && ActiveAction == ActiveServiceAction.None))
                return false;
            return SelectedCustomerId == customer.Id &&
                   FocusedStationId == customer.Station &&
                   SelectedToolOwnerCustomerId == customer.Id &&
                   SelectedToolStationId == customer.Station &&
                   SelectedServiceStep == customer.Step &&
                   SelectedWashStage == customer.WashStage;
        }

        public void ClearInteractionContext()
        {
            SelectedTool = null;
            ActiveAction = ActiveServiceAction.None;
            SelectedToolOwnerCustomerId = -1;
            SelectedToolStationId = -1;
            SelectedServiceStep = -1;
            SelectedWashStage = WashStage.Dry;
        }

        internal void ClearFocus()
        {
            SelectedCustomerId = -1;
            FocusedStationId = -1;
            ClearInteractionContext();
            ViewState = SalonViewState.Overview;
        }
    }
}
