using System;

namespace HairSalon.ServiceArchitecture
{
    /// <summary>将异步动作绑定到顾客、工位、交互版本和物理版本。</summary>
    public sealed class ActionToken : IEquatable<ActionToken>
    {
        public ActionToken(
            string actionId,
            int customerId,
            int stationId,
            int contextVersion,
            int expectedPhysicalRevision,
            ServiceActionType actionType,
            ServiceTool tool,
            float startedAtWorldTime)
        {
            if (string.IsNullOrWhiteSpace(actionId)) throw new ArgumentException("ActionId is required.", nameof(actionId));
            ActionId = actionId;
            CustomerId = customerId;
            StationId = stationId;
            ContextVersion = contextVersion;
            ExpectedPhysicalRevision = expectedPhysicalRevision;
            ActionType = actionType;
            Tool = tool;
            StartedAtWorldTime = startedAtWorldTime;
        }

        public string ActionId { get; }
        public int CustomerId { get; }
        public int StationId { get; }
        public int ContextVersion { get; }
        public int ExpectedPhysicalRevision { get; }
        public ServiceActionType ActionType { get; }
        public ServiceTool Tool { get; }
        public float StartedAtWorldTime { get; }

        public bool Equals(ActionToken other)
        {
            return !ReferenceEquals(other, null) && ActionId == other.ActionId && CustomerId == other.CustomerId
                && StationId == other.StationId && ContextVersion == other.ContextVersion
                && ExpectedPhysicalRevision == other.ExpectedPhysicalRevision && ActionType == other.ActionType
                && Tool == other.Tool && StartedAtWorldTime == other.StartedAtWorldTime;
        }

        public override bool Equals(object obj) => Equals(obj as ActionToken);
        public override int GetHashCode() => ActionId.GetHashCode();
    }

    /// <summary>临时交互状态；不保存订单和物理状态。</summary>
    public sealed class InteractionContext
    {
        private int _nextActionSequence;

        public int FocusedCustomerId { get; private set; } = -1;
        public int FocusedStationId { get; private set; } = -1;
        public ServiceTool SelectedTool { get; private set; } = ServiceTool.None;
        public int SelectedToolCustomerId { get; private set; } = -1;
        public int SelectedToolStationId { get; private set; } = -1;
        public string ActiveActionId { get; private set; }
        public ActionToken ActiveActionToken { get; private set; }
        public int ContextVersion { get; private set; }
        public string CurrentHint { get; private set; }

        public void Focus(int customerId, int stationId)
        {
            if (FocusedCustomerId == customerId && FocusedStationId == stationId) return;
            ClearValues();
            FocusedCustomerId = customerId;
            FocusedStationId = stationId;
            ContextVersion++;
        }

        public void SelectTool(ServiceTool tool)
        {
            SelectedTool = tool;
            SelectedToolCustomerId = FocusedCustomerId;
            SelectedToolStationId = FocusedStationId;
        }

        public ActionToken BeginAction(
            ServiceActionType actionType,
            ServiceTool tool,
            int expectedPhysicalRevision,
            float startedAtWorldTime)
        {
            SelectTool(tool);
            string actionId = $"{FocusedCustomerId}:{ContextVersion}:{++_nextActionSequence}";
            var token = new ActionToken(actionId, FocusedCustomerId, FocusedStationId, ContextVersion,
                expectedPhysicalRevision, actionType, tool, startedAtWorldTime);
            ActiveActionId = actionId;
            ActiveActionToken = token;
            return token;
        }

        public void ExecuteQuickAction(ServiceActionType actionType, ServiceTool tool)
        {
            ClearInteractionContext(ClearReason.QuickAction);
        }

        public void ClearInteractionContext(ClearReason reason)
        {
            int customer = FocusedCustomerId;
            int station = FocusedStationId;
            ClearValues();
            if (reason != ClearReason.CustomerChanged && reason != ClearReason.FocusExited
                && reason != ClearReason.SceneUnloaded)
            {
                FocusedCustomerId = customer;
                FocusedStationId = station;
            }
            ContextVersion++;
        }

        public bool IsTokenCurrent(ActionToken token)
        {
            return token != null && ActiveActionToken != null && ActiveActionToken.Equals(token)
                && token.ContextVersion == ContextVersion && token.CustomerId == FocusedCustomerId
                && token.StationId == FocusedStationId;
        }

        public InteractionContextSnapshot CreateSnapshot()
        {
            return new InteractionContextSnapshot(
                FocusedCustomerId, FocusedStationId, SelectedTool, SelectedToolCustomerId,
                SelectedToolStationId, ActiveActionId, ActiveActionToken, ContextVersion, CurrentHint);
        }

        internal void CompleteAction(ActionToken token)
        {
            if (ActiveActionToken != null && ActiveActionToken.Equals(token))
            {
                ActiveActionId = null;
                ActiveActionToken = null;
            }
        }

        private void ClearValues()
        {
            FocusedCustomerId = -1;
            FocusedStationId = -1;
            SelectedTool = ServiceTool.None;
            SelectedToolCustomerId = -1;
            SelectedToolStationId = -1;
            ActiveActionId = null;
            ActiveActionToken = null;
            CurrentHint = null;
        }
    }

    /// <summary>Resolver读取的不可变交互快照。</summary>
    public sealed class InteractionContextSnapshot : IEquatable<InteractionContextSnapshot>
    {
        public InteractionContextSnapshot(
            int focusedCustomerId,
            int focusedStationId,
            ServiceTool selectedTool,
            int selectedToolCustomerId,
            int selectedToolStationId,
            string activeActionId,
            ActionToken activeActionToken,
            int contextVersion,
            string currentHint)
        {
            FocusedCustomerId = focusedCustomerId;
            FocusedStationId = focusedStationId;
            SelectedTool = selectedTool;
            SelectedToolCustomerId = selectedToolCustomerId;
            SelectedToolStationId = selectedToolStationId;
            ActiveActionId = activeActionId;
            ActiveActionToken = activeActionToken;
            ContextVersion = contextVersion;
            CurrentHint = currentHint;
        }

        public int FocusedCustomerId { get; }
        public int FocusedStationId { get; }
        public ServiceTool SelectedTool { get; }
        public int SelectedToolCustomerId { get; }
        public int SelectedToolStationId { get; }
        public string ActiveActionId { get; }
        public ActionToken ActiveActionToken { get; }
        public int ContextVersion { get; }
        public string CurrentHint { get; }

        public bool Equals(InteractionContextSnapshot other)
        {
            return !ReferenceEquals(other, null) && FocusedCustomerId == other.FocusedCustomerId
                && FocusedStationId == other.FocusedStationId && SelectedTool == other.SelectedTool
                && SelectedToolCustomerId == other.SelectedToolCustomerId
                && SelectedToolStationId == other.SelectedToolStationId && ActiveActionId == other.ActiveActionId
                && Equals(ActiveActionToken, other.ActiveActionToken) && ContextVersion == other.ContextVersion
                && CurrentHint == other.CurrentHint;
        }

        public override bool Equals(object obj) => Equals(obj as InteractionContextSnapshot);
        public override int GetHashCode() => ContextVersion * 397 ^ FocusedCustomerId;
    }
}
