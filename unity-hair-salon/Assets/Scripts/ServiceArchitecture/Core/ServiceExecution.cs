using System;

namespace HairSalon.ServiceArchitecture
{
    /// <summary>
    /// A single uninterrupted service. Progress is elapsed time only; it has no
    /// internal step, stage, or customer-need semantics.
    /// </summary>
    public sealed class ServiceExecution
    {
        internal ServiceExecution(
            string executionId, int customerId, int stationId,
            ServiceExecutionType type, float duration, ActionToken token)
        {
            ExecutionId = executionId;
            CustomerId = customerId;
            StationId = stationId;
            Type = type;
            Duration = duration;
            ActionToken = token;
            State = ServiceExecutionState.Started;
        }

        public string ExecutionId { get; }
        public int CustomerId { get; }
        public int StationId { get; }
        public ServiceExecutionType Type { get; }
        public float Duration { get; }
        public float Elapsed { get; private set; }
        public float Progress => Duration <= 0f ? 1f : Math.Min(1f, Elapsed / Duration);
        public ServiceExecutionState State { get; private set; }
        public ActionToken ActionToken { get; }
        public ActionResult Result { get; internal set; }

        internal void BeginExecuting() => State = ServiceExecutionState.Executing;

        public static float DurationFor(ServiceExecutionType type)
        {
            switch (type)
            {
                case ServiceExecutionType.Wash: return 5f;
                case ServiceExecutionType.Haircut: return 4f;
                case ServiceExecutionType.BlowDry: return 3f;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public void Tick(float deltaTime)
        {
            if (State != ServiceExecutionState.Executing) return;
            Elapsed = Math.Min(Duration, Elapsed + Math.Max(0f, deltaTime));
        }

        internal void MarkCompleted(ActionResult result)
        {
            Result = result;
            State = ServiceExecutionState.Completed;
            Elapsed = Duration;
        }
    }

    public sealed class ServiceExecutionCompletion
    {
        internal ServiceExecutionCompletion(ApplyActionResult applyResult, ActionResult actionResult)
        {
            ApplyResult = applyResult;
            ActionResult = actionResult;
        }

        public ApplyActionResult ApplyResult { get; }
        public ActionResult ActionResult { get; }
    }

    /// <summary>
    /// Owns only the time boundary. Business state is submitted through the
    /// existing Resolver -> Result -> Applier chain.
    /// </summary>
    public sealed class ServiceExecutionController
    {
        private readonly ActionResolver _resolver;
        private readonly ActionResultApplier _applier;
        private readonly CustomerServiceState _state;
        private readonly InteractionContext _interaction;
        private ServiceExecution _active;
        private int _sequence;

        public ServiceExecutionController(
            ActionResolver resolver, ActionResultApplier applier,
            CustomerServiceState state, InteractionContext interaction)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _applier = applier ?? throw new ArgumentNullException(nameof(applier));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
        }

        public ServiceExecution Active => _active;

        public ServiceExecution Start(ServiceExecutionType type, float duration, float worldTime)
        {
            if (_active != null && _active.State == ServiceExecutionState.Executing)
                throw new InvalidOperationException("A service is already executing.");
            if (duration < 0f || float.IsNaN(duration) || float.IsInfinity(duration))
                throw new ArgumentOutOfRangeException(nameof(duration));

            ServiceTool tool = ToolFor(type);
            ActionToken token = _interaction.BeginAction(
                ServiceActionType.ServiceCompletion, tool,
                _state.Physical.PhysicalStateRevision, worldTime);
            _active = new ServiceExecution(
                "service:" + (++_sequence), _state.CustomerId, _state.StationId,
                type, duration, token);
            _active.BeginExecuting();
            return _active;
        }

        public ServiceExecution Start(ServiceExecutionType type, float worldTime)
        {
            return Start(type, ServiceExecution.DurationFor(type), worldTime);
        }

        public ServiceExecutionCompletion Complete()
        {
            if (_active == null) throw new InvalidOperationException("No service is executing.");
            if (_active.State == ServiceExecutionState.Completed)
                return new ServiceExecutionCompletion(
                    _applier.Apply(_state, _interaction, _active.Result), _active.Result);
            if (_active.Progress < 1f)
                return new ServiceExecutionCompletion(
                    new ApplyActionResult(ApplyStatus.InvalidResult, DiagnosticCode.InvalidResult), null);

            var request = new ActionRequest(
                _active.ActionToken, _active.CustomerId, _active.StationId,
                ServiceActionType.ServiceCompletion, ToolFor(_active.Type),
                _active.Duration, false);
            ActionResult result = _resolver.Resolve(
                request, _state.Order, _state.Physical.CreateSnapshot(),
                _state.Progress.CreateSnapshot(), _interaction.CreateSnapshot(),
                _state.Metrics.CreateSnapshot());
            ApplyActionResult applied = _applier.Apply(_state, _interaction, result);
            if (applied.Status == ApplyStatus.Applied)
                _active.MarkCompleted(result);
            return new ServiceExecutionCompletion(applied, result);
        }

        private static ServiceTool ToolFor(ServiceExecutionType type)
        {
            switch (type)
            {
                case ServiceExecutionType.Wash: return ServiceTool.Shower;
                case ServiceExecutionType.Haircut: return ServiceTool.Scissors;
                case ServiceExecutionType.BlowDry: return ServiceTool.BlowDryer;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
