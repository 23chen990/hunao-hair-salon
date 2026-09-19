using System;
using System.Collections.Generic;
using HairSalon.ServiceArchitecture;

namespace HairSalon
{
    public enum ServiceType { Wash, Dry, Cut, Dye, Perm }
    public enum SalonTool { Scissors, ThinningShears, Clippers, BlowDryer, Shampoo, DyeBottle, PermSolution }
    public enum WorkstationType { Wash, Haircut, Perm }
    public enum WorkstationState
    {
        Available,
        Reserved,
        CustomerEnRoute,
        AwaitingService,
        AwaitingTransfer,
        InService,
        Rework,
        Completed
    }
    public enum SalonViewState { Overview, WorkstationFocus }
    public enum CustomerState { Entering, Waiting, MovingToStation, Serving, Finished, Leaving, Exited }
    public enum CustomerEmotion { Calm, Impatient, Angry, Happy }
    public enum CustomerReactionKind { None, Confused, Protest, Resistance, Impatient, Angry }
    public enum CustomerWrongServiceKind { None, Wash, Dry, Haircut }
    public enum CustomerServiceFeedback { None, Satisfied, Dissatisfied }
    public enum CustomerServiceResult
    {
        None,
        NormalCompletion,
        HappyCompletion,
        UnhappyCompletion,
        SevereUnhappyCompletion,
        Failed
    }
    public enum CustomerAttentionState { Unserved, ServiceEngaged, ActiveOperation, Neglected }
    public enum BackgroundTaskState { Inactive, Running, Complete }
    public enum BackgroundRiskState { None, Early, Ideal, SlightlyOver, Dangerous, Disaster }

    [Serializable]
    public sealed class CustomerPatienceConfig
    {
        public float InitialPatience = 100f;
        public float DrainPerSecond = 1.2f;
        public float ImpatientAtPatience = 55f;
        public float AngryAtPatience = 25f;
        public float ServiceStartRelief = 12f;
        public float ServiceArrivalGraceSeconds = 1.5f;
    }

    [Serializable]
    public sealed class SalonFlowConfig
    {
        public float MinSpawnInterval = 1.5f;
        public float MaxSpawnInterval = 2.6f;
        public int MaxCustomers = 5;
        public int WaitingCapacity = 4;
        public int InitialCustomerCount = 0;
        public float BusinessDuration = 180f;
    }

    [Serializable]
    public sealed class ServicePointModel
    {
        public float X;
        public float Y;
        public float Z;
    }

    [Serializable]
    public sealed class BackgroundTaskModel
    {
        public float StartTime;
        public float Elapsed;
        public float IdealStart;
        public float IdealEnd;
        public float DangerAt;
        public BackgroundTaskState State = BackgroundTaskState.Inactive;
        public BackgroundRiskState RiskState = BackgroundRiskState.None;

        public void Start(float idealStart, float idealEnd, float dangerAt, float worldTime = 0f)
        {
            StartTime = worldTime;
            Elapsed = 0f;
            IdealStart = Math.Max(0f, idealStart);
            IdealEnd = Math.Max(IdealStart, idealEnd);
            DangerAt = Math.Max(IdealEnd, dangerAt);
            State = BackgroundTaskState.Running;
            RiskState = IdealStart <= 0f ? BackgroundRiskState.Ideal : BackgroundRiskState.Early;
        }

        public void Tick(float dt)
        {
            if (State != BackgroundTaskState.Running) return;
            Elapsed += Math.Max(0f, dt);
            if (Elapsed < IdealStart) RiskState = BackgroundRiskState.Early;
            else if (Elapsed <= IdealEnd) RiskState = BackgroundRiskState.Ideal;
            else if (Elapsed < DangerAt) RiskState = BackgroundRiskState.SlightlyOver;
            else if (Elapsed < DangerAt * 1.35f) RiskState = BackgroundRiskState.Dangerous;
            else RiskState = BackgroundRiskState.Disaster;
        }
    }

    [Serializable]
    public sealed class WorkstationModel
    {
        public int Id;
        public WorkstationType Type;
        public int CurrentCustomerId = -1;
        public WorkstationState State = WorkstationState.Available;
        public ServicePointModel ServicePoint = new ServicePointModel();
        public readonly List<SalonTool> AvailableTools = new List<SalonTool>();
        public bool Occupied => CurrentCustomerId >= 0;
        public int StationId { get => Id; set => Id = value; }
        public WorkstationType StationType { get => Type; set => Type = value; }
        public int OccupiedCustomerId { get => CurrentCustomerId; set => CurrentCustomerId = value; }
        public ServiceType? CurrentService;
        public bool IsOccupied => Occupied;
        public ServicePointModel InteractionPoint { get => ServicePoint; set => ServicePoint = value; }

        public static WorkstationModel Haircut(int id)
        {
            var station = new WorkstationModel { Id = id, Type = WorkstationType.Haircut };
            station.AvailableTools.Add(SalonTool.Scissors);
            station.AvailableTools.Add(SalonTool.ThinningShears);
            station.AvailableTools.Add(SalonTool.Clippers);
            station.AvailableTools.Add(SalonTool.BlowDryer);
            station.AvailableTools.Add(SalonTool.DyeBottle);
            return station;
        }
    }

    [Serializable]
    public sealed class CustomerModel
    {
        public int Id;
        public List<ServiceType> Needs = new List<ServiceType>();
        public int Step;
        public float Patience = 100f;
        public float MaxPatience = 100f;
        public float Satisfaction = 70f;
        public CustomerState State = CustomerState.Waiting;
        public CustomerEmotion Emotion = CustomerEmotion.Calm;
        public int Station = -1;
        public BackgroundTaskModel BackgroundTask = new BackgroundTaskModel();
        public HairStage HairStage = HairStage.Original;
        public HaircutServiceModel HaircutService;
        public HaircutServiceRating LastHaircutRating = HaircutServiceRating.None;
        public float ReactionRemaining;
        public CustomerReactionKind ReactionKind = CustomerReactionKind.None;
        public float StateElapsed;
        public CustomerServiceFeedback ServiceFeedback = CustomerServiceFeedback.None;
        public CustomerServiceResult ServiceResult = CustomerServiceResult.None;
        public float TotalWaitSeconds;
        public float ServiceElapsed;
        public bool WasImpatientBeforeService;
        public CustomerAttentionState AttentionState = CustomerAttentionState.Unserved;
        public bool HasServiceEngaged;
        public WashStage WashStage = WashStage.Dry;
        public bool HairWet;
        public bool ShampooApplied;
        public int CompletedWashCount;
        public int ExtraServiceCount;
        public BlowStage BlowStage = BlowStage.AwaitingStart;
        public BlowResult LastBlowResult = BlowResult.None;
        public ServiceStepPhase ServicePhase = ServiceStepPhase.Ready;
        public ServiceExecution ServiceExecution;
        public ServiceExecutionCompletion ServiceExecutionCompletion;
        public ServiceProcessStage ProcessStage = ServiceProcessStage.None;
        public float ProcessingElapsed;
        public float ProcessingDuration;
        public float DyeCleanupGraceDurationSnapshot;
        public ActiveServiceAction ActiveServiceAction = ActiveServiceAction.None;
        public float ActiveServiceElapsed;
        public float ActiveServiceDuration;
        public bool TowelWrapped;
        public float ServiceDelaySeconds;
        public bool HadServiceDelay;
        public float ServiceArrivalGraceRemaining;
        public float WaitingSatisfactionLoss;
        public int WrongStationCount;
        public CustomerWrongServiceKind WrongServiceKind;
        public AccidentSeverity AccidentSeverity = AccidentSeverity.None;
        public float RecoveryCap = 100f;
        public float ManualBlowElapsed;
        public bool ManualBlowHolding;
        public bool AutoBlowRunning;
        public bool AutoBlowSafetyStopped;
        public bool OrderRequirementsCompleted;
        public bool ExitReady;
        public ExitBlockReason ExitBlockReason = ExitBlockReason.RequirementsIncomplete;
        public readonly List<FunnyDisasterEvent> ActiveDisasterEvents = new List<FunnyDisasterEvent>();
        public readonly List<RecoveryRequirement> RecoveryRequirements = new List<RecoveryRequirement>();
        /// <summary>阶段3洗头领域状态的只读显示投影；正式写入只发生在领域提交器中。</summary>
        public CustomerPhysicalStateSnapshot ServicePhysicalState { get; internal set; }
        public int WorkstationId { get => Station; set => Station = value; }
        public int CurrentStepIndex => Step;
        public int CompletedStepCount => Math.Min(Step, Needs.Count);
        public bool IsComplete => Step >= Needs.Count;
        public bool HasBlockingPhysicalState => TowelWrapped || ShampooApplied ||
            WashStage == WashStage.Wetting || WashStage == WashStage.ShampooApplied ||
            WashStage == WashStage.Shampooing || WashStage == WashStage.Foamy ||
            WashStage == WashStage.Rinsing || WashStage == WashStage.Rinsed;
        public ServiceType CurrentNeed => IsComplete ? ServiceType.Cut : Needs[Step];
        public ServiceType? NextNeed => Step + 1 < Needs.Count ? Needs[Step + 1] : (ServiceType?)null;
        public bool HasUnresolvedFoamBurstEvent
        {
            get
            {
                for (int i = 0; i < ActiveDisasterEvents.Count; i++)
                {
                    FunnyDisasterEvent evt = ActiveDisasterEvents[i];
                    if (evt != null && evt.EventType == FunnyDisasterType.FoamBurst && !evt.IsResolved)
                        return true;
                }
                return false;
            }
        }

        public bool HasOutstandingReWashRequirement
        {
            get
            {
                for (int i = 0; i < RecoveryRequirements.Count; i++)
                {
                    RecoveryRequirement req = RecoveryRequirements[i];
                    if (req != null && req.RecoveryType == RecoveryType.ReWash && !req.IsCompleted)
                        return true;
                }
                return false;
            }
        }
        public WorkstationType? NextRequiredStation =>
            IsComplete ? (WorkstationType?)null : SalonGameModel.RequiredStationFor(CurrentNeed);
        public float PatienceProgress => Math.Max(0f, Math.Min(1f, Patience / Math.Max(.01f, MaxPatience)));
        public bool IsProcessing => ProcessStage == ServiceProcessStage.DyeProcessing ||
            ProcessStage == ServiceProcessStage.DyeReadyForCleanup ||
            ProcessStage == ServiceProcessStage.PermProcessing;
        public bool IsDyeCleanupReady => ProcessStage == ServiceProcessStage.DyeReadyForCleanup;
        public bool HasDyeFailed => ProcessStage == ServiceProcessStage.DyeFailed;
        public float ProcessingProgress => ProcessingDuration <= 0f ? (IsProcessing ? 1f : 0f) :
            Math.Max(0f, Math.Min(1f, ProcessingElapsed / ProcessingDuration));
        public float DyeCleanupRiskProgress => !IsDyeCleanupReady ? 0f :
            Math.Max(0f, Math.Min(1f, (ProcessingElapsed - ProcessingDuration) /
                Math.Max(.01f, DyeCleanupGraceDurationSnapshot)));
    }

    public sealed class SalonGameModel
    {
        public const int LocalPlayerId = 1;
        public const bool ReputationSystemEnabled = false;
        public const int MaxCustomers = 5;
        public const int WaitingCapacity = 4;
        public const float EnteringSeconds = .8f;
        public const float MovingToStationSeconds = .7f;
        public const float FinishedFeedbackSeconds = 1.6f;
        public const float LeavingSeconds = 3.4f;
        private const float WrongServiceInterruptProgress = .3f;
        private struct AutoBlowTiming
        {
            public float Startup;
            public float GoodStart;
            public float GoodEnd;
            public float MinorEnd;
            public float SafetyStop;
        }

        public float RemainingTime { get; private set; } = 180f;
        public float WorldElapsed { get; private set; }
        public SalonPaymentModel Payments { get; }
        public CustomerPatienceConfig PatienceConfig { get; }
        public SalonFlowConfig FlowConfig { get; }
        public SalonServiceConfig ServiceConfig { get; }
        public CustomerExperienceProfile ExperienceProfile { get; }
        public EquipmentProductModel AutoBlowStandProduct { get; } = new EquipmentProductModel();
        /// <summary>基础自动吹发属于首日可玩的服务；购买支架只升级时间窗口。</summary>
        public bool AutoBlowAvailable => true;
        public bool HasAutoBlowStand => AutoBlowStandProduct.Purchased;
        public bool FirstDayCompleteForShop { get; private set; }
        public int Balance => Payments.Balance;
        public int Served { get; private set; }
        public int AngryLeaves { get; private set; }
        public int Mistakes { get; private set; }
        public SalonViewState ViewState => GetOrCreatePlayerContext(LocalPlayerId).ViewState;
        public CustomerModel SelectedCustomer => FindCustomer(
            GetOrCreatePlayerContext(LocalPlayerId).SelectedCustomerId);
        public bool IsRunning { get; private set; } = true;
        public readonly List<CustomerModel> Customers = new List<CustomerModel>();
        public readonly List<WorkstationModel> Workstations = new List<WorkstationModel>();
        private readonly List<ManagementTransaction> _managementTransactions = new List<ManagementTransaction>();
        public IReadOnlyList<ManagementTransaction> ManagementTransactions => _managementTransactions;
        private readonly List<CustomerModel> _waitingQueue = new List<CustomerModel>();
        private readonly HashSet<int> _issuedCustomerIds = new HashSet<int>();
        private readonly Dictionary<int, PlayerContext> _playerContexts =
            new Dictionary<int, PlayerContext>();
        private readonly Stage3WashServiceAdapter _washServiceAdapter;
        private readonly Dictionary<int, HaircutConfig> _activeHaircutConfigs =
            new Dictionary<int, HaircutConfig>();
        private readonly Dictionary<int, SalonTool> _activeHaircutTools =
            new Dictionary<int, SalonTool>();
        public IReadOnlyList<CustomerModel> WaitingQueue => _waitingQueue;
        public IReadOnlyDictionary<int, PlayerContext> PlayerContexts => _playerContexts;
        public event Action<CustomerModel> CustomerChanged;
        public event Action<SalonViewState, CustomerModel> ViewChanged;
        public event Action<PlayerContext> PlayerContextChanged;
        public event Action<CustomerModel> HaircutOvercut;
        public event Action<PaymentDropModel> PaymentCreated;

