using System;

namespace HairSalon
{
    public enum HaircutInteractionState { Idle, ToolSelected, Holding, Result }
    public enum HaircutResult { None, Undercut, Perfect, Overcut, WrongTool, Resisted }
    public enum HairStage { Original, Trimmed, Complete, Overcut }

    [Serializable]
    public sealed class HaircutConfig
    {
        public float PerfectMin = 1.5f;
        public float PerfectMax = 2.5f;
        public float ScissorCycleSeconds = .52f;
        public float ThinningPerfectMin = 1.45f;
        public float ThinningPerfectMax = 2.1f;
        public float ThinningCycleSeconds = .4f;
        public float ClippersPerfectMin = .7f;
        public float ClippersPerfectMax = 1.45f;
        public float ClippersCycleSeconds = .2f;
        public float UndercutPenalty = 10f;
        public float OvercutPenalty = 40f;
        public float WrongToolPenalty = 24f;
        public float ParticleRate = 8f;
        public float ParticleLife = 2.8f;
        public float ResultFeedbackSeconds = 1.35f;
        public float CustomerReactionSeconds = 2.2f;
        public int MaxParticles = 28;
        public bool RetryAllowed = true;

        public float SafePerfectMin => Math.Max(0f, PerfectMin);
        public float SafePerfectMax => Math.Max(SafePerfectMin, PerfectMax);

        public static bool IsHaircutTool(SalonTool tool)
        {
            return tool == SalonTool.Scissors ||
                   tool == SalonTool.ThinningShears ||
                   tool == SalonTool.Clippers;
        }

        public float GetPerfectMin(SalonTool tool)
        {
            if (tool == SalonTool.ThinningShears) return Math.Max(0f, ThinningPerfectMin);
            if (tool == SalonTool.Clippers) return Math.Max(0f, ClippersPerfectMin);
            return SafePerfectMin;
        }

        public float GetPerfectMax(SalonTool tool)
        {
            float min = GetPerfectMin(tool);
            float configured = tool == SalonTool.ThinningShears ? ThinningPerfectMax :
                               tool == SalonTool.Clippers ? ClippersPerfectMax : PerfectMax;
            return Math.Max(min, configured);
        }

        public float GetCycleSeconds(SalonTool tool)
        {
            float configured = tool == SalonTool.ThinningShears ? ThinningCycleSeconds :
                               tool == SalonTool.Clippers ? ClippersCycleSeconds : ScissorCycleSeconds;
            return Math.Max(.08f, configured);
        }
    }

    public sealed class HaircutInteraction
    {
        private readonly HaircutConfig _config;
        private SalonTool? _selectedTool;

        public HaircutInteractionState State { get; private set; } = HaircutInteractionState.Idle;
        public HaircutResult LastResult { get; private set; } = HaircutResult.None;
        public float HoldTime { get; private set; }
        public float HoldProgress
        {
            get
            {
                float completionThreshold = _selectedTool.HasValue
                    ? _config.GetPerfectMin(_selectedTool.Value)
                    : _config.SafePerfectMin;
                return completionThreshold <= 0f
                    ? 1f
                    : Math.Max(0f, Math.Min(1f, HoldTime / completionThreshold));
            }
        }
        public SalonTool? SelectedTool => _selectedTool;
        public event Action<HaircutResult> ResultResolved;

        public HaircutInteraction(HaircutConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void SelectTool(SalonTool tool)
        {
            if (State == HaircutInteractionState.Holding) CancelHold();
            _selectedTool = tool;
            HoldTime = 0f;
            LastResult = HaircutResult.None;
            State = HaircutInteractionState.ToolSelected;
        }

        public bool BeginHold()
        {
            bool retryingUndercut = State == HaircutInteractionState.Result &&
                                    LastResult == HaircutResult.Undercut &&
                                    _config.RetryAllowed;
            if (!_selectedTool.HasValue || !HaircutConfig.IsHaircutTool(_selectedTool.Value) ||
                (State != HaircutInteractionState.ToolSelected && !retryingUndercut)) return false;
            HoldTime = 0f;
            LastResult = HaircutResult.None;
            State = HaircutInteractionState.Holding;
            return true;
        }

        public HaircutResult Tick(float deltaTime)
        {
            if (State != HaircutInteractionState.Holding) return HaircutResult.None;
            HoldTime += Math.Max(0f, deltaTime);
            if (HoldTime > _config.GetPerfectMax(_selectedTool.Value)) return Resolve(HaircutResult.Overcut);
            return HaircutResult.None;
        }

        public HaircutResult ReleaseHold()
        {
            if (State != HaircutInteractionState.Holding) return HaircutResult.None;
            return Resolve(HoldTime < _config.GetPerfectMin(_selectedTool.Value) ? HaircutResult.Undercut : HaircutResult.Perfect);
        }

        public void CancelHold()
        {
            if (State != HaircutInteractionState.Holding) return;
            HoldTime = 0f;
            LastResult = HaircutResult.None;
            State = _selectedTool.HasValue ? HaircutInteractionState.ToolSelected : HaircutInteractionState.Idle;
        }

        public void PrepareRetry()
        {
            if (State != HaircutInteractionState.Result ||
                LastResult != HaircutResult.Undercut ||
                !_config.RetryAllowed) return;
            HoldTime = 0f;
            LastResult = HaircutResult.None;
            State = HaircutInteractionState.ToolSelected;
        }

        public void Reset()
        {
            _selectedTool = null;
            HoldTime = 0f;
            LastResult = HaircutResult.None;
            State = HaircutInteractionState.Idle;
        }

        private HaircutResult Resolve(HaircutResult result)
        {
            if (State != HaircutInteractionState.Holding) return HaircutResult.None;
            LastResult = result;
            State = HaircutInteractionState.Result;
            ResultResolved?.Invoke(result);
            return result;
        }
    }
}
