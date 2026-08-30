using System;
using System.Collections.Generic;

namespace HairSalon
{
    public enum HaircutServiceState { Active, Completed, Failed }
    public enum HaircutServiceRating { None, Perfect, Recovered, Failed }

    [Serializable]
    public sealed class HaircutServiceModel
    {
        private readonly List<SalonTool> _requiredTools;

        public IReadOnlyList<SalonTool> RequiredTools => _requiredTools;
        public int CurrentStepIndex { get; private set; }
        public bool HadUndercut { get; private set; }
        public HaircutServiceState State { get; private set; } = HaircutServiceState.Active;
        public HaircutServiceRating Rating { get; private set; } = HaircutServiceRating.None;
        public SalonTool CurrentRequiredTool =>
            _requiredTools[Math.Min(CurrentStepIndex, _requiredTools.Count - 1)];

        public HaircutServiceModel(params SalonTool[] requiredTools)
        {
            if (requiredTools == null || requiredTools.Length < 1 || requiredTools.Length > 2)
                throw new ArgumentException("A haircut order requires one or two steps.", nameof(requiredTools));

            _requiredTools = new List<SalonTool>(requiredTools.Length);
            foreach (SalonTool tool in requiredTools)
            {
                if (!HaircutConfig.IsHaircutTool(tool))
                    throw new ArgumentException("Haircut orders only accept haircut tools.", nameof(requiredTools));
                _requiredTools.Add(tool);
            }
        }

        public HaircutResult BeginAttempt(SalonTool selectedTool)
        {
            if (State != HaircutServiceState.Active) return HaircutResult.None;
            if (selectedTool == CurrentRequiredTool) return HaircutResult.None;
            ApplyAttempt(selectedTool, HaircutResult.WrongTool);
            return HaircutResult.WrongTool;
        }

        public bool ApplyAttempt(SalonTool selectedTool, HaircutResult result)
        {
            if (State != HaircutServiceState.Active || result == HaircutResult.None) return false;

            if (selectedTool != CurrentRequiredTool || result == HaircutResult.WrongTool)
            {
                Fail();
                return true;
            }

            if (result == HaircutResult.Overcut)
            {
                Fail();
                return true;
            }

            if (result == HaircutResult.Undercut)
            {
                HadUndercut = true;
                return true;
            }

            if (result != HaircutResult.Perfect) return false;
            CurrentStepIndex++;
            if (CurrentStepIndex < _requiredTools.Count) return true;

            State = HaircutServiceState.Completed;
            Rating = HadUndercut ? HaircutServiceRating.Recovered : HaircutServiceRating.Perfect;
            return true;
        }

        public bool IsStepComplete(int index)
        {
            return index >= 0 && index < _requiredTools.Count && index < CurrentStepIndex;
        }

        private void Fail()
        {
            State = HaircutServiceState.Failed;
            Rating = HaircutServiceRating.Failed;
        }
    }
}