        public bool IsOver => RemainingTime <= 0f;

        public SalonGameModel(
            SalonRewardConfig rewardConfig = null,
            CustomerPatienceConfig patienceConfig = null,
            SalonFlowConfig flowConfig = null,
            SalonServiceConfig serviceConfig = null,
            CustomerExperienceProfile experienceProfile = null)
        {
            Payments = new SalonPaymentModel(8640, rewardConfig);
            PatienceConfig = patienceConfig ?? new CustomerPatienceConfig();
            FlowConfig = flowConfig ?? new SalonFlowConfig();
            ServiceConfig = serviceConfig ?? new SalonServiceConfig();
            ExperienceProfile = experienceProfile ?? new CustomerExperienceProfile();
            _washServiceAdapter = new Stage3WashServiceAdapter();
            GetOrCreatePlayerContext(LocalPlayerId);
            RemainingTime = Math.Max(0f, FlowConfig.BusinessDuration);
            Workstations.Add(new WorkstationModel { Id = 0, Type = WorkstationType.Wash, ServicePoint = new ServicePointModel { X = -6.2f, Z = 3.6f } });
            Workstations.Add(WorkstationModel.Haircut(1));
            Workstations.Add(WorkstationModel.Haircut(2));
            Workstations.Add(new WorkstationModel { Id = 3, Type = WorkstationType.Perm, ServicePoint = new ServicePointModel { X = 7.1f, Z = 2.8f } });
            Workstations.Add(new WorkstationModel { Id = 4, Type = WorkstationType.Wash, ServicePoint = new ServicePointModel { X = -4.6f, Z = 3.6f } });
            Workstations[1].ServicePoint = new ServicePointModel { X = -1.8f, Z = -0.6f };
            Workstations[2].ServicePoint = new ServicePointModel { X = 2.1f, Z = -0.6f };
            Workstations[0].AvailableTools.Add(SalonTool.Shampoo);
            Workstations[4].AvailableTools.Add(SalonTool.Shampoo);
            Workstations[3].AvailableTools.Add(SalonTool.PermSolution);
        }

        public bool PlayerBusy
        {
            get
            {
                foreach (CustomerModel customer in Customers)
                    if (customer.AttentionState == CustomerAttentionState.ActiveOperation ||
                        customer.ActiveServiceAction != ActiveServiceAction.None ||
                        customer.ManualBlowHolding)
                        return true;
                return false;
            }
        }

        public void Tick(float dt)
        {
            float step = Math.Max(0f, dt);
            WorldElapsed += step;
            RemainingTime = Math.Max(0f, RemainingTime - step);
            for (int i = Customers.Count - 1; i >= 0; i--)
            {
                var customer = Customers[i];
                UpdateProcessingService(customer, step);
                // Active timed service actions (shampoo / rinse / towel / dye / perm) are
                // advanced by the model, not by a specific input path. Previously only the
                // desktop loop ticked them, so a timed action started from the mobile path
                // could never finish. Both paths now share this single advancement point.
                TickActiveServiceAction(customer, step);
                bool wasMovingToStation = customer.State == CustomerState.MovingToStation;
                customer.BackgroundTask.Tick(step);
                CustomerState stateBeforeServiceUpdate = customer.State;
                UpdateServiceExecution(customer, step);
                if (customer.ServiceExecution == null ||
                    customer.ServiceExecution.State != ServiceExecutionState.Executing)
                    UpdateServiceStage(customer, step);
                bool serviceUpdateChangedState = customer.State != stateBeforeServiceUpdate;

                if ((customer.State == CustomerState.Entering || customer.State == CustomerState.Waiting ||
                    customer.State == CustomerState.MovingToStation) && !customer.HasServiceEngaged)
                    customer.TotalWaitSeconds += step;
                else if ((customer.State == CustomerState.Waiting || customer.State == CustomerState.MovingToStation) &&
                         customer.HasServiceEngaged)
                    AddServiceDelay(customer, step);
                else if (customer.State == CustomerState.Serving)
                {
                    // Once a step has been engaged, a customer who is left waiting for
                    // the next operation is on the service-delay clock. Active operations
                    // and the unattended auto-blow timer remain protected until the player
                    // returns to finish them.
                    if ((IsAwaitingTransfer(customer) || customer.HasServiceEngaged) &&
                        !IsUninterruptibleOperation(customer))
                        AddServiceDelay(customer, step);
                    else customer.ServiceElapsed += step;
                }

                if (!serviceUpdateChangedState) customer.StateElapsed += step;
                if (customer.State == CustomerState.Entering)
                {
                    if (customer.StateElapsed >= EnteringSeconds)
                        ChangeState(customer, CustomerState.Waiting);
                    continue;
                }

                if (customer.State == CustomerState.Finished)
                {
                    if (customer.StateElapsed >= FinishedFeedbackSeconds)
                    {
                        ClearFocusForCustomer(customer.Id);
                        ReleaseWorkstation(customer);
                        ChangeState(customer, CustomerState.Leaving);
                    }
                    continue;
                }

                if (customer.State == CustomerState.Leaving)
                {
                    if (customer.StateElapsed >= LeavingSeconds)
                    {
                        ChangeState(customer, CustomerState.Exited);
                        ClearFocusForCustomer(customer.Id);
                        Customers.RemoveAt(i);
                    }
                    continue;
                }

                if (customer.State == CustomerState.MovingToStation &&
                    customer.StateElapsed >= MovingToStationSeconds)
                {
                    ConfirmStationArrival(customer);
                }

                if (customer.State == CustomerState.Waiting || customer.State == CustomerState.MovingToStation || customer.State == CustomerState.Serving)
                {
                    bool reactionWasVisible = customer.ReactionKind != CustomerReactionKind.None
                        && customer.ReactionRemaining > 0f;
                    customer.ReactionRemaining = Math.Max(0f, customer.ReactionRemaining - step);
                    if (reactionWasVisible && customer.ReactionRemaining <= 0f)
                    {
                        customer.ReactionKind = CustomerReactionKind.None;
                        customer.WrongServiceKind = CustomerWrongServiceKind.None;
                        CustomerChanged?.Invoke(customer);
                    }
                    if (wasMovingToStation || customer.State == CustomerState.MovingToStation) continue;
                    if (customer.State == CustomerState.Serving && customer.ServiceArrivalGraceRemaining > 0f)
                    {
                        float graceUsed = Math.Min(step, customer.ServiceArrivalGraceRemaining);
                        customer.ServiceArrivalGraceRemaining = Math.Max(0f,
                            customer.ServiceArrivalGraceRemaining - graceUsed);
                        float remainingStep = step - graceUsed;
                        if (remainingStep <= 0f) continue;
                        float graceMultiplier = PatienceDrainMultiplier(customer);
                        if (graceMultiplier > 0f) DrainPatience(customer, remainingStep * graceMultiplier);
                        continue;
                    }
                    float multiplier = PatienceDrainMultiplier(customer);
                    if (multiplier > 0f) DrainPatience(customer, step * multiplier);
                }
            }
        }

        public bool BeginServiceExecution(CustomerModel customer, ServiceExecutionType type)
        {
            if (customer == null || customer.State != CustomerState.Serving ||
                customer.Station < 0 || customer.Station >= Workstations.Count) return false;
            // Haircut is a deliberate press-and-hold interaction. It must only enter through
            // BeginHaircutAction/TickHaircutAction and can never run as a background timer.
            if (type == ServiceExecutionType.Haircut) return false;
            if (customer.ServiceExecution != null &&
                customer.ServiceExecution.State == ServiceExecutionState.Executing) return false;

            WorkstationType stationType = Workstations[customer.Station].Type;
            bool stationMatches = type == ServiceExecutionType.Wash
                ? stationType == WorkstationType.Wash
                : stationType == WorkstationType.Haircut;
            if (!stationMatches) return false;

            float duration = type == ServiceExecutionType.Wash
                ? ServiceConfig.WashServiceDuration
                : ServiceConfig.BlowDryServiceDuration;
            customer.ServiceExecution = _washServiceAdapter.StartServiceExecution(
                customer, type, Math.Max(0f, duration), WorldElapsed);
            customer.ServiceExecutionCompletion = null;
            EngageService(customer);
            customer.AttentionState = CustomerAttentionState.ServiceEngaged;
            SetWorkstationState(customer, WorkstationState.InService);
            CustomerChanged?.Invoke(customer);
            return true;
        }

        public bool BeginProcessingServiceApply(CustomerModel customer)
        {
            if (customer == null || customer.State != CustomerState.Serving || customer.IsComplete ||
                customer.Station < 0 || customer.Station >= Workstations.Count ||
                customer.ActiveServiceAction != ActiveServiceAction.None || customer.IsProcessing ||
                PlayerBusy)
                return false;

            ServiceType service = customer.CurrentNeed;
            if (ClassifyProcessingServiceApply(customer, service).Relation !=
                ServiceRelation.NormalService)
                return false;

            ActiveServiceAction action = service == ServiceType.Dye
                ? ActiveServiceAction.ApplyDye : ActiveServiceAction.ApplyPerm;
            float duration = service == ServiceType.Dye
                ? ServiceConfig.DyeApplyDuration : ServiceConfig.PermApplyDuration;
            customer.ProcessStage = service == ServiceType.Dye
                ? ServiceProcessStage.DyeApply : ServiceProcessStage.PermApply;
            customer.ProcessingElapsed = 0f;
            customer.ProcessingDuration = 0f;
            return BeginTimedAction(customer, action, duration);
        }

        public ServiceActionClassification ClassifyProcessingServiceApply(
            CustomerModel customer, ServiceType attemptedService)
        {
            if (customer == null || (attemptedService != ServiceType.Dye &&
                attemptedService != ServiceType.Perm))
                return new ServiceActionClassification(ServiceRelation.WrongService, false,
                    ServiceActionClassificationReason.UnrequestedService);

            int requestedIndex = customer.Needs.IndexOf(attemptedService);
            if (requestedIndex < 0 || customer.Station < 0 || customer.Station >= Workstations.Count ||
                !IsCompatibleStation(attemptedService, Workstations[customer.Station].Type))
                return new ServiceActionClassification(ServiceRelation.WrongService, false,
                    ServiceActionClassificationReason.UnrequestedService);
            if (customer.Step > requestedIndex)
                return new ServiceActionClassification(ServiceRelation.ExtraService, false,
                    ServiceActionClassificationReason.CompletedServiceRepeated);
            if (customer.IsComplete || customer.CurrentNeed != attemptedService)
                return new ServiceActionClassification(ServiceRelation.WrongService, false,
                    ServiceActionClassificationReason.UnrequestedService);
            return new ServiceActionClassification(ServiceRelation.NormalService, false,
                ServiceActionClassificationReason.None);
        }

        private void BeginProcessingWait(CustomerModel customer, ServiceType service)
        {
            customer.ProcessStage = service == ServiceType.Dye
                ? ServiceProcessStage.DyeProcessing : ServiceProcessStage.PermProcessing;
            customer.ProcessingElapsed = 0f;
            customer.ProcessingDuration = Math.Max(0f, service == ServiceType.Dye
                ? ServiceConfig.DyeProcessingDuration : ServiceConfig.PermProcessingDuration);
            customer.DyeCleanupGraceDurationSnapshot = service == ServiceType.Dye
                ? Math.Max(.01f, ServiceConfig.DyeCleanupGraceDuration) : 0f;
            customer.ServicePhase = ServiceStepPhase.BackgroundRunning;
            customer.AttentionState = CustomerAttentionState.ServiceEngaged;
            SetWorkstationState(customer, WorkstationState.InService);
            CustomerChanged?.Invoke(customer);
        }

        private void UpdateProcessingService(CustomerModel customer, float deltaTime)
        {
            if (customer == null || !customer.IsProcessing || customer.State != CustomerState.Serving)
                return;
            customer.ProcessingElapsed += Math.Max(0f, deltaTime);
            if (customer.ProcessStage == ServiceProcessStage.DyeProcessing ||
                customer.ProcessStage == ServiceProcessStage.DyeReadyForCleanup)
            {
                if (customer.ProcessStage == ServiceProcessStage.DyeProcessing &&
                    customer.ProcessingElapsed >= customer.ProcessingDuration)
                {
                    customer.ProcessStage = ServiceProcessStage.DyeReadyForCleanup;
                    customer.ServicePhase = ServiceStepPhase.Ready;
                    SetWorkstationState(customer, WorkstationState.AwaitingService);
                }
                float grace = Math.Max(.01f, customer.DyeCleanupGraceDurationSnapshot);
                if (customer.ProcessStage == ServiceProcessStage.DyeReadyForCleanup &&
                    customer.ProcessingElapsed > customer.ProcessingDuration + grace)
                {
                    FailUnhandledDye(customer);
                    return;
                }
                CustomerChanged?.Invoke(customer);
                return;
            }

            customer.ProcessingElapsed = Math.Min(customer.ProcessingDuration, customer.ProcessingElapsed);
            if (customer.ProcessingProgress < 1f)
            {
                CustomerChanged?.Invoke(customer);
                return;
            }

            _washServiceAdapter.CompleteExternalServiceMilestone(customer, MilestoneId.PermCompleted);
            customer.ProcessStage = ServiceProcessStage.Complete;
            customer.ServicePhase = ServiceStepPhase.Complete;
            if (customer.Step == customer.Needs.Count - 1) customer.Step++;
            CompleteCurrentStep(customer);
        }

        public bool ResolveDyeCleanup(CustomerModel customer)
        {
            if (customer == null || !customer.IsDyeCleanupReady || customer.State != CustomerState.Serving ||
                customer.Station < 0 || customer.Station >= Workstations.Count ||
                Workstations[customer.Station].Type != WorkstationType.Haircut ||
                SelectedCustomer != customer || PlayerBusy)
                return false;
            _washServiceAdapter.CompleteExternalServiceMilestone(customer, MilestoneId.DyeCompleted);
            customer.ProcessStage = ServiceProcessStage.Complete;
            customer.ServicePhase = ServiceStepPhase.Complete;
            if (customer.Step == customer.Needs.Count - 1) customer.Step++;
            CompleteCurrentStep(customer);
            return true;
        }

