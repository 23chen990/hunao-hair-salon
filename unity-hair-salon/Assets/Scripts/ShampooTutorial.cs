using System;
using UnityEngine;

namespace HairSalon
{
    public enum ShampooTutorialStep
    {
        Inactive,
        SelectShowerForWet,
        HoldToWet,
        SelectShampoo,
        HoldToShampoo,
        SelectShowerForRinse,
        HoldToRinse,
        SelectTowel,
        MoveToNextStation,
        Completed
    }

    public interface IShampooTutorialProgressStore
    {
        bool IsCompleted { get; }
        void MarkCompleted();
    }

    public sealed class PlayerPrefsShampooTutorialProgressStore : IShampooTutorialProgressStore
    {
        public const string Key = "HairSalon.ShampooTutorialCompleted.v1";
        public bool IsCompleted => PlayerPrefs.GetInt(Key, 0) == 1;

        public void MarkCompleted()
        {
            PlayerPrefs.SetInt(Key, 1);
            PlayerPrefs.Save();
        }
    }

    public sealed class ShampooTutorialController
    {
        private readonly IShampooTutorialProgressStore _store;
        private int _customerId = -1;

        public ShampooTutorialStep Step { get; private set; }
        public int CustomerId => _customerId;
        public bool IsActive => Step != ShampooTutorialStep.Inactive && Step != ShampooTutorialStep.Completed;
        public bool IsCompleted => _store.IsCompleted || Step == ShampooTutorialStep.Completed;

        public ShampooTutorialController(IShampooTutorialProgressStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Step = store.IsCompleted ? ShampooTutorialStep.Completed : ShampooTutorialStep.Inactive;
        }

        public bool TryBegin(CustomerModel customer)
        {
            if (IsCompleted || IsActive || customer == null || customer.IsComplete ||
                customer.CurrentNeed != ServiceType.Wash || customer.State != CustomerState.Serving)
                return false;
            _customerId = customer.Id;
            Step = ShampooTutorialStep.SelectShowerForWet;
            return true;
        }

        public void NotifyToolSelected(CustomerModel customer, ActiveServiceAction action)
        {
            if (!Matches(customer)) return;
            if (Step == ShampooTutorialStep.SelectShowerForWet && action == ActiveServiceAction.Shower)
                Step = ShampooTutorialStep.HoldToWet;
            else if (Step == ShampooTutorialStep.SelectShampoo && action == ActiveServiceAction.Shampoo)
                Step = ShampooTutorialStep.HoldToShampoo;
            else if (Step == ShampooTutorialStep.SelectShowerForRinse && action == ActiveServiceAction.Shower)
                Step = ShampooTutorialStep.HoldToRinse;
        }

        public void NotifyCustomerStageChanged(CustomerModel customer)
        {
            if (!Matches(customer)) return;
            if (customer.WashStage == WashStage.Wet)
                Step = ShampooTutorialStep.SelectShampoo;
            else if (customer.WashStage == WashStage.Foamy)
                Step = ShampooTutorialStep.SelectShowerForRinse;
            else if (customer.WashStage == WashStage.Rinsed)
                Step = ShampooTutorialStep.SelectTowel;
            else if (customer.WashStage == WashStage.Toweled)
                Step = ShampooTutorialStep.MoveToNextStation;
        }

        public void NotifyMovedToNextStation(CustomerModel customer)
        {
            if (!Matches(customer) || Step != ShampooTutorialStep.MoveToNextStation || customer.Station < 0 ||
                customer.CurrentNeed == ServiceType.Wash ||
                !SalonGameModel.IsCompatibleStation(customer.CurrentNeed, customer.Station)) return;
            Step = ShampooTutorialStep.Completed;
            _store.MarkCompleted();
        }

        public bool IsToolExpected(ActiveServiceAction action)
        {
            if (Step == ShampooTutorialStep.SelectShowerForWet || Step == ShampooTutorialStep.HoldToWet ||
                Step == ShampooTutorialStep.SelectShowerForRinse || Step == ShampooTutorialStep.HoldToRinse)
                return action == ActiveServiceAction.Shower;
            if (Step == ShampooTutorialStep.SelectShampoo || Step == ShampooTutorialStep.HoldToShampoo)
                return action == ActiveServiceAction.Shampoo;
            return Step == ShampooTutorialStep.SelectTowel && action == ActiveServiceAction.WrapTowel;
        }

        public string Prompt
        {
            get
            {
                switch (Step)
                {
                    case ShampooTutorialStep.SelectShowerForWet: return "先把头发打湿";
                    case ShampooTutorialStep.HoldToWet: return "长按顾客头部";
                    case ShampooTutorialStep.SelectShampoo: return "使用洗发水";
                    case ShampooTutorialStep.HoldToShampoo: return "长按揉洗";
                    case ShampooTutorialStep.SelectShowerForRinse:
                    case ShampooTutorialStep.HoldToRinse: return "冲掉泡沫";
                    case ShampooTutorialStep.SelectTowel: return "包上毛巾";
                    case ShampooTutorialStep.MoveToNextStation: return "带顾客去下一个工位";
                    default: return string.Empty;
                }
            }
        }

        private bool Matches(CustomerModel customer) => customer != null && customer.Id == _customerId && IsActive;
    }
}