        private void FailUnhandledDye(CustomerModel customer)
        {
            customer.ProcessStage = ServiceProcessStage.DyeFailed;
            customer.ServicePhase = ServiceStepPhase.Late;
            customer.ServiceFeedback = CustomerServiceFeedback.Dissatisfied;
            customer.ServiceResult = CustomerServiceResult.Failed;
            customer.Emotion = CustomerEmotion.Angry;
            customer.ReactionKind = CustomerReactionKind.Protest;
            customer.ReactionRemaining = Math.Max(customer.ReactionRemaining, FinishedFeedbackSeconds);
            customer.HadServiceDelay = true;
            Mistakes++;
            ApplyAccidentSeverity(customer, AccidentSeverity.Major);
            SetWorkstationState(customer, WorkstationState.Completed);
            ChangeState(customer, CustomerState.Finished);
            CustomerChanged?.Invoke(customer);
        }

        private void UpdateServiceExecution(CustomerModel customer, float deltaTime)
        {
            ServiceExecution execution = customer == null ? null : customer.ServiceExecution;
            if (execution == null || execution.State != ServiceExecutionState.Executing) return;
            execution.Tick(deltaTime);
            if (execution.Progress < 1f)
            {
                CustomerChanged?.Invoke(customer);
                return;
            }

            ServiceExecutionCompletion completion = _washServiceAdapter.CompleteServiceExecution(customer);
            customer.ServiceExecutionCompletion = completion;
            if (completion.ApplyResult.Status != ApplyStatus.Applied) return;
            customer.AttentionState = CustomerAttentionState.ServiceEngaged;
            ServiceType completedService = execution.Type == ServiceExecutionType.Wash
                ? ServiceType.Wash : ServiceType.Dry;
            if (!customer.IsComplete && customer.CurrentNeed == completedService)
            {
                customer.ServicePhase = ServiceStepPhase.Complete;
                if (execution.Type == ServiceExecutionType.Wash) customer.CompletedWashCount++;
                CompleteCurrentStep(customer, true);
                return;
            }

            customer.ServicePhase = ServiceStepPhase.Ready;
            ApplyExtraService(customer);
        }

        public CustomerModel Spawn(int id, IList<ServiceType> needs)
        {
            if (_issuedCustomerIds.Contains(id))
                throw new ArgumentException("Customer IDs must be unique within a session.", nameof(id));
            int waiting = 0;
            int active = 0;
            foreach (var customer in Customers)
            {
                if (customer.State != CustomerState.Exited) active++;
                if (customer.State == CustomerState.Entering || customer.State == CustomerState.Waiting) waiting++;
            }
            int maxCustomers = Math.Max(1, FlowConfig.MaxCustomers);
            int waitingCapacity = Math.Max(1, FlowConfig.WaitingCapacity);
            if (active >= maxCustomers || waiting >= waitingCapacity) return null;
            var created = new CustomerModel
            {
                Id = id,
                Needs = new List<ServiceType>(needs),
                State = CustomerState.Entering,
                MaxPatience = Math.Max(.01f, ExperienceProfile.MaxPatience),
                Patience = Math.Min(Math.Max(0f, ExperienceProfile.MaxPatience),
                    Math.Max(0f, ExperienceProfile.InitialPatience)),
                Satisfaction = Math.Max(0f, Math.Min(100f, ExperienceProfile.InitialSatisfaction))
            };
            _issuedCustomerIds.Add(id);
            Customers.Add(created);
            _waitingQueue.Add(created);
            _washServiceAdapter.Register(created);
            CustomerChanged?.Invoke(created);
            return created;
        }

        public int GetWaitingSlot(CustomerModel customer)
        {
            if (customer == null) return -1;
            int index = _waitingQueue.IndexOf(customer);
            return index >= 0 && index < Math.Max(1, FlowConfig.WaitingCapacity) ? index : -1;
        }

        public int AutoAssignWaitingCustomers()
        {
            int assigned = 0;
            for (int station = 0; station < Workstations.Count; station++)
            {
                if (Workstations[station].Type != WorkstationType.Haircut || IsStationOccupied(station))
                    continue;
                if (_waitingQueue.Count == 0) break;
                CustomerModel head = _waitingQueue[0];
                if (head.State != CustomerState.Waiting || head.CurrentNeed != ServiceType.Cut)
                    break;
                if (Assign(head, station)) assigned++;
            }
            return assigned;
        }

        public void SelectCustomer(CustomerModel customer)
        {
            SelectCustomer(LocalPlayerId, customer);
        }

        public void SelectCustomer(int playerId, CustomerModel customer)
        {
            if (customer == null || !Customers.Contains(customer) ||
                customer.State == CustomerState.Leaving || customer.State == CustomerState.Exited) return;
            PlayerContext context = GetOrCreatePlayerContext(playerId);
            context.FocusCustomer(customer.Id, customer.Station);
            if (playerId == LocalPlayerId) _washServiceAdapter.FocusCustomer(customer);
            PlayerContextChanged?.Invoke(context);
            if (playerId == LocalPlayerId)
                ViewChanged?.Invoke(context.ViewState, customer);
        }

        public void ClearFocus()
        {
            ClearFocus(LocalPlayerId);
        }

        public void ClearFocus(int playerId)
        {
            PlayerContext context = GetOrCreatePlayerContext(playerId);
            context.ClearFocus();
            PlayerContextChanged?.Invoke(context);
            if (playerId == LocalPlayerId)
                ViewChanged?.Invoke(context.ViewState, null);
        }

        public PlayerContext GetOrCreatePlayerContext(int playerId)
        {
            if (!_playerContexts.TryGetValue(playerId, out PlayerContext context))
            {
                context = new PlayerContext(playerId);
                _playerContexts.Add(playerId, context);
            }
            return context;
        }

        public CustomerModel FindCustomer(int customerId)
        {
            if (customerId < 0) return null;
            for (int i = 0; i < Customers.Count; i++)
                if (Customers[i].Id == customerId) return Customers[i];
            return null;
        }

        public CustomerPhysicalStateSnapshot GetServicePhysicalSnapshot(CustomerModel customer) =>
            _washServiceAdapter.Physical(customer);

        public ServiceProgressSnapshot GetServiceProgressSnapshot(CustomerModel customer) =>
            _washServiceAdapter.Progress(customer);

        public InteractionContextSnapshot GetServiceInteractionSnapshot(CustomerModel customer) =>
            _washServiceAdapter.Interaction(customer);

        public IReadOnlyList<ActionHistoryEntry> GetServiceActionHistory(CustomerModel customer) =>
            _washServiceAdapter.History(customer);

        public ServiceExitReadinessResult GetServiceExitReadiness(CustomerModel customer) =>
            _washServiceAdapter.ExitReadiness(customer);

        private void ClearFocusForCustomer(int customerId)
        {
            foreach (KeyValuePair<int, PlayerContext> pair in _playerContexts)
            {
                if (pair.Value.SelectedCustomerId != customerId) continue;
                pair.Value.ClearFocus();
                PlayerContextChanged?.Invoke(pair.Value);
                if (pair.Key == LocalPlayerId)
                    ViewChanged?.Invoke(pair.Value.ViewState, null);
            }
        }

        private void ClearAllPlayerFocus()
        {
            foreach (KeyValuePair<int, PlayerContext> pair in _playerContexts)
            {
                pair.Value.ClearFocus();
                PlayerContextChanged?.Invoke(pair.Value);
                if (pair.Key == LocalPlayerId)
                    ViewChanged?.Invoke(pair.Value.ViewState, null);
            }
        }

        private void RefreshFocusedStation(CustomerModel customer)
        {
            if (customer == null) return;
            foreach (KeyValuePair<int, PlayerContext> pair in _playerContexts)
            {
                if (pair.Value.SelectedCustomerId != customer.Id) continue;
                pair.Value.FocusCustomer(customer.Id, customer.Station);
                PlayerContextChanged?.Invoke(pair.Value);
                if (pair.Key == LocalPlayerId)
                    ViewChanged?.Invoke(pair.Value.ViewState, customer);
            }
        }

        public bool Assign(CustomerModel customer, int station)
        {
            if (customer == null || station < 0 || station >= Workstations.Count) return false;
            bool isWaiting = customer.State == CustomerState.Waiting;
            bool isTransfer = customer.State == CustomerState.Serving &&
                              customer.Station >= 0 && customer.Station < Workstations.Count &&
                              !IsMovementLocked(customer);
            if (!isWaiting && !isTransfer) return false;
            WorkstationType targetType = Workstations[station].Type;
            if (IsStationOccupied(station)) return false;
            int waitingIndex = isWaiting ? _waitingQueue.IndexOf(customer) : -1;
            if (isWaiting && waitingIndex < 0) return false;
            if (isTransfer) ReleaseWorkstation(customer);
            Workstations[station].CurrentCustomerId = customer.Id;
            Workstations[station].CurrentService = customer.CurrentNeed;
            Workstations[station].State = WorkstationState.Reserved;
            customer.Station = station;
            _washServiceAdapter.MovementStarted(customer, station);
            // 泡沫等待是绑定洗头工位的后台任务。顾客一旦被转移，计时必须取消，
            // 否则一个已经失效的计时器会在别的工位继续判定迟到事故。
            // 吹发后台任务的 CurrentNeed 是 Dry，因此不受这里影响。
            if (customer.BackgroundTask.State == BackgroundTaskState.Running &&
                customer.CurrentNeed == ServiceType.Wash)
            {
                customer.BackgroundTask.State = BackgroundTaskState.Inactive;
                customer.BackgroundTask.RiskState = BackgroundRiskState.None;
            }
            RefreshFocusedStation(customer);
            if (!IsCompatibleStation(customer.CurrentNeed, targetType))
            {
                customer.WrongStationCount++;
                ApplySatisfactionLoss(customer,
                    ExperienceProfile.WrongStationPenalty * ExperienceProfile.WrongStationSensitivity);
                customer.ReactionKind = CustomerReactionKind.Confused;
                customer.ReactionRemaining = Math.Max(customer.ReactionRemaining, 1.2f);
            }
            else if (customer.ReactionKind == CustomerReactionKind.Confused)
            {
                customer.ReactionKind = CustomerReactionKind.None;
                customer.ReactionRemaining = 0f;
            }
            if (customer.Emotion != CustomerEmotion.Calm)
                customer.WasImpatientBeforeService = true;
            if (!customer.HasServiceEngaged)
            {
                customer.ServiceElapsed = 0f;
                customer.AttentionState = CustomerAttentionState.Unserved;
            }
            if (isWaiting) _waitingQueue.RemoveAt(waitingIndex);
            ChangeState(customer, CustomerState.MovingToStation);
            if (customer.CurrentNeed == ServiceType.Cut &&
                (customer.HaircutService == null || customer.HaircutService.State != HaircutServiceState.Active))
                customer.HaircutService = new HaircutServiceModel(SalonTool.Scissors);
            if (isWaiting) PrepareCurrentStep(customer);
            Workstations[station].State = WorkstationState.CustomerEnRoute;
            // View/focus callbacks run during assignment. Reassert the invariant after all of
            // them: Dye at either HairStation must never retain a question-mark reaction.
            ClearCompatibleStationConfusion(customer, false);
            return true;
        }

        public bool ConfirmStationArrival(CustomerModel customer)
        {
            if (customer == null || customer.State != CustomerState.MovingToStation ||
                customer.Station < 0 || customer.Station >= Workstations.Count ||
                Workstations[customer.Station].CurrentCustomerId != customer.Id)
                return false;

            ClearCompatibleStationConfusion(customer, false);
            ChangeState(customer, CustomerState.Serving);
            _washServiceAdapter.StationArrived(customer);
            customer.ServiceArrivalGraceRemaining = Math.Max(0f,
                PatienceConfig.ServiceArrivalGraceSeconds);
            SetWorkstationState(customer, WorkstationState.AwaitingService);
            return true;
        }

        public bool ClearCompatibleStationConfusion(CustomerModel customer, bool notify = true)
        {
            if (customer == null || customer.Station < 0 || customer.Station >= Workstations.Count ||
                !IsCompatibleStation(customer.CurrentNeed, Workstations[customer.Station].Type) ||
                customer.ReactionKind != CustomerReactionKind.Confused)
                return false;

            customer.ReactionKind = CustomerReactionKind.None;
            customer.ReactionRemaining = 0f;
            if (notify) CustomerChanged?.Invoke(customer);
            return true;
        }

        private static bool IsMovementLocked(CustomerModel customer)
        {
            if (customer == null) return true;
            return customer.ActiveServiceAction != ActiveServiceAction.None ||
                   customer.AttentionState == CustomerAttentionState.ActiveOperation ||
                   customer.ManualBlowHolding || customer.AutoBlowRunning || customer.IsProcessing ||
                   (customer.ServiceExecution != null &&
                    customer.ServiceExecution.State == ServiceExecutionState.Executing);
        }

        public bool IsAwaitingTransfer(CustomerModel customer)
        {
            return customer != null && customer.State == CustomerState.Serving &&
                   customer.Station >= 0 && customer.Station < Workstations.Count &&
                   Workstations[customer.Station].State == WorkstationState.AwaitingTransfer;
        }

        public bool BeginActiveOperation(CustomerModel customer)
        {
            return BeginActiveOperation(customer, false);
        }

        private bool BeginActiveOperation(CustomerModel customer, bool allowHaircutBlockedState)
        {
            if (customer == null || customer.State != CustomerState.Serving) return false;
            if (customer.Station < 0 || customer.Station >= Workstations.Count ||
                Workstations[customer.Station].Type != WorkstationType.Haircut) return false;
            CustomerPhysicalStateSnapshot physical = _washServiceAdapter.Physical(customer);
            if (!allowHaircutBlockedState && (physical.IsTowelWrapped
                || physical.ShampooState != ShampooState.None || physical.FoamAmount > 0f))
                return false;
            if (customer.CurrentNeed == ServiceType.Dry && customer.BlowStage != BlowStage.AwaitingStart) return false;
            if (customer.AttentionState == CustomerAttentionState.ActiveOperation) return true;
            EngageService(customer);
            customer.AttentionState = CustomerAttentionState.ActiveOperation;
            SetWorkstationState(customer, WorkstationState.InService);
            return true;
        }

        public bool EndActiveOperation(CustomerModel customer)
        {
            if (customer == null || customer.AttentionState != CustomerAttentionState.ActiveOperation)
                return false;

            customer.AttentionState = CustomerAttentionState.ServiceEngaged;
            return true;
        }

        public bool IsStationOccupied(int station)
        {
            if (station >= 0 && station < Workstations.Count && Workstations[station].Occupied)
                return true;
            foreach (var customer in Customers)
                if (customer.Station == station &&
                    (customer.State == CustomerState.MovingToStation || customer.State == CustomerState.Serving ||
                     customer.State == CustomerState.Finished)) return true;
            return false;
        }

        public bool ConfigureHaircutOrder(CustomerModel customer, params SalonTool[] tools)
        {
            if (customer == null || customer.CurrentNeed != ServiceType.Cut) return false;
            customer.HaircutService = new HaircutServiceModel(tools);
            customer.HairStage = HairStage.Original;
            customer.LastHaircutRating = HaircutServiceRating.None;
            _washServiceAdapter.ConfigureHaircutOrder(customer);
            CustomerChanged?.Invoke(customer);
            return true;
        }

        public static bool IsCompatibleStation(ServiceType service, int station)
        {
            if (station == 0 || station == 4) return service == ServiceType.Wash;
            if (station == 1 || station == 2)
                return service == ServiceType.Cut || service == ServiceType.Dry || service == ServiceType.Dye;
            return station == 3 && service == ServiceType.Perm;
        }

        public static bool IsCompatibleStation(ServiceType service, WorkstationType station)
        {
            if (service == ServiceType.Wash) return station == WorkstationType.Wash;
            if (service == ServiceType.Cut || service == ServiceType.Dry || service == ServiceType.Dye)
                return station == WorkstationType.Haircut;
            return service == ServiceType.Perm && station == WorkstationType.Perm;
        }

        public static WorkstationType RequiredStationFor(ServiceType service)
        {
            if (service == ServiceType.Wash) return WorkstationType.Wash;
            if (service == ServiceType.Cut || service == ServiceType.Dry || service == ServiceType.Dye)
                return WorkstationType.Haircut;
            return WorkstationType.Perm;
        }

        public bool ApplyService(CustomerModel customer, ServiceType tool, float progress)
        {
            if (customer == null || customer.State == CustomerState.Exited || customer.State == CustomerState.Leaving || customer.State == CustomerState.Finished) return false;
            if (!customer.Needs.Contains(tool))
            {
                customer.Satisfaction = Math.Max(0f, customer.Satisfaction - 8f);
                Mistakes++;
                CustomerChanged?.Invoke(customer);
                return false;
            }
            if (customer.State != CustomerState.Serving || !IsCompatibleStation(tool, customer.Station)) return false;
            if (progress >= 1f)
            {
                CompleteCurrentStep(customer);
            }
            return true;
        }

        public bool ApplyWrongStationTool(CustomerModel customer, SalonTool tool)
        {
            if (customer == null || customer.State != CustomerState.Serving || customer.Station < 0 ||
                customer.Station >= Workstations.Count ||
                IsCompatibleStation(customer.CurrentNeed, Workstations[customer.Station].Type) ||
                !Workstations[customer.Station].AvailableTools.Contains(tool)) return false;
            AccidentSeverity severity = tool == SalonTool.Scissors || tool == SalonTool.ThinningShears ||
                                        tool == SalonTool.Clippers
                ? AccidentSeverity.Major : AccidentSeverity.Moderate;
            Mistakes++;
            ApplyAccidentSeverity(customer, severity);
            customer.ServiceFeedback = CustomerServiceFeedback.Dissatisfied;
            if (severity == AccidentSeverity.Major)
            {
                customer.ServiceResult = CustomerServiceResult.Failed;
                customer.Emotion = CustomerEmotion.Angry;
                SetWorkstationState(customer, WorkstationState.Completed);
                ChangeState(customer, CustomerState.Finished);
            }
            CustomerChanged?.Invoke(customer);
            return true;
        }

        public bool BeginWashAction(CustomerModel customer, WashAction action)
        {
            if (!CanUseWashStation(customer)) return false;
            if (action == WashAction.WrapTowel)
                return PerformQuickAction(customer, ActiveServiceAction.WrapTowel) ==
                       ServiceActionResult.QuickActionCompleted;

            bool shower = action == WashAction.Shower || action == WashAction.Wet || action == WashAction.Rinse;
            if (!shower && action != WashAction.Shampoo) return false;
            ActiveServiceAction active = shower ? ActiveServiceAction.Shower : ActiveServiceAction.Shampoo;
            ServiceActionType domainAction = shower ? ServiceActionType.Shower : ServiceActionType.Shampoo;
            ServiceTool tool = shower ? ServiceTool.Shower : ServiceTool.Shampoo;
            CustomerPhysicalStateSnapshot physical = _washServiceAdapter.Physical(customer);
            float duration = shower && physical.FoamAmount > 0f
                && customer.AccidentSeverity >= AccidentSeverity.Moderate
                ? ServiceConfig.OverdueRinseDuration
                : shower ? ServiceConfig.RinseDuration : ServiceConfig.ShampooDuration;

            if (!_washServiceAdapter.BeginTimedAction(customer, domainAction, tool, WorldElapsed))
                return false;
            if (!BeginTimedAction(customer, active, duration))
            {
                _washServiceAdapter.CompleteTimedAction(customer, 0f, true);
                return false;
            }
            _washServiceAdapter.Project(customer);
            CustomerChanged?.Invoke(customer);
            return true;
        }

        /// <summary>
        /// 正式洗头第 1 段：短主动洗发。
        /// 一次调用内先复用领域「打湿」动作（立即结算），再开启一个短时「打泡沫」动作。
        /// 泡沫动作完成后由 <see cref="TickActiveServiceAction"/> 自动进入后台泡沫等待，
        /// 玩家可以离开，稍后必须回来冲洗。不新增任何移动版专用状态。
        /// </summary>
        public bool BeginWashFoamHold(CustomerModel customer)
        {
            if (!CanUseWashStation(customer) || customer.CurrentNeed != ServiceType.Wash) return false;
            if (customer.ServiceExecution != null &&
                customer.ServiceExecution.State == ServiceExecutionState.Executing) return false;
            if (PlayerBusy) return false;

            // Wet hair first through the same domain action the detailed desktop flow uses.
            if (!_washServiceAdapter.BeginTimedAction(
                    customer, ServiceActionType.Shower, ServiceTool.Shower, WorldElapsed)) return false;
            ApplyActionResult wetted = _washServiceAdapter.CompleteTimedAction(
                customer, Math.Max(0f, ServiceConfig.RinseDuration), false);
            if (wetted.Status != ApplyStatus.Applied) return false;

            if (!_washServiceAdapter.BeginTimedAction(
                    customer, ServiceActionType.Shampoo, ServiceTool.Shampoo, WorldElapsed)) return false;
            if (!BeginTimedAction(customer, ActiveServiceAction.Shampoo, ServiceConfig.ShampooDuration))
            {
                _washServiceAdapter.DiscardTimedAction(customer, ClearReason.ToolBecameInvalid);
                return false;
            }
            CustomerChanged?.Invoke(customer);
            return true;
        }

        /// <summary>
        /// 正式洗头第 2 段：回来冲洗收尾。
        /// 复用领域「冲洗」动作把泡沫清零并满足 RinseClean 里程碑，随后推进订单步数。
        /// 冲洗耗时在事故达到 Moderate 时延长，复用既有的 OverdueRinseDuration 规则。
        /// </summary>
        public bool FinishWashRinse(CustomerModel customer)
        {
            if (!CanUseWashStation(customer) || customer.CurrentNeed != ServiceType.Wash) return false;
            if (customer.ServiceExecution != null &&
                customer.ServiceExecution.State == ServiceExecutionState.Executing) return false;
            if (PlayerBusy) return false;
            if (!IsWashFoamReadyToRinse(customer)) return false;

            if (!_washServiceAdapter.BeginTimedAction(
                    customer, ServiceActionType.Shower, ServiceTool.Shower, WorldElapsed)) return false;
            float duration = customer.AccidentSeverity >= AccidentSeverity.Moderate
                ? ServiceConfig.OverdueRinseDuration : ServiceConfig.RinseDuration;
            ApplyActionResult rinsed = _washServiceAdapter.CompleteTimedAction(
                customer, Math.Max(0f, duration), false);
            if (rinsed.Status != ApplyStatus.Applied) return false;

            customer.BackgroundTask.State = BackgroundTaskState.Complete;
            customer.ServicePhase = ServiceStepPhase.Complete;
            if (_washServiceAdapter.IsWashReadyForTransition(customer) && !customer.IsComplete)
            {
                customer.CompletedWashCount++;
                CompleteCurrentStep(customer, true);
            }
            else
            {
                SetWorkstationState(customer, WorkstationState.AwaitingService);
                CustomerChanged?.Invoke(customer);
            }
            return true;
        }

        /// <summary>泡沫后台等待是否正在进行；View 只能读取，不得自行推导。</summary>
        public bool IsWashFoamWaitRunning(CustomerModel customer)
        {
            return customer != null && customer.CurrentNeed == ServiceType.Wash &&
                   customer.ShampooApplied &&
                   customer.BackgroundTask.State == BackgroundTaskState.Running;
        }

        /// <summary>泡沫是否已经进入可冲洗窗口（玩家回来的时机）。</summary>
        public bool IsWashFoamReadyToRinse(CustomerModel customer)
        {
            return IsWashFoamWaitRunning(customer) &&
                   customer.BackgroundTask.Elapsed >= Math.Max(0f, ServiceConfig.FoamOptimalStart);
        }

        public ServiceActionResult ResolveWashToolSelection(CustomerModel customer, WashAction action)
        {
            if (!CanUseWashStation(customer)) return ServiceActionResult.Invalid;
            if (action == WashAction.WrapTowel)
                return PerformQuickAction(customer, ActiveServiceAction.WrapTowel);

            bool correctShower = (action == WashAction.Shower || action == WashAction.Wet ||
                                  action == WashAction.Rinse) &&
                                 (customer.WashStage == WashStage.Dry ||
                                  customer.WashStage == WashStage.Foamy ||
                                  (customer.WashStage == WashStage.Toweled && !customer.TowelWrapped));
            if (correctShower) return ServiceActionResult.Progressed;

            if (action == WashAction.Shampoo &&
                (customer.WashStage == WashStage.Wet ||
                 customer.WashStage == WashStage.ShampooApplied))
            {
                if (customer.WashStage == WashStage.Wet && !ApplyShampoo(customer))
                    return ServiceActionResult.Failed;
                return ServiceActionResult.Progressed;
            }

            if (action == WashAction.Shampoo || action == WashAction.Shower ||
                action == WashAction.Wet || action == WashAction.Rinse)
            {
                ClearInteractionForCustomer(customer.Id);
                ApplyExtraService(customer);
                return ServiceActionResult.ExtraService;
            }
            return ServiceActionResult.Invalid;
        }

        public ServiceActionResult PerformQuickAction(
            CustomerModel customer, ActiveServiceAction action)
        {
            if (customer == null) return ServiceActionResult.Invalid;
            ClearInteractionForCustomer(customer.Id);
            if (action == ActiveServiceAction.WrapTowel)
            {
                if (customer.ActiveServiceAction == ActiveServiceAction.Shower ||
                    customer.ActiveServiceAction == ActiveServiceAction.Shampoo)
                {
                    customer.ActiveServiceAction = ActiveServiceAction.None;
                    customer.ActiveServiceElapsed = 0f;
                    customer.ActiveServiceDuration = 0f;
                    EndActiveOperation(customer);
                    _washServiceAdapter.DiscardTimedAction(customer, ClearReason.ToolBecameInvalid);
                }
                if (!CanUseWashStation(customer))
                    return ServiceActionResult.Invalid;
                EngageService(customer);
                ApplyActionResult applied = _washServiceAdapter.ExecuteQuickAction(
                    customer, ServiceActionType.WrapTowel, ServiceTool.Towel, WorldElapsed);
                if (applied.Status != ApplyStatus.Applied) return ServiceActionResult.Invalid;
                customer.ActiveServiceAction = ActiveServiceAction.None;
                customer.ActiveServiceElapsed = 0f;
                customer.ActiveServiceDuration = 0f;
                if (_washServiceAdapter.IsWashReadyForTransition(customer)
                    && !customer.IsComplete && customer.CurrentNeed == ServiceType.Wash)
                {
                    customer.CompletedWashCount++;
                    customer.ServicePhase = ServiceStepPhase.Complete;
                    CompleteCurrentStep(customer, true);
                }
                else if (!customer.IsComplete && customer.CurrentNeed != ServiceType.Wash)
                {
                    customer.ServicePhase = ServiceStepPhase.Ready;
                    ApplyExtraService(customer);
                }
                else
                {
                    customer.ServicePhase = ServiceStepPhase.Ready;
                    SetWorkstationState(customer, WorkstationState.AwaitingService);
                    CustomerChanged?.Invoke(customer);
                }
                return ServiceActionResult.QuickActionCompleted;
            }
            CustomerPhysicalStateSnapshot physical = _washServiceAdapter.Physical(customer);
            if (action != ActiveServiceAction.RemoveTowel || !physical.IsTowelWrapped ||
                customer.ActiveServiceAction != ActiveServiceAction.None ||
                customer.State != CustomerState.Serving || customer.Station < 0 ||
                customer.Station >= Workstations.Count ||
                (Workstations[customer.Station].Type != WorkstationType.Haircut
                 && Workstations[customer.Station].Type != WorkstationType.Wash))
                return ServiceActionResult.Invalid;

            EngageService(customer);
            ApplyActionResult removed = _washServiceAdapter.ExecuteQuickAction(
                customer, ServiceActionType.RemoveTowel, ServiceTool.Towel, WorldElapsed);
            if (removed.Status != ApplyStatus.Applied) return ServiceActionResult.Invalid;
            customer.ActiveServiceAction = ActiveServiceAction.None;
            customer.ActiveServiceElapsed = 0f;
            customer.ActiveServiceDuration = 0f;
            customer.ServicePhase = ServiceStepPhase.Ready;
            if (!TryFinalizeCompletedOrder(customer, customer.Station))
            {
                SetWorkstationState(customer, WorkstationState.AwaitingService);
                CustomerChanged?.Invoke(customer);
            }
            return ServiceActionResult.QuickActionCompleted;
        }

        public bool ApplyShampoo(CustomerModel customer)
        {
            if (!CanUseWashStation(customer) || customer.WashStage != WashStage.Wet) return false;
            EngageService(customer);
            customer.ShampooApplied = true;
            customer.WashStage = WashStage.ShampooApplied;
            customer.ServicePhase = ServiceStepPhase.Ready;
            SetWorkstationState(customer, WorkstationState.AwaitingService);
            CustomerChanged?.Invoke(customer);
            return true;
        }

        public bool BeginTowelRemoval(CustomerModel customer)
        {
            if (customer == null || !_washServiceAdapter.Physical(customer).IsTowelWrapped
                || customer.State != CustomerState.Serving ||
                customer.Station < 0 || Workstations[customer.Station].Type != WorkstationType.Haircut) return false;
            if (!_washServiceAdapter.BeginTimedAction(
                customer, ServiceActionType.RemoveTowel, ServiceTool.Towel, WorldElapsed))
                return false;
            if (BeginTimedAction(customer, ActiveServiceAction.RemoveTowel, ServiceConfig.RemoveTowelDuration))
                return true;
            _washServiceAdapter.CompleteTimedAction(customer, 0f, true);
            return false;
        }

        public bool TickActiveServiceAction(CustomerModel customer, float dt)
        {
            if (customer == null || customer.ActiveServiceAction == ActiveServiceAction.None) return false;
            customer.ActiveServiceElapsed += Math.Max(0f, dt);
            if (customer.ActiveServiceElapsed < customer.ActiveServiceDuration) return true;

            ActiveServiceAction completed = customer.ActiveServiceAction;
            customer.ActiveServiceAction = ActiveServiceAction.None;
            customer.ActiveServiceElapsed = customer.ActiveServiceDuration;
            EndActiveOperation(customer);
            if (completed == ActiveServiceAction.ApplyDye || completed == ActiveServiceAction.ApplyPerm)
            {
                BeginProcessingWait(customer, completed == ActiveServiceAction.ApplyDye
                    ? ServiceType.Dye : ServiceType.Perm);
            }
            else if (completed == ActiveServiceAction.Shower || completed == ActiveServiceAction.Shampoo)
            {
                ActionResult preview = _washServiceAdapter.PreviewTimedActionResult(
                    customer, customer.ActiveServiceElapsed);
                ServiceActionClassification classification = ClassifyServiceAction(
                    customer, preview, GetServiceProgressSnapshot(customer), GetServicePhysicalSnapshot(customer));
                ApplyActionResult applied = _washServiceAdapter.CompleteTimedAction(
                    customer, customer.ActiveServiceElapsed, false);
                if (applied.Status != ApplyStatus.Applied) return false;
                ProjectDomainActionFeedback(customer, applied, classification);
                customer.ServicePhase = ServiceStepPhase.Ready;
                if (completed == ActiveServiceAction.Shampoo)
                {
                    // Shampooing leaves foam on the head. When the order still needs a wash,
                    // this starts the existing foam-wait background task so the player can
                    // walk away and must come back to rinse. The late thresholds already
                    // escalate to Minor/Moderate accidents inside UpdateServiceStage.
                    if (customer.CurrentNeed == ServiceType.Wash && customer.ShampooApplied)
                    {
                        customer.BackgroundTask.Start(
                            Math.Max(0f, ServiceConfig.FoamOptimalStart),
                            Math.Max(ServiceConfig.FoamOptimalStart, ServiceConfig.FoamMinorLateThreshold),
                            Math.Max(ServiceConfig.FoamMinorLateThreshold, ServiceConfig.FoamModerateLateThreshold),
                            WorldElapsed);
                        customer.ServicePhase = ServiceStepPhase.BackgroundRunning;
                    }
                    else
                    {
                        customer.BackgroundTask.State = BackgroundTaskState.Inactive;
                        customer.BackgroundTask.RiskState = BackgroundRiskState.None;
                    }
                }
                SetWorkstationState(customer, WorkstationState.AwaitingService);
                CustomerChanged?.Invoke(customer);
            }
            else if (completed == ActiveServiceAction.WrapTowel)
            {
                CompleteTowelWrap(customer);
            }
            else if (completed == ActiveServiceAction.RemoveTowel)
            {
                ApplyActionResult applied = _washServiceAdapter.CompleteTimedAction(
                    customer, customer.ActiveServiceElapsed, false);
                if (applied.Status != ApplyStatus.Applied) return false;
                customer.ServicePhase = ServiceStepPhase.Ready;
                if (!TryFinalizeCompletedOrder(customer, customer.Station))
                {
                    SetWorkstationState(customer, WorkstationState.AwaitingService);
                    CustomerChanged?.Invoke(customer);
                }
            }
            return true;
        }

        public bool IsWrongHaircutHoldAction(CustomerModel customer, float elapsedTime)
        {
            if (customer == null) return false;
            if (customer.WrongServiceKind != CustomerWrongServiceKind.None) return false;
            ActionResult action = _washServiceAdapter.PreviewHaircutResult(customer, elapsedTime);
            return IsWrongServiceAction(customer, action,
                GetServiceProgressSnapshot(customer), GetServicePhysicalSnapshot(customer));
        }

        private bool IsWrongServiceAction(
            CustomerModel customer,
            ActionResult action,
            ServiceProgressSnapshot progress,
            CustomerPhysicalStateSnapshot physical)
        {
            if (customer == null || action == null) return false;
            ServiceActionClassification classification = ClassifyServiceAction(customer, action, progress, physical);
            return classification.Relation == ServiceRelation.WrongService;
        }

        private ServiceActionClassification ClassifyServiceAction(
            CustomerModel customer,
            ActionResult action,
            ServiceProgressSnapshot progress,
            CustomerPhysicalStateSnapshot physical)
        {
            if (customer == null)
                return new ServiceActionClassification(
                    ServiceRelation.NormalService, false, ServiceActionClassificationReason.None);
            return ServiceActionClassifier.Classify(
                action, _washServiceAdapter.Order(customer), progress, physical);
        }

        private ServiceActionClassification ClassifyBlowDryAction(
            CustomerModel customer,
            ActionResult action,
            ServiceProgressSnapshot progress,
            CustomerPhysicalStateSnapshot physical)
        {
            if (customer != null && customer.OrderRequirementsCompleted && !customer.ExitReady &&
                customer.ExitBlockReason == ExitBlockReason.WetHair)
                return new ServiceActionClassification(ServiceRelation.NormalService, false,
                    ServiceActionClassificationReason.None);
            return ClassifyServiceAction(customer, action, progress, physical);
        }

        public void CancelActiveServiceAction(CustomerModel customer)
        {
            if (customer == null) return;
            ActiveServiceAction action = customer.ActiveServiceAction;
            if (action == ActiveServiceAction.ManualBlow)
            {
                float elapsed = customer.ManualBlowElapsed;
                ActionResult preview = _washServiceAdapter.PreviewTimedActionResult(customer, elapsed);
                ServiceActionClassification classification = ClassifyServiceAction(
                    customer, preview, GetServiceProgressSnapshot(customer), GetServicePhysicalSnapshot(customer));
                _washServiceAdapter.DiscardTimedAction(customer, ClearReason.ToolBecameInvalid);
                var cancelled = new ApplyActionResult(ApplyStatus.InvalidToken, DiagnosticCode.InvalidToken);
                EndManualBlowState(customer, cancelled, classification, elapsed);
                return;
            }
            float activeElapsed = customer.ActiveServiceElapsed;
            bool wasProcessingApply = action == ActiveServiceAction.ApplyDye ||
                action == ActiveServiceAction.ApplyPerm;
            customer.ActiveServiceAction = ActiveServiceAction.None;
            customer.ActiveServiceElapsed = 0f;
            customer.ActiveServiceDuration = 0f;
            EndActiveOperation(customer);
            if (wasProcessingApply) customer.ProcessStage = ServiceProcessStage.None;
            if (action == ActiveServiceAction.Shower || action == ActiveServiceAction.Shampoo)
                _washServiceAdapter.CompleteTimedAction(customer, activeElapsed, true);
            else if (action == ActiveServiceAction.RemoveTowel)
                _washServiceAdapter.DiscardTimedAction(customer, ClearReason.ToolBecameInvalid);
            else if (action == ActiveServiceAction.WrapTowel)
                customer.WashStage = WashStage.ReadyToWrap;
            CustomerChanged?.Invoke(customer);
        }

        private void CompleteTowelWrap(CustomerModel customer)
        {
            customer.ActiveServiceAction = ActiveServiceAction.None;
            customer.ActiveServiceElapsed = customer.ActiveServiceDuration = 0f;
            customer.WashStage = WashStage.Toweled;
            customer.TowelWrapped = true;
            customer.CompletedWashCount++;
            customer.ServicePhase = ServiceStepPhase.Complete;
            if (!customer.IsComplete && customer.CurrentNeed == ServiceType.Wash)
                CompleteCurrentStep(customer, true);
            else
                ApplyExtraService(customer);
        }

        public bool StartManualBlow(CustomerModel customer)
        {
            if (customer == null || !CanPerformBlowDry(customer) ||
                customer.State != CustomerState.Serving || !IsCompatibleStation(ServiceType.Dry, customer.Station) ||
                (customer.BlowStage != BlowStage.AwaitingStart && customer.BlowStage != BlowStage.Early) ||
                customer.AutoBlowRunning) return false;
            float accumulatedElapsed = customer.BlowStage == BlowStage.Early
                ? Math.Max(0f, customer.ManualBlowElapsed) : 0f;
            if (!_washServiceAdapter.BeginTimedAction(customer, ServiceActionType.BlowDry, ServiceTool.BlowDryer, WorldElapsed))
                return false;
            if (!BeginTimedAction(customer, ActiveServiceAction.ManualBlow, ServiceConfig.ManualBlowMinorEnd))
            {
                _washServiceAdapter.CompleteTimedAction(customer, 0f, true);
                return false;
            }
            customer.ManualBlowElapsed = accumulatedElapsed;
            customer.ManualBlowHolding = true;
            customer.BlowStage = BlowStage.ManualHolding;
            customer.LastBlowResult = BlowResult.None;
            customer.BackgroundTask.State = BackgroundTaskState.Inactive;
            CustomerChanged?.Invoke(customer);
            return true;
        }

        public bool TickManualBlow(CustomerModel customer, float dt)
        {
            if (customer == null || !customer.ManualBlowHolding ||
                customer.ActiveServiceAction != ActiveServiceAction.ManualBlow) return false;
            customer.ManualBlowElapsed += Math.Max(0f, dt);
            if (customer.ManualBlowElapsed >= Math.Max(0f, ServiceConfig.ManualBlowMinorEnd))
                customer.BlowStage = BlowStage.Moderate;
            else if (customer.ManualBlowElapsed > Math.Max(ServiceConfig.ManualBlowGoodEnd, ServiceConfig.ManualBlowGoodStart))
                customer.BlowStage = BlowStage.Minor;
            else if (customer.ManualBlowElapsed >= Math.Max(0f, ServiceConfig.ManualBlowGoodStart))
                customer.BlowStage = BlowStage.Good;
            else customer.BlowStage = BlowStage.ManualHolding;
            CustomerChanged?.Invoke(customer);
            return true;
        }

        public BlowResult EndManualBlowHold(CustomerModel customer)
        {
            if (customer == null || !customer.ManualBlowHolding) return BlowResult.None;
            customer.ManualBlowHolding = false;
            float elapsed = customer.ManualBlowElapsed;
            ActionResult preview = _washServiceAdapter.PreviewTimedActionResult(customer, elapsed);
            ServiceActionClassification classification = ClassifyBlowDryAction(
                customer, preview, GetServiceProgressSnapshot(customer), GetServicePhysicalSnapshot(customer));
            bool releasedTooEarly = elapsed < Math.Max(0f, ServiceConfig.ManualBlowGoodStart);
            ApplyActionResult applied;
            if (releasedTooEarly)
            {
                _washServiceAdapter.DiscardTimedAction(customer, ClearReason.ToolBecameInvalid);
                applied = new ApplyActionResult(ApplyStatus.InvalidToken, DiagnosticCode.InvalidToken);
            }
            else
            {
                applied = _washServiceAdapter.CompleteTimedAction(customer, elapsed, false);
            }
            BlowResult result = EndManualBlowState(customer, applied, classification, elapsed);
            if (result != BlowResult.Undone)
            {
                if (!customer.IsComplete && customer.CurrentNeed == ServiceType.Dry)
                    CompleteCurrentStep(customer, true);
                else if (customer.IsComplete)
                    CompleteCurrentStep(customer);
            }
            return result;
        }

        private BlowResult EndManualBlowState(
            CustomerModel customer,
            ApplyActionResult applied,
            ServiceActionClassification classification,
            float elapsed)
        {
            if (customer == null) return BlowResult.None;
            customer.ActiveServiceAction = ActiveServiceAction.None;
            customer.ManualBlowHolding = false;
            customer.ActiveServiceElapsed = 0f;
            customer.ActiveServiceDuration = 0f;
            customer.ServicePhase = ServiceStepPhase.Ready;
            EndActiveOperation(customer);

            BlowResult result = elapsed < Math.Max(0f, ServiceConfig.ManualBlowGoodStart)
                ? BlowResult.Undone
                : elapsed <= Math.Max(ServiceConfig.ManualBlowGoodStart, ServiceConfig.ManualBlowGoodEnd)
                    ? BlowResult.Good
                    : elapsed <= Math.Max(ServiceConfig.ManualBlowGoodEnd, ServiceConfig.ManualBlowMinorEnd)
                        ? BlowResult.Minor
                        : BlowResult.Moderate;
            customer.LastBlowResult = result;
            customer.BlowStage = result == BlowResult.Undone ? BlowStage.Early :
                result == BlowResult.Good ? BlowStage.Good :
                result == BlowResult.Minor ? BlowStage.Minor : BlowStage.Moderate;
            if (result == BlowResult.Undone) return result;
            if (applied.Status != ApplyStatus.Applied || applied.ActionResult == null)
                return BlowResult.None;
            if (result == BlowResult.Minor)
                ApplyAccidentSeverity(customer, AccidentSeverity.Minor);
            if (result == BlowResult.Moderate)
                ApplyAccidentSeverity(customer, AccidentSeverity.Moderate);
            ProjectDomainActionFeedback(customer, applied, classification);
            return result;
        }

        public bool StartBlow(CustomerModel customer) => StartManualBlow(customer);

        public BlowResult FinishBlow(CustomerModel customer) => EndManualBlowHold(customer);

        public void SetFirstDayCompleteForDebug(bool complete)
        {
            FirstDayCompleteForShop = complete;
            AutoBlowStandProduct.CanPurchase = complete && !AutoBlowStandProduct.Purchased;
            AutoBlowStandProduct.LockReason = complete ? string.Empty : "完成首日营业后开放";
        }

        public bool PurchaseAutoBlowStand()
        {
            if (AutoBlowStandProduct.Purchased) return true;
            if (!FirstDayCompleteForShop)
            {
                AutoBlowStandProduct.CanPurchase = false;
                AutoBlowStandProduct.LockReason = "完成首日营业后开放";
                return false;
            }
            if (!Payments.TrySpend(AutoBlowStandProduct.Price))
            {
                AutoBlowStandProduct.LockReason = "金币不足";
                return false;
            }
            AutoBlowStandProduct.Purchased = true;
            AutoBlowStandProduct.CanPurchase = false;
            AutoBlowStandProduct.LockReason = string.Empty;
            _managementTransactions.Add(new ManagementTransaction
            {
                Type = ManagementInvestmentType.Equipment,
                ItemId = AutoBlowStandProduct.Id,
                Amount = AutoBlowStandProduct.Price
            });
            return true;
        }

        public bool PayCompensation(DayStats stats, int amount)
        {
            int expense = Math.Max(0, amount);
            if (!Payments.TrySpend(expense)) return false;
            stats?.RecordCompensation(expense);
            return true;
        }

        public void ForceCloseRemainingCustomers(DayStats stats)
        {
            for (int i = Customers.Count - 1; i >= 0; i--)
            {
                CustomerModel customer = Customers[i];
                if (customer.State == CustomerState.Exited) continue;
                if (customer.State == CustomerState.Finished ||
                    (customer.OrderRequirementsCompleted && !customer.HasBlockingPhysicalState))
                {
                    stats?.RecordCustomerSnapshot(customer);
                }
                else if (customer.HasServiceEngaged || customer.Step > 0 ||
                         customer.ActiveServiceAction != ActiveServiceAction.None)
                {
                    customer.ServiceResult = CustomerServiceResult.SevereUnhappyCompletion;
                    customer.ServiceFeedback = CustomerServiceFeedback.Dissatisfied;
                    customer.Emotion = CustomerEmotion.Angry;
                    stats?.RecordIncompleteAtClose();
                }
                else
                {
                    customer.ServiceResult = CustomerServiceResult.Failed;
                    customer.ServiceFeedback = CustomerServiceFeedback.Dissatisfied;
                    customer.Emotion = CustomerEmotion.Angry;
                    stats?.RecordUnservedAtClose();
                }
                ReleaseWorkstation(customer);
                customer.State = CustomerState.Exited;
                stats?.RecordCustomerSnapshot(customer);
                CustomerChanged?.Invoke(customer);
            }
            Customers.Clear();
            _waitingQueue.Clear();
            ClearAllPlayerFocus();
        }

        public void ResetForNextDay()
        {
            ForceCloseRemainingCustomers(null);
            Payments.ClearDayDrops();
            _issuedCustomerIds.Clear();
            _activeHaircutConfigs.Clear();
            _activeHaircutTools.Clear();
            _washServiceAdapter.ResetForNextDay();
            RemainingTime = Math.Max(0f, FlowConfig.BusinessDuration);
            WorldElapsed = 0f;
            Served = 0;
            AngryLeaves = 0;
            Mistakes = 0;
            IsRunning = true;
        }

        /// <summary>
        /// Restores the persistent economy and permanent shop flags at initialization or
        /// a business-day boundary. Live customers and an in-flight day are rejected so
        /// loading cannot erase active service state or payment pickup state.
        /// </summary>
        public void RestorePersistentState(int balance, bool purchased, bool firstDayComplete)
        {
            if (balance < 0) throw new ArgumentOutOfRangeException(nameof(balance));
            bool atInitialization = WorldElapsed <= 0f && Served == 0 && AngryLeaves == 0 && Mistakes == 0;
            bool atBusinessBoundary = RemainingTime <= 0f && Customers.Count == 0 && _waitingQueue.Count == 0;
            if (!atInitialization && !atBusinessBoundary)
                throw new InvalidOperationException(
                    "Persistent state can only be restored at initialization or a business-day boundary.");
            if (Customers.Count > 0 || _waitingQueue.Count > 0 || PlayerBusy)
                throw new InvalidOperationException(
                    "Persistent state cannot be restored while a customer or operation is active.");

            Payments.RestoreBalance(balance);
            AutoBlowStandProduct.Purchased = purchased;
            FirstDayCompleteForShop = firstDayComplete;
            AutoBlowStandProduct.CanPurchase = firstDayComplete && !purchased;
            AutoBlowStandProduct.LockReason = firstDayComplete
                ? string.Empty : "完成首日营业后开放";
        }

        private AutoBlowTiming GetAutoBlowTiming()
        {
            float startup = Math.Max(.5f, Math.Min(1f, ServiceConfig.AutoBlowStartDuration));
            float goodStart = Math.Max(0f, ServiceConfig.ManualBlowGoodStart);
            float goodEnd = Math.Max(goodStart, ServiceConfig.ManualBlowGoodEnd);
            float minorEnd = Math.Max(goodEnd, ServiceConfig.ManualBlowMinorEnd);
            float safetyStop = Math.Max(goodEnd + .5f, ServiceConfig.AutoBlowSafetyStopTime);

            if (HasAutoBlowStand)
            {
                // The purchased stand improves setup and gives a wider late-recovery
                // window; its safety stop remains a hard ceiling so unattended customers
                // still need the player to return and finish.
                startup = Math.Max(.25f, startup * .65f);
                goodStart = Math.Max(0f, goodStart - .5f);
                goodEnd = Math.Max(goodStart, goodEnd + .75f);
                minorEnd = Math.Max(goodEnd, minorEnd + 1f);
            }

            safetyStop = Math.Max(goodEnd + .5f, safetyStop);
            return new AutoBlowTiming
            {
                Startup = startup,
                GoodStart = goodStart,
                GoodEnd = goodEnd,
                MinorEnd = minorEnd,
                SafetyStop = safetyStop
            };
        }

        public bool StartAutoBlow(CustomerModel customer)
        {
            if (!AutoBlowAvailable || customer == null || !CanPerformBlowDry(customer) ||
                customer.TowelWrapped || customer.State != CustomerState.Serving ||
                !IsCompatibleStation(ServiceType.Dry, customer.Station) ||
                customer.BlowStage != BlowStage.AwaitingStart) return false;
            if (!_washServiceAdapter.BeginBackgroundTimedAction(
                    customer, ServiceActionType.BlowDry, ServiceTool.BlowDryer, WorldElapsed))
                return false;
            EngageService(customer);
            customer.AutoBlowRunning = true;
            customer.AutoBlowSafetyStopped = false;
            customer.BlowStage = BlowStage.AutoRunning;
            customer.ServicePhase = ServiceStepPhase.BackgroundRunning;
            customer.LastBlowResult = BlowResult.None;
            AutoBlowTiming timing = GetAutoBlowTiming();
            customer.BackgroundTask.Start(timing.Startup + timing.GoodStart,
                timing.Startup + timing.GoodEnd,
                Math.Max(timing.Startup + timing.MinorEnd, timing.SafetyStop), WorldElapsed);
            SetWorkstationState(customer, WorkstationState.InService);
            CustomerChanged?.Invoke(customer);
            return true;
        }

        public BlowResult FinishAutoBlow(CustomerModel customer)
        {
            if (customer == null || (!customer.AutoBlowRunning && !customer.AutoBlowSafetyStopped))
                return BlowResult.None;
            float elapsed = customer.BackgroundTask.Elapsed;
            AutoBlowTiming timing = GetAutoBlowTiming();
            if (!customer.AutoBlowSafetyStopped && elapsed < timing.Startup + timing.GoodStart)
                return BlowResult.Undone;
            BlowResult result = customer.AutoBlowSafetyStopped || elapsed >= timing.SafetyStop
                ? BlowResult.Moderate : elapsed > timing.Startup + timing.GoodEnd
                    ? BlowResult.Minor : BlowResult.Good;
            ActionResult preview = _washServiceAdapter.PreviewTimedActionResult(customer, elapsed);
            ServiceActionClassification classification = ClassifyBlowDryAction(
                customer, preview, GetServiceProgressSnapshot(customer), GetServicePhysicalSnapshot(customer));
            ApplyActionResult applied = _washServiceAdapter.CompleteTimedAction(customer, elapsed, false);
            if (applied.Status != ApplyStatus.Applied || applied.ActionResult == null)
                return BlowResult.None;
            customer.AutoBlowRunning = false;
            customer.BackgroundTask.State = BackgroundTaskState.Complete;
            customer.LastBlowResult = result;
            customer.BlowStage = BlowStage.Complete;
            if (result == BlowResult.Minor) ApplyAccidentSeverity(customer, AccidentSeverity.Minor);
            else if (result == BlowResult.Moderate) ApplyAccidentSeverity(customer, AccidentSeverity.Moderate);
            ProjectDomainActionFeedback(customer, applied, classification);
            if (!customer.IsComplete && customer.CurrentNeed == ServiceType.Dry)
                CompleteCurrentStep(customer, true);
            else if (customer.IsComplete)
                CompleteCurrentStep(customer);
            return result;
        }

        public bool ApplyHaircutResult(CustomerModel customer, HaircutResult result, HaircutConfig config)
        {
            SalonTool tool = customer != null && customer.HaircutService != null
                ? customer.HaircutService.CurrentRequiredTool
                : SalonTool.Scissors;
            return ApplyHaircutResult(customer, tool, result, config);
        }

        public bool ApplyHaircutResult(CustomerModel customer, SalonTool selectedTool, HaircutResult result, HaircutConfig config)
        {
            if (customer == null || config == null || result == HaircutResult.None) return false;
            float elapsed = result == HaircutResult.Undercut
                ? Math.Max(0f, config.GetPerfectMin(selectedTool) * .5f)
                : result == HaircutResult.Overcut
                    ? config.GetPerfectMax(selectedTool) + .01f
                    : (config.GetPerfectMin(selectedTool) + config.GetPerfectMax(selectedTool)) * .5f;
            bool operationWasActive = customer.AttentionState == CustomerAttentionState.ActiveOperation;
            if (!BeginHaircutAction(customer, selectedTool, config)) return false;
            HaircutResult resolved = CompleteHaircutAction(customer, elapsed, false);
            if (!operationWasActive) EndActiveOperation(customer);
            return resolved != HaircutResult.None;
        }

        /// <summary>
        /// 一次性剪发（点按式）按住时长由模型/配置决定，View 只读取结果。
        /// 沿用既有口径：落在完美区间下沿，因此点按式剪发仍是稳定完成而非过剪。
        /// </summary>
        public float HaircutHoldDurationFor(SalonTool tool, HaircutConfig config)
        {
            return config == null ? 0f : Math.Max(0f, config.GetPerfectMin(tool)) + .05f;
        }

        public bool BeginHaircutAction(CustomerModel customer, SalonTool selectedTool, HaircutConfig config)
        {
            if (customer == null || config == null || !HaircutConfig.IsHaircutTool(selectedTool)
                || customer.State != CustomerState.Serving || customer.Station < 0
                || customer.Station >= Workstations.Count
                || Workstations[customer.Station].Type != WorkstationType.Haircut)
                return false;
            if (!customer.Needs.Contains(ServiceType.Cut))
            {
                customer.ReactionKind = CustomerReactionKind.Resistance;
                customer.ReactionRemaining = Math.Max(customer.ReactionRemaining, 1.2f);
                CustomerChanged?.Invoke(customer);
            }
            if (!_washServiceAdapter.BeginHaircutAction(customer, selectedTool, config, WorldElapsed))
                return false;
            if (!BeginActiveOperation(customer, true))
            {
                _washServiceAdapter.DiscardTimedAction(customer, ClearReason.ToolBecameInvalid);
                return false;
            }
            _activeHaircutConfigs[customer.Id] = config;
            _activeHaircutTools[customer.Id] = selectedTool;
            return true;
        }

        public HaircutResult CompleteHaircutAction(CustomerModel customer, float elapsedTime, bool interrupted)
        {
            if (customer == null || !_activeHaircutConfigs.TryGetValue(customer.Id, out HaircutConfig config)
                || !_activeHaircutTools.TryGetValue(customer.Id, out SalonTool selectedTool))
                return HaircutResult.None;
            ActionResult preview = _washServiceAdapter.PreviewHaircutResult(
                customer, Math.Max(0f, elapsedTime));
            ServiceActionClassification classification = ClassifyServiceAction(
                customer, preview, GetServiceProgressSnapshot(customer), GetServicePhysicalSnapshot(customer));
            HaircutActionApplyResult domain = _washServiceAdapter.CompleteHaircutAction(
                customer, Math.Max(0f, elapsedTime), interrupted);
            _activeHaircutConfigs.Remove(customer.Id);
            _activeHaircutTools.Remove(customer.Id);
            if (domain.ApplyResult.Status != ApplyStatus.Applied || domain.ActionResult == null)
                return HaircutResult.None;

            HaircutResult result = ToLegacyHaircutResult(domain.ActionResult);
            ProjectDomainActionFeedback(customer, domain.ApplyResult, classification);
            ProjectHaircutOutcome(customer, selectedTool, result, config, domain.ActionResult);
            return result;
        }

        private static HaircutResult ToLegacyHaircutResult(ActionResult result)
        {
            if (result.ExecutionStatus == ExecutionStatus.Resisted) return HaircutResult.Resisted;
            if (result.ExecutionQuality == ExecutionQuality.Under) return HaircutResult.Undercut;
            if (result.ExecutionQuality == ExecutionQuality.Perfect) return HaircutResult.Perfect;
            if (result.ExecutionQuality == ExecutionQuality.Over) return HaircutResult.Overcut;
            return result.ExecutionStatus == ExecutionStatus.Executed
                ? HaircutResult.WrongTool : HaircutResult.None;
        }

        private void ProjectHaircutOutcome(
            CustomerModel customer,
            SalonTool selectedTool,
            HaircutResult result,
            HaircutConfig config,
            ActionResult domainResult)
        {
            if (domainResult != null && domainResult.OrderEffect == OrderEffect.ExtraService)
            {
                if (result == HaircutResult.Perfect) customer.HairStage = HairStage.Complete;
                else if (result == HaircutResult.Undercut) customer.HairStage = HairStage.Trimmed;
                else if (result == HaircutResult.Overcut) customer.HairStage = HairStage.Overcut;
                customer.ServicePhase = ServiceStepPhase.Ready;
                SetWorkstationState(customer, WorkstationState.AwaitingService);
                return;
            }

            bool fulfillsRequirement = _washServiceAdapter.IsOutstandingRequiredCutMilestone(customer, selectedTool);
            if (result == HaircutResult.Resisted)
            {
                SetWorkstationState(customer, WorkstationState.AwaitingService);
                CustomerChanged?.Invoke(customer);
                return;
            }
            if (customer.HaircutService == null
                || (!fulfillsRequirement && customer.HaircutService.State != HaircutServiceState.Active))
            {
                customer.HaircutService = new HaircutServiceModel(
                    fulfillsRequirement ? SalonTool.Scissors : selectedTool);
                if (!fulfillsRequirement) customer.HairStage = HairStage.Original;
            }

            bool towelDamaged = domainResult.PhysicalStateDelta.TowelCondition == TowelCondition.Damaged;
            bool wrongTool = selectedTool != customer.HaircutService.CurrentRequiredTool
                || result == HaircutResult.WrongTool;
            if (towelDamaged)
            {
                customer.ServiceFeedback = CustomerServiceFeedback.Dissatisfied;
                customer.ReactionRemaining = Math.Max(0f, config.CustomerReactionSeconds);
                SetWorkstationState(customer, WorkstationState.Rework);
                CustomerChanged?.Invoke(customer);
                return;
            }
            if (wrongTool)
            {
                SetWorkstationState(customer, WorkstationState.Rework);
                CustomerChanged?.Invoke(customer);
                return;
            }
            if (!customer.HaircutService.ApplyAttempt(selectedTool, result)) return;

            if (result == HaircutResult.Undercut)
            {
                customer.HairStage = HairStage.Trimmed;
                customer.Emotion = CustomerEmotion.Impatient;
                customer.ReactionRemaining = Math.Max(0f, config.CustomerReactionSeconds);
                SetWorkstationState(customer, WorkstationState.Rework);
                CustomerChanged?.Invoke(customer);
                return;
            }

            if (result == HaircutResult.Perfect)
            {
                if (customer.HaircutService.State == HaircutServiceState.Active)
                {
                    customer.HairStage = HairStage.Trimmed;
                    customer.Emotion = CustomerEmotion.Calm;
                    customer.ReactionRemaining = 0f;
                    CustomerChanged?.Invoke(customer);
                    return;
                }

                customer.HairStage = HairStage.Complete;
                customer.LastHaircutRating = customer.HaircutService.Rating;
                if (!customer.IsComplete && customer.CurrentNeed == ServiceType.Cut)
                {
                    CompleteCurrentStep(customer, true,
                        domainResult.MistakeSeverity == MistakeSeverity.None);
                }
                else
                {
                    customer.ServicePhase = ServiceStepPhase.Ready;
                    SetWorkstationState(customer, WorkstationState.AwaitingService);
                }
                CustomerChanged?.Invoke(customer);
                return;
            }

            if (result != HaircutResult.Overcut) return;
            customer.HairStage = HairStage.Overcut;
            ApplyAccidentSeverity(customer, AccidentSeverity.Major);
            customer.Emotion = CustomerEmotion.Angry;
            customer.ReactionRemaining = Math.Max(0f, config.CustomerReactionSeconds);
            Mistakes++;
            HaircutOvercut?.Invoke(customer);
            customer.LastHaircutRating = HaircutServiceRating.Failed;
            customer.ServiceFeedback = CustomerServiceFeedback.Dissatisfied;
            customer.ServiceResult = CustomerServiceResult.Failed;
            SetWorkstationState(customer, WorkstationState.Completed);
            ChangeState(customer, CustomerState.Finished);
        }

        private void ProjectDomainActionFeedback(
            CustomerModel customer,
            ApplyActionResult applied,
            ServiceActionClassification classification)
        {
            if (customer == null || applied == null || applied.Status != ApplyStatus.Applied ||
                applied.ActionResult == null || classification == null) return;

            ActionResult result = applied.ActionResult;
            customer.WrongServiceKind = CustomerWrongServiceKind.None;

            if (result.ExecutionStatus == ExecutionStatus.Resisted)
            {
                customer.ReactionKind = CustomerReactionKind.Resistance;
                customer.Emotion = CustomerEmotion.Impatient;
                customer.ReactionRemaining = Math.Max(customer.ReactionRemaining, 1.2f);
                customer.WrongServiceKind = CustomerWrongServiceKind.None;
                CustomerChanged?.Invoke(customer);
                return;
            }

            if (classification.Relation == ServiceRelation.ExtraService ||
                classification.Relation == ServiceRelation.WrongService)
            {
                if (classification.Relation == ServiceRelation.WrongService)
                {
                    customer.WrongServiceKind = WrongServiceKindFor(result.ActionToken.ActionType);
                }
                customer.ExtraServiceCount++;
                Mistakes++;

                customer.ReactionKind = customer.WrongServiceKind == CustomerWrongServiceKind.Haircut
                    ? CustomerReactionKind.Angry : CustomerReactionKind.Protest;
                customer.Emotion = customer.WrongServiceKind == CustomerWrongServiceKind.Haircut
                    ? CustomerEmotion.Angry : CustomerEmotion.Impatient;
                customer.ReactionRemaining = Math.Max(customer.ReactionRemaining,
                    customer.WrongServiceKind == CustomerWrongServiceKind.Haircut ? 2f : 1.4f);
            }
            else if (classification.IsDisasterCandidate)
            {
                customer.ReactionKind = CustomerReactionKind.Protest;
                customer.Emotion = CustomerEmotion.Impatient;
                customer.ReactionRemaining = Math.Max(customer.ReactionRemaining, 1.4f);
            }

            if (result.MistakeSeverity == MistakeSeverity.Major)
            {
                customer.ReactionKind = CustomerReactionKind.Angry;
                customer.Emotion = CustomerEmotion.Angry;
                customer.ReactionRemaining = Math.Max(customer.ReactionRemaining, 2.2f);
            }
            CustomerChanged?.Invoke(customer);
        }

        private static CustomerWrongServiceKind WrongServiceKindFor(ServiceActionType action)
        {
            if (action == ServiceActionType.Haircut) return CustomerWrongServiceKind.Haircut;
            if (action == ServiceActionType.BlowDry) return CustomerWrongServiceKind.Dry;
            if (action == ServiceActionType.Shower || action == ServiceActionType.Shampoo)
                return CustomerWrongServiceKind.Wash;
            return CustomerWrongServiceKind.None;
        }

        private bool IsHappyCompletion(CustomerModel customer, HaircutServiceRating rating)
        {
            return rating == HaircutServiceRating.Perfect &&
                   customer.Satisfaction >= Math.Max(0f, ExperienceProfile.HappyThreshold) &&
                   customer.AccidentSeverity == AccidentSeverity.None &&
                   customer.WrongStationCount == 0 &&
                   !customer.HadServiceDelay &&
                   customer.ServiceFeedback != CustomerServiceFeedback.Dissatisfied &&
                   !customer.WasImpatientBeforeService &&
                    customer.TotalWaitSeconds <= Math.Max(0f, Payments.Config.HappyMaxWaitSeconds) &&
                    customer.ServiceElapsed <= Math.Max(0f, Payments.Config.HappyMaxServiceSeconds);
        }

        private bool CanPerformBlowDry(CustomerModel customer)
        {
            if (customer == null || customer.TowelWrapped)
                return false;

            ServiceProgressSnapshot progress = _washServiceAdapter.Progress(customer);
            bool requiresDry = customer.Needs.Contains(ServiceType.Dry);
            bool dryCompleted = requiresDry && progress[MilestoneId.HairDry].EverCompleted &&
                progress[MilestoneId.HairDry].SatisfiedNow;
            bool hasCutProgress = CustomerHasCutProgress(customer, progress);

            if (requiresDry)
                return !dryCompleted;

            if (!customer.HairWet) return false;

            if (customer.Needs.Contains(ServiceType.Cut))
                return hasCutProgress || customer.OrderRequirementsCompleted;

            return customer.OrderRequirementsCompleted;
        }

        private static bool CustomerHasCutProgress(CustomerModel customer, ServiceProgressSnapshot progress)
        {
            if (customer == null || customer.Needs == null || progress == null)
                return false;

            if (!customer.Needs.Contains(ServiceType.Cut))
                return false;

            return progress[MilestoneId.ScissorsCompleted].EverCompleted
                   || progress[MilestoneId.ClippersCompleted].EverCompleted
                   || progress[MilestoneId.ThinningShearsCompleted].EverCompleted;
        }

        private bool CanUseWashStation(CustomerModel customer)
        {
            return customer != null && customer.State == CustomerState.Serving && customer.Station >= 0 &&
                   customer.Station < Workstations.Count && Workstations[customer.Station].Type == WorkstationType.Wash &&
                   customer.ActiveServiceAction == ActiveServiceAction.None;
        }

        private bool BeginTimedAction(CustomerModel customer, ActiveServiceAction action, float duration)
        {
            if (customer == null || customer.ActiveServiceAction != ActiveServiceAction.None) return false;
            customer.ActiveServiceAction = action;
            customer.ActiveServiceElapsed = 0f;
            customer.ActiveServiceDuration = Math.Max(0f, duration);
            customer.ServicePhase = ServiceStepPhase.Active;
            EngageService(customer);
            customer.AttentionState = CustomerAttentionState.ActiveOperation;
            SetWorkstationState(customer, WorkstationState.InService);
            CustomerChanged?.Invoke(customer);
            return true;
        }

        private void EngageService(CustomerModel customer)
        {
            if (customer == null || customer.HasServiceEngaged) return;
            customer.HasServiceEngaged = true;
            customer.AttentionState = CustomerAttentionState.ServiceEngaged;
            if (customer.Emotion != CustomerEmotion.Calm)
                customer.WasImpatientBeforeService = true;
            customer.Patience = Math.Min(Math.Max(0f, ExperienceProfile.MaxPatience),
                customer.Patience + Math.Max(0f, PatienceConfig.ServiceStartRelief));
            CustomerEmotion relieved = customer.Emotion == CustomerEmotion.Angry
                ? CustomerEmotion.Impatient
                : CustomerEmotion.Calm;
            if (relieved == customer.Emotion) return;
            customer.Emotion = relieved;
            CustomerChanged?.Invoke(customer);
        }

        private void PrepareCurrentStep(CustomerModel customer)
        {
            if (customer == null || customer.IsComplete) return;
            customer.ServicePhase = ServiceStepPhase.Ready;
            customer.ActiveServiceAction = ActiveServiceAction.None;
            customer.ActiveServiceElapsed = 0f;
            customer.ActiveServiceDuration = 0f;
            customer.BackgroundTask.State = BackgroundTaskState.Inactive;
            customer.BackgroundTask.RiskState = BackgroundRiskState.None;
            if (customer.CurrentNeed == ServiceType.Wash)
                customer.WashStage = WashStage.Dry;
            else if (customer.CurrentNeed == ServiceType.Dry)
            {
                customer.BlowStage = BlowStage.AwaitingStart;
                customer.LastBlowResult = BlowResult.None;
            }
        }

        private void UpdateServiceStage(CustomerModel customer, float dt)
        {
            if (customer == null) return;
            if (customer.WashStage == WashStage.FoamWait ||
                customer.WashStage == WashStage.ReadyToRinse)
            {
                if (customer.WashStage == WashStage.FoamWait &&
                    customer.BackgroundTask.Elapsed >= Math.Max(0f, ServiceConfig.FoamOptimalStart))
                {
                    customer.WashStage = WashStage.ReadyToRinse;
                    customer.ServicePhase = ServiceStepPhase.Ready;
                    customer.StateElapsed = 0f;
                    SetWorkstationState(customer, WorkstationState.AwaitingService);
                    CustomerChanged?.Invoke(customer);
                }
                if (customer.WashStage == WashStage.ReadyToRinse)
                {
                    float elapsed = customer.BackgroundTask.Elapsed;
                    if (elapsed >= Math.Max(ServiceConfig.FoamMinorLateThreshold, ServiceConfig.FoamModerateLateThreshold))
                    {
                        ApplyAccidentSeverity(customer, AccidentSeverity.Moderate);
                        customer.ServicePhase = ServiceStepPhase.Late;
                    }
                    else if (elapsed >= ServiceConfig.FoamMinorLateThreshold)
                    {
                        ApplyAccidentSeverity(customer, AccidentSeverity.Minor);
                        customer.ServicePhase = ServiceStepPhase.Late;
                    }
                }
            }
            else if (customer.CurrentNeed == ServiceType.Dry && customer.AutoBlowRunning &&
                     customer.BackgroundTask.State == BackgroundTaskState.Running)
            {
                float elapsed = customer.BackgroundTask.Elapsed;
                AutoBlowTiming timing = GetAutoBlowTiming();
                if (elapsed < timing.Startup + timing.GoodStart)
                    customer.BlowStage = BlowStage.Early;
                else if (elapsed <= timing.Startup + timing.GoodEnd)
                    customer.BlowStage = BlowStage.Good;
                else if (elapsed < Math.Max(timing.Startup + timing.MinorEnd, timing.SafetyStop))
                    customer.BlowStage = BlowStage.Minor;
                else
                {
                    customer.AutoBlowRunning = false;
                    customer.AutoBlowSafetyStopped = true;
                    customer.BlowStage = BlowStage.SafetyStopped;
                    customer.BackgroundTask.State = BackgroundTaskState.Complete;
                    customer.ServicePhase = ServiceStepPhase.Late;
                    customer.HadServiceDelay = true;
                    ApplyAccidentSeverity(customer, AccidentSeverity.Moderate);
                    SetWorkstationState(customer, WorkstationState.InService);
                    CustomerChanged?.Invoke(customer);
                }
                customer.ServicePhase = customer.BlowStage == BlowStage.Minor || customer.BlowStage == BlowStage.SafetyStopped
                    ? ServiceStepPhase.Late
                    : ServiceStepPhase.BackgroundRunning;
            }
        }

        private void AddServiceDelay(CustomerModel customer, float dt)
        {
            if (customer == null) return;
            customer.ServiceDelaySeconds += Math.Max(0f, dt);
            if (customer.ServiceDelaySeconds > Math.Max(0f, ServiceConfig.ReadyDelayGrace))
                customer.HadServiceDelay = true;
        }

        private void CompleteCurrentStep(
            CustomerModel customer,
            bool advanceRequirement = false,
            bool rewardPositiveService = true)
        {
            if (customer == null) return;
            int completedAtStation = customer.Station;
            if (!advanceRequirement && (customer.OrderRequirementsCompleted || customer.IsComplete))
            {
                if (!TryFinalizeCompletedOrder(customer, completedAtStation))
                {
                    customer.StateElapsed = 0f;
                    customer.AttentionState = CustomerAttentionState.ServiceEngaged;
                    SetWorkstationState(customer, WorkstationState.AwaitingTransfer);
                    CustomerChanged?.Invoke(customer);
                }
                return;
            }
            if (customer.IsComplete) return;
            ClearInteractionForCustomer(customer.Id);
            WorkstationType completedStationType = completedAtStation >= 0 && completedAtStation < Workstations.Count
                ? Workstations[completedAtStation].Type
                : WorkstationType.Haircut;
            customer.Step++;
            if (rewardPositiveService)
                ApplyPositiveService(customer, ExperienceProfile.PositiveServiceGain);
            customer.ServicePhase = ServiceStepPhase.Complete;

            if (customer.OrderRequirementsCompleted || customer.IsComplete)
            {
                if (!TryFinalizeCompletedOrder(customer, completedAtStation))
                {
                    customer.StateElapsed = 0f;
                    customer.AttentionState = CustomerAttentionState.ServiceEngaged;
                    SetWorkstationState(customer, WorkstationState.AwaitingTransfer);
                    CustomerChanged?.Invoke(customer);
                }
                return;
            }

            if (IsCompatibleStation(customer.CurrentNeed, completedStationType))
            {
                PrepareCurrentStep(customer);
                if (completedAtStation >= 0 && completedAtStation < Workstations.Count)
                    Workstations[completedAtStation].CurrentService = customer.CurrentNeed;
                SetWorkstationState(customer, WorkstationState.AwaitingService);
                CustomerChanged?.Invoke(customer);
                return;
            }

            PrepareCurrentStep(customer);
            customer.StateElapsed = 0f;
            customer.AttentionState = CustomerAttentionState.ServiceEngaged;
            SetWorkstationState(customer, WorkstationState.AwaitingTransfer);
            CustomerChanged?.Invoke(customer);
        }

        private void ApplyExtraService(CustomerModel customer)
        {
            if (customer == null) return;
            customer.ExtraServiceCount++;
            customer.HadServiceDelay = true;
            customer.ServiceFeedback = CustomerServiceFeedback.Dissatisfied;
            Mistakes++;
            ApplySatisfactionLoss(customer, 8f * Math.Max(0f, ExperienceProfile.MistakeSensitivity));
            SetWorkstationState(customer, WorkstationState.AwaitingService);
            CustomerChanged?.Invoke(customer);
        }

        private void ClearInteractionForCustomer(int customerId)
        {
            foreach (KeyValuePair<int, PlayerContext> pair in _playerContexts)
                if (pair.Value.SelectedCustomerId == customerId ||
                    pair.Value.SelectedToolOwnerCustomerId == customerId)
                {
                    pair.Value.ClearInteractionContext();
                    PlayerContextChanged?.Invoke(pair.Value);
                }
        }

        private bool TryFinalizeCompletedOrder(CustomerModel customer, int completedAtStation)
        {
            if (customer == null || (!customer.OrderRequirementsCompleted && !customer.IsComplete)) return false;
            ServiceExitReadinessResult readiness = _washServiceAdapter.ExitReadiness(customer);
            bool physicalExitReady = readiness.PhysicalExitReady;
            // 4A.5.1: current phase allows wetness to remain as quality debt,
            // but must not hard-block closure. Other blockers still block completion.
            if (!physicalExitReady && IsWetnessOnlyBlockingConstraint(readiness) &&
                !customer.Needs.Contains(ServiceType.Dry))
                physicalExitReady = true;
            if (!physicalExitReady) return false;

            HaircutServiceRating rating = customer.LastHaircutRating == HaircutServiceRating.None
                ? HaircutServiceRating.Perfect
                : customer.LastHaircutRating;
            if (customer.HadServiceDelay || customer.AccidentSeverity != AccidentSeverity.None ||
                customer.WrongStationCount > 0)
                rating = HaircutServiceRating.Recovered;
            bool happyCompletion = IsHappyCompletion(customer, rating);
            bool severeUnhappy = customer.Satisfaction < Math.Max(0f, ExperienceProfile.SevereUnhappyThreshold);
            bool unhappy = !severeUnhappy && customer.Satisfaction < Math.Max(0f, ExperienceProfile.UnhappyThreshold);
            customer.ServiceResult = happyCompletion ? CustomerServiceResult.HappyCompletion :
                severeUnhappy ? CustomerServiceResult.SevereUnhappyCompletion :
                unhappy ? CustomerServiceResult.UnhappyCompletion : CustomerServiceResult.NormalCompletion;
            if (unhappy || severeUnhappy)
            {
                customer.Emotion = CustomerEmotion.Angry;
                customer.ServiceFeedback = CustomerServiceFeedback.Dissatisfied;
            }
            else if (customer.ServiceFeedback != CustomerServiceFeedback.Dissatisfied)
            {
                customer.Emotion = happyCompletion ? CustomerEmotion.Happy : CustomerEmotion.Calm;
                customer.ServiceFeedback = CustomerServiceFeedback.Satisfied;
            }
            Served++;
            CreatePayment(customer, completedAtStation, rating, happyCompletion);
            SetWorkstationState(customer, WorkstationState.Completed);
            ChangeState(customer, CustomerState.Finished);
            CustomerChanged?.Invoke(customer);
            return true;
        }

        private static bool IsWetnessOnlyBlockingConstraint(ServiceExitReadinessResult readiness)
        {
            if (readiness == null) return false;
            foreach (ExitConstraint blocker in readiness.BlockingConstraints)
            {
                if (blocker != ExitConstraint.DryEnough)
                    return false;
            }
            return true;
        }

        private void DrainPatience(CustomerModel customer, float step)
        {
            CustomerEmotion previousEmotion = customer.Emotion;
            float before = customer.Patience;
            customer.Patience = Math.Max(0f, before - step * Math.Max(0f, PatienceConfig.DrainPerSecond) *
                Math.Max(0f, ExperienceProfile.PatienceDecayMultiplier));
            float lost = before - customer.Patience;
            if (lost > 0f)
            {
                float satisfactionLoss = lost * Math.Max(0f, ExperienceProfile.WaitingToSatisfactionRate);
                customer.WaitingSatisfactionLoss += satisfactionLoss;
            }
            if (customer.ReactionRemaining <= 0f)
            {
                float angryAt = Math.Max(0f, PatienceConfig.AngryAtPatience);
                float impatientAt = Math.Max(angryAt, PatienceConfig.ImpatientAtPatience);
                customer.Emotion = customer.Patience > impatientAt ? CustomerEmotion.Calm :
                    customer.Patience > angryAt ? CustomerEmotion.Impatient : CustomerEmotion.Angry;
            }
            if (customer.Emotion != previousEmotion)
                CustomerChanged?.Invoke(customer);
            if (customer.State != CustomerState.Serving && customer.Emotion != CustomerEmotion.Calm)
                customer.WasImpatientBeforeService = true;
            if (customer.Patience <= 0f && !IsUninterruptibleOperation(customer))
                ProcessPatienceLeave(customer);
        }

        private float PatienceDrainMultiplier(CustomerModel customer)
        {
            if (customer == null || customer.State == CustomerState.Entering || customer.State == CustomerState.Waiting)
                return 1f;
            if (customer.State != CustomerState.Serving) return 0f;
            if (IsUninterruptibleOperation(customer))
                return 0f;
            if (customer.Station >= 0 && customer.Station < Workstations.Count &&
                !IsCompatibleStation(customer.CurrentNeed, Workstations[customer.Station].Type)) return 1f;
            if (IsAwaitingTransfer(customer) || customer.HasServiceEngaged ||
                (customer.WashStage == WashStage.ReadyToRinse &&
                 customer.BackgroundTask.Elapsed >= ServiceConfig.FoamMinorLateThreshold) ||
                customer.AutoBlowSafetyStopped)
            {
                // A short post-service handoff grace keeps the player from being
                // punished while moving back to the customer. Once that grace is
                // spent, an ignored engaged customer steadily loses patience.
                if (customer.ServiceDelaySeconds <= Math.Max(0f, ServiceConfig.ReadyDelayGrace))
                    return 0f;
                return Math.Max(0f, ExperienceProfile.ServiceDelayPatienceMultiplier);
            }
            return 1f;
        }

        private static bool IsUninterruptibleOperation(CustomerModel customer)
        {
            if (customer == null) return false;
            return customer.AttentionState == CustomerAttentionState.ActiveOperation ||
                customer.ActiveServiceAction != ActiveServiceAction.None ||
                customer.ManualBlowHolding || customer.AutoBlowRunning || customer.IsProcessing ||
                (customer.ServiceExecution != null &&
                 customer.ServiceExecution.State == ServiceExecutionState.Executing);
        }

        private void ApplySatisfactionLoss(CustomerModel customer, float amount)
        {
            if (customer == null) return;
            customer.Satisfaction = Math.Max(0f, customer.Satisfaction - Math.Max(0f, amount));
        }

        private void ApplyAccidentSeverity(CustomerModel customer, AccidentSeverity severity)
        {
            if (customer == null || severity <= customer.AccidentSeverity) return;
            AccidentSeverity previous = customer.AccidentSeverity;
            customer.AccidentSeverity = severity;
            float penalty = severity == AccidentSeverity.Minor
                ? ExperienceProfile.MinorPenalty : severity == AccidentSeverity.Moderate
                    ? ExperienceProfile.ModeratePenalty : ExperienceProfile.ModeratePenalty * 2f;
            if (previous == AccidentSeverity.Minor) penalty = Math.Max(0f, penalty - ExperienceProfile.MinorPenalty);
            ApplySatisfactionLoss(customer, penalty * ExperienceProfile.MistakeSensitivity);
            customer.RecoveryCap = severity == AccidentSeverity.Major ? ExperienceProfile.RecoveryCapMajor :
                severity == AccidentSeverity.Moderate ? ExperienceProfile.RecoveryCapModerate : ExperienceProfile.RecoveryCapMinor;
        }

        private void ApplyPositiveService(CustomerModel customer, float amount)
        {
            if (customer == null) return;
            float cap = Math.Min(100f, customer.RecoveryCap);
            customer.Satisfaction = Math.Min(cap, customer.Satisfaction + Math.Max(0f, amount) *
                Math.Max(0f, ExperienceProfile.PositiveServiceMultiplier));
        }

        private void ProcessPatienceLeave(CustomerModel customer)
        {
            if (customer == null || customer.State == CustomerState.Leaving || customer.State == CustomerState.Exited)
                return;
            if (IsUninterruptibleOperation(customer)) return;
            customer.AttentionState = CustomerAttentionState.Neglected;
            _waitingQueue.Remove(customer);
            ReleaseWorkstation(customer);
            customer.Emotion = CustomerEmotion.Angry;
            customer.ServiceFeedback = CustomerServiceFeedback.Dissatisfied;
            customer.ServiceResult = CustomerServiceResult.Failed;
            AngryLeaves++;
            ClearFocusForCustomer(customer.Id);
            ChangeState(customer, CustomerState.Leaving);
        }

        private void CreatePayment(CustomerModel customer, int station, HaircutServiceRating rating, bool happyCompletion)
        {
            if (station < 0) return;
            PaymentDropModel drop = Payments.CreateOrderPayment(
                customer.Id, station, customer.Needs, rating, happyCompletion);
            PaymentCreated?.Invoke(drop);
        }

        private void ReleaseWorkstation(CustomerModel customer)
        {
            if (customer == null) return;
            if (customer.Station >= 0 && customer.Station < Workstations.Count)
            {
                Workstations[customer.Station].CurrentCustomerId = -1;
                Workstations[customer.Station].CurrentService = null;
                Workstations[customer.Station].State = WorkstationState.Available;
            }
            customer.Station = -1;
        }

        private void SetWorkstationState(CustomerModel customer, WorkstationState state)
        {
            if (customer == null || customer.Station < 0 || customer.Station >= Workstations.Count) return;
            WorkstationModel station = Workstations[customer.Station];
            if (station.CurrentCustomerId == customer.Id) station.State = state;
        }

        private void ChangeState(CustomerModel customer, CustomerState state)
        {
            if (customer == null || customer.State == state) return;
            customer.State = state;
            customer.StateElapsed = 0f;
            if (state == CustomerState.Waiting)
            {
                customer.AttentionState = CustomerAttentionState.Unserved;
                if (!_waitingQueue.Contains(customer)) _waitingQueue.Add(customer);
            }
            else if (state == CustomerState.Leaving || state == CustomerState.Exited)
            {
                _waitingQueue.Remove(customer);
                customer.AttentionState = CustomerAttentionState.Neglected;
            }
            CustomerChanged?.Invoke(customer);
        }
    }
}
