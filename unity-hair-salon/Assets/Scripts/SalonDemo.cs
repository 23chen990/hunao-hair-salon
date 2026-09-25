using System.Collections;
using System.Collections.Generic;
using System.IO;
using HairSalon;
using HairSalon.AssetPipeline;
using HairSalon.Character;
using HairSalon.CutStations;
using HairSalon.ServiceArchitecture;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class SalonDemo : MonoBehaviour
{
    [Header("Haircut Long Press MVP")]
    public HaircutConfig HaircutSettings = new HaircutConfig();
    [Header("Phase 4 reward tuning")]
    public SalonRewardConfig RewardSettings = new SalonRewardConfig();
    [Header("Phase 4 patience tuning")]
    public CustomerPatienceConfig PatienceSettings = new CustomerPatienceConfig();
    [Header("Phase 5 concurrent customer flow")]
    public SalonFlowConfig FlowSettings = new SalonFlowConfig();
    [Header("Phase 6 wash, blow and towel timing")]
    public SalonServiceConfig ServiceSettings = new SalonServiceConfig();
    [Header("Phase 7 business day and traffic tuning")]
    public DayConfig DaySettings = DayConfig.CreateDefault();
    [Header("Shop satisfaction HUD tuning")]
    public ShopSatisfactionConfig SatisfactionSettings = new ShopSatisfactionConfig();

    private static readonly Color Coral = Hex("D8665E");
    private static readonly Color CoralLight = Hex("EA8A76");
    private static readonly Color Teal = Hex("3F8F88");
    private static readonly Color TealDark = Hex("25635F");
    private static readonly Color Cream = Hex("F2D7B6");
    private static readonly Color Wood = Hex("8B542F");
    private static readonly Color DarkWood = Hex("543322");
    private static readonly Color Purple = Hex("7C579C");
    private static readonly Color Gold = Hex("F4B63D");
    private static readonly Color Ink = Hex("2D292B");
    private static readonly Color Exterior = Hex("252A38");
    private static readonly Dictionary<string, Sprite> TopHudSprites = new Dictionary<string, Sprite>();
    private static TMP_FontAsset _topHudFont;
    private static readonly ServiceExecutionType[] WashStationServices =
    {
        ServiceExecutionType.Wash
    };
    private static readonly ServiceExecutionType[] HaircutStationServices =
    {
        ServiceExecutionType.BlowDry
    };
    private static readonly SalonTool[] HaircutStationTools =
    {
        SalonTool.Scissors,
        SalonTool.ThinningShears,
        SalonTool.Clippers,
        SalonTool.BlowDryer,
        SalonTool.DyeBottle
    };

    private readonly List<SalonCustomerView> _customerViews = new List<SalonCustomerView>();
    private readonly Dictionary<int, Transform> _customerSeatAnchors = new Dictionary<int, Transform>();
    private readonly Dictionary<int, Transform> _playerServiceAnchors = new Dictionary<int, Transform>();
    private readonly Dictionary<int, Transform> _stationQueueAnchors = new Dictionary<int, Transform>();
    private readonly Dictionary<int, CutStation> _cutStations = new Dictionary<int, CutStation>();
    private readonly Dictionary<int, Transform> _stationUiAnchors = new Dictionary<int, Transform>();
    private readonly Dictionary<int, Transform> _waitingUiAnchors = new Dictionary<int, Transform>();
    private readonly Dictionary<int, GameObject> _selectionPlates = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GameObject> _stationRoots = new Dictionary<int, GameObject>();
    private SalonGameModel _game;
    private Camera _camera;
    private Canvas _hudCanvas;
    private GameObject _toolBar;
    private RectTransform _toolButtonRoot;
    private Text _focusLabel;
    private Text _hintLabel;
    private Image _toolBarBacking;
    private TMP_Text _coinBalanceLabel;
    private GameObject _shopPanel;
    private Text _shopStatusLabel;
    private Text _managementFundsLabel;
    private Button _autoBlowPurchaseButton;
    private BusinessDayController _dayController;
    private CustomerTrafficDirector _trafficDirector;
    private TMP_Text _dayNumberLabel;
    private Text _businessClockLabel;
    private Text _closingLabel;
    private TMP_Text _reputationLabel;
    private TMP_Text _weekdayLabel;
    private TMP_Text _weatherWeekdayLabel;
    private TMP_Text _topHudDayLabel;
    private TMP_Text _topHudCoinLabel;
    private TMP_Text _satisfactionLabel;
    private ShopSatisfactionModel _satisfaction;
    private Image _satisfactionFill;
    private Image _satisfactionSmile;
    private Sprite _satisfactionHappySprite;
    private Sprite _satisfactionNeutralSprite;
    private Sprite _satisfactionSadSprite;
    private GameObject _dayStartPanel;
    private Text _dayStartLabel;
    private Text _preOpenInfoLabel;
    private Button _startBusinessButton;
    private GameObject _resultPanel;
    private Text _resultTitleLabel;
    private Text _resultSummaryLabel;
    private Button _resultContinueButton;
    private GameObject _pausePanel;
    private Button _resumeButton;
    private RectTransform _coinHudTarget;
    private Transform _player;
    private HairdresserCharacter _playerCharacter;
    private Vector3 _playerTarget;
    private Vector3 _overviewCameraPosition;
    private Quaternion _overviewCameraRotation;
    private Vector3 _cameraPositionTarget;
    private float _cameraSizeTarget;
    private int _selectedToolIndex;
    private HaircutInteraction _haircutInteraction;
    private SalonCustomerView _activeHaircutView;
    private int _activeHaircutPointer = int.MinValue;
    private int _nextCustomerId;
    private float _spawnCooldown;
    private bool _deferredResultPending;
    private bool _resultPresentationCompleted;
    private Vector3[] _playerRoute = new Vector3[0];
    private int _playerRouteIndex;
    private Vector3 _playerRouteDestination;
    private bool _hasPlayerRoute;

    private ActiveServiceAction _selectedServiceAction = ActiveServiceAction.None;
    private SalonCustomerView _activeServiceView;
    private int _activeServicePointer = int.MinValue;
    private Phase7AcceptanceOptions _phase7AcceptanceOptions;
    private Stage3WashAcceptanceOptions _phase3WashAcceptanceOptions;
    private Stage4AHaircutAcceptanceOptions _phase4AHaircutAcceptanceOptions;
    private Stage4A5AcceptanceOptions _phase4A5AcceptanceOptions;
    private ShampooTutorialController _shampooTutorial;
    private Coroutine _toastRoutine;
    private int _toastVersion;
    private int _toastCustomerId = -1;
    private CustomerState? _toastExpectedCustomerState;
    private ShampooTutorialStep _lastTutorialToastStep = ShampooTutorialStep.Inactive;

    internal SalonGameModel RuntimeGame => _game;
    internal BusinessDayController RuntimeDay => _dayController;
    internal void RuntimeAttachCustomerView(CustomerModel customer)
    {
        if (customer == null || FindCustomerView(customer) != null) return;
        CreateCustomerView(customer, EntrancePosition, Hex("C7569B"), false);
    }
    internal void RuntimeFocusCustomer(CustomerModel customer)
    {
        if (customer == null) return;
        _game.SelectCustomer(customer);
        ApplyFocus(customer);
    }

    private static readonly Vector3 EntrancePosition = new Vector3(-10.4f, 1.05f, -2.7f);
    private static readonly Vector3 ExitPosition = new Vector3(-10.8f, 1.05f, 5.9f);
    public static readonly Vector3 PrimaryWashBedPosition = new Vector3(-7.2f, .35f, 4.6f);
    public static readonly Vector3 SecondaryWashBedPosition = PrimaryWashBedPosition + new Vector3(2.6f, 0f, 0f);
    public static readonly Vector3 WashCustomerAnchorPosition = PrimaryWashBedPosition + new Vector3(0f, 1.4f, -.6f);
    public static readonly Vector3 SecondaryWashCustomerAnchorPosition = SecondaryWashBedPosition + new Vector3(0f, 1.4f, -.6f);
    public static readonly Vector3 WashPlayerAnchorPosition = PrimaryWashBedPosition + new Vector3(-1.55f, -.35f, -.55f);
    public static readonly Vector3 SecondaryWashPlayerAnchorPosition = SecondaryWashBedPosition + new Vector3(1.55f, -.35f, -.55f);
    private static readonly Vector3[] WaitingPositions =
    {
        new Vector3(-9f, .4f, -4.3f),
        new Vector3(-7.75f, .4f, -4.3f),
        new Vector3(-6.5f, .4f, -4.3f),
        new Vector3(-5.25f, .4f, -4.3f)
    };

    public SalonViewState CurrentViewState => _game == null ? SalonViewState.Overview : _game.ViewState;
    public static IReadOnlyList<ServiceExecutionType> GetWashStationServices() => WashStationServices;
    public static IReadOnlyList<ServiceExecutionType> GetHaircutStationServices() => HaircutStationServices;
    public static IReadOnlyList<SalonTool> GetHaircutStationTools() => HaircutStationTools;
    public static bool ShouldShowCustomerStatusAtStation(
        CustomerModel customer, bool isSelected, bool reachedDestination)
    {
        if (customer == null) return false;
        return customer.State != CustomerState.Leaving && customer.State != CustomerState.Exited;
    }

    public static bool ShouldPreferMobileTarget(bool candidateAvailable, float candidateDistance,
        bool currentAvailable, float currentDistance)
    {
        if (candidateAvailable != currentAvailable) return candidateAvailable;
        return candidateDistance < currentDistance;
    }

    public static bool HasArmedToolSelection(int selectedToolIndex,
        ActiveServiceAction selectedAction, HaircutInteractionState haircutState)
    {
        if (selectedToolIndex < 0) return false;
        return selectedAction != ActiveServiceAction.None ||
               haircutState == HaircutInteractionState.ToolSelected ||
               haircutState == HaircutInteractionState.Holding;
    }
    public static float GetToastDuration(float duration) => Mathf.Clamp(duration, 1f, 1.5f);
    public static bool ShouldReportExtraHaircut(
        CustomerModel customer, bool fulfilledRequiredCut, int extraServiceCountBefore)
    {
        return customer != null &&
               (!fulfilledRequiredCut || customer.ExtraServiceCount > extraServiceCountBefore);
    }

    public static bool IsWashActionAvailable(CustomerModel customer, ActiveServiceAction action)
    {
        if (customer == null || customer.ActiveServiceAction != ActiveServiceAction.None) return false;
        if (action == ActiveServiceAction.Shower || action == ActiveServiceAction.Shampoo)
            return !customer.TowelWrapped;
        if (action == ActiveServiceAction.WrapTowel) return !customer.TowelWrapped;
        return action == ActiveServiceAction.RemoveTowel && customer.TowelWrapped;
    }

    private void Start()
    {
        Application.runInBackground = true;
        _phase7AcceptanceOptions = Phase7RuntimeAcceptance.ConfigureFromCommandLine(DaySettings);
        _phase3WashAcceptanceOptions = Stage3WashRuntimeAcceptance.ConfigureFromCommandLine(DaySettings);
        _phase4AHaircutAcceptanceOptions =
            Stage4AHaircutRuntimeAcceptance.ConfigureFromCommandLine(DaySettings);
        _phase4A5AcceptanceOptions =
            Stage4A5FreedomRuntimeAcceptance.ConfigureFromCommandLine(DaySettings);
        ConfigureMobileGame();
        if (IsNormalGameplayMode)
        {
            FlowSettings.MaxCustomers = Mathf.Max(5, FlowSettings.MaxCustomers);
            FlowSettings.WaitingCapacity = Mathf.Max(4, FlowSettings.WaitingCapacity);
        }
        Screen.orientation = ScreenOrientation.LandscapeLeft;
#if UNITY_STANDALONE_OSX
        Screen.SetResolution(1600, 900, false);
#endif
        FlowSettings.WaitingCapacity = Mathf.Clamp(FlowSettings.WaitingCapacity, 1, WaitingPositions.Length);
        FlowSettings.MaxCustomers = Mathf.Max(FlowSettings.WaitingCapacity, DaySettings.MaxConcurrentCustomers);
        FlowSettings.BusinessDuration = Mathf.Max(0f, DaySettings.BusinessDuration);
        _game = new SalonGameModel(RewardSettings, PatienceSettings, FlowSettings, ServiceSettings);
        _dayController = new BusinessDayController(DaySettings);
        _satisfaction = new ShopSatisfactionModel(SatisfactionSettings);
        _trafficDirector = new CustomerTrafficDirector(DaySettings);
        _dayController.StateChanged += HandleDayStateChanged;
        _satisfaction.Changed += HandleSatisfactionChanged;
        _haircutInteraction = new HaircutInteraction(HaircutSettings);
        _haircutInteraction.ResultResolved += HandleHaircutResult;
        _game.PaymentCreated += HandlePaymentCreated;
        _game.CustomerChanged += HandleCustomerChanged;
        _game.ViewChanged += HandleViewChanged;
        BuildWorld();
        WashCraftIntegration.Apply(GameObject.Find("Fixed Salon Map").transform);
        BuildHud();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevelopmentDebugOverlay.IsRequested(Application.absoluteURL, System.Environment.GetCommandLineArgs()))
            gameObject.AddComponent<DevelopmentDebugOverlay>().Initialize(this);
        if (BrowserCoreFlowSmoke.IsRequested(Application.absoluteURL))
            gameObject.AddComponent<BrowserCoreFlowSmoke>().Initialize(this);
#endif
        if (_mobileMode) RestoreMobileGame();
        else _dayController.PrepareDay(1);
        ResetSpawnCooldown();
        ApplyOverview(true);
        if (_phase7AcceptanceOptions != null)
            gameObject.AddComponent<Phase7RuntimeAcceptance>().Initialize(this, _phase7AcceptanceOptions);
        if (_phase3WashAcceptanceOptions != null)
            gameObject.AddComponent<Stage3WashRuntimeAcceptance>().Initialize(this, _phase3WashAcceptanceOptions);
        if (_phase4AHaircutAcceptanceOptions != null)
            gameObject.AddComponent<Stage4AHaircutRuntimeAcceptance>()
                .Initialize(this, _phase4AHaircutAcceptanceOptions);
        if (_phase4A5AcceptanceOptions != null)
            gameObject.AddComponent<Stage4A5FreedomRuntimeAcceptance>()
                .Initialize(this, _phase4A5AcceptanceOptions);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (WashCraftIntegration.IsEnabled &&
            (Application.absoluteURL.Contains("washCraft=detail") || Application.absoluteURL.Contains("washCraft=service")))
            gameObject.AddComponent<WashCraftEvidence>().Initialize(this);
#endif
        StartCoroutine(CaptureFromCommandLine());
    }

    private void Update()
    {
        if (_game == null || _dayController == null) return;
        _dayController.TickWithAdmissionCount(Time.deltaTime, CountActiveCustomers(), CountUnfinishedCustomers());
        UpdateDayHud();
        RefreshMobileDayPresentation();
        EmitMobileEvidence();
        if (_dayController.IsPaused || _dayController.State == DayState.PreOpen ||
            _dayController.State == DayState.ClosedManagement) return;
        if (_dayController.State == DayState.Result)
        {
            // A forced/timeout Result can arrive while a customer is already
            // on the visible exit route. Keep ticking that route until the
            // view is removed, then finish the normal result presentation.
            if (_deferredResultPending)
            {
                _game.Tick(Time.deltaTime);
                UpdateCustomerViews();
                UpdateCamera();
            }
            else TryFinalizeDeferredResult();
            return;
        }
        _game.Tick(Time.deltaTime);
        MaintainCustomerFlow(Time.deltaTime);
        if (_mobileMode) UpdateMobilePlay(Time.deltaTime);
        else
        {
            UpdateHaircutInteraction();
            UpdateActiveServiceInteraction();
        }
        UpdateCustomerViews();
        TryFinalizeDeferredResult();
        UpdateCamera();
        if (!_mobileMode) UpdatePlayer();
    }

    public void HandleCustomerClick(CustomerModel customer)
    {
        if (_mobileMode) return;
        if (!CanInteractWithSalon() || customer == null || customer.State == CustomerState.Leaving || customer.State == CustomerState.Exited) return;
        CancelHaircutInteraction(true);
        _game.SelectCustomer(customer);
    }

    public void HandleStationClick(int stationId)
    {
        if (_mobileMode) return;
        if (!CanInteractWithSalon() || _game == null || _game.SelectedCustomer == null) return;
        CustomerModel customer = _game.SelectedCustomer;
        bool targetMatchesCurrentNeed = stationId >= 0 && stationId < _game.Workstations.Count &&
            SalonGameModel.IsCompatibleStation(
                customer.CurrentNeed, _game.Workstations[stationId].Type);
        if (!_game.Assign(customer, stationId))
        {
            ShowToast(customer.ActiveServiceAction != ActiveServiceAction.None ||
                      customer.AttentionState == CustomerAttentionState.ActiveOperation
                ? "当前操作结束或取消后即可移动顾客"
                : "目标工位仍被占用，或当前不能转移顾客");
            return;
        }
        ResetToolSelection();
        ApplyFocus(customer);
        _shampooTutorial?.NotifyMovedToNextStation(customer);
        RefreshTutorialPresentation();
        if (!targetMatchesCurrentNeed)
            ShowToast("顾客被安排到了错误工位");
        else
            ShowCustomerStateToast("顾客正在前往工位", customer, CustomerState.MovingToStation);
    }

    public void HandleCustomerDrop(CustomerModel customer, int stationId)
    {
        if (_mobileMode) return;
        if (customer == null) return;
        _game.SelectCustomer(customer);
        HandleStationClick(stationId);
    }

    public void HandleBlankClick()
    {
        if (_mobileMode) return;
        if (!CanInteractWithSalon() || _game == null || _game.ViewState == SalonViewState.Overview) return;
        CancelHaircutInteraction(true);
        _game.ClearFocus();
    }

    private void BuildWorld()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.72f, .68f, .62f);
        RenderSettings.fog = false;

        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(PhysicsRaycaster));
        cameraObject.tag = "MainCamera";
        _camera = cameraObject.GetComponent<Camera>();
        _camera.orthographic = true;
        _camera.orthographicSize = 8.35f;
        _camera.backgroundColor = Exterior;
        _camera.nearClipPlane = .1f;
        _camera.farClipPlane = 100f;
        _camera.transform.position = new Vector3(0f, 18f, -19f);
        _camera.transform.LookAt(new Vector3(0f, 0f, 1.4f));
        WashCraftIntegration.ConfigureCamera(_camera);
        _overviewCameraPosition = _camera.transform.position;
        _overviewCameraRotation = _camera.transform.rotation;
        _cameraPositionTarget = _overviewCameraPosition;
        _cameraSizeTarget = _camera.orthographicSize;

        var sun = new GameObject("Warm Key Light", typeof(Light)).GetComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, .84f, .68f);
        sun.intensity = 1.35f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        var fill = new GameObject("Soft Fill", typeof(Light)).GetComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(.55f, .7f, 1f);
        fill.intensity = .42f;
        fill.transform.rotation = Quaternion.Euler(55f, 145f, 0f);

        var salon = new GameObject("Fixed Salon Map").transform;
        var exterior = Block("Exterior Base", salon, new Vector3(0f, -.35f, 1f), new Vector3(23.5f, .55f, 15.5f), Exterior);
        exterior.AddComponent<SalonFloorClick>().Owner = this;
        for (int x = -10; x <= 10; x++)
        for (int z = -5; z <= 7; z++)
        {
            var tileColor = ((x + z) & 1) == 0 ? Teal : new Color(Teal.r * .92f, Teal.g * .96f, Teal.b * .96f);
            var tile = Block("Floor Tile", salon, new Vector3(x, -.02f, z), new Vector3(.97f, .12f, .97f), tileColor);
            tile.AddComponent<SalonFloorClick>().Owner = this;
        }

        BuildWalls(salon);
        BuildWashZone(salon);
        BuildHaircutZone(salon);
        BuildProcessingZone(salon);
        BuildWaitingZone(salon);
        BuildCashier(salon);
        BuildShelvesAndPlants(salon);
        BuildStage1SupplyProps(salon);
        GameObject playerPrefab = Resources.Load<GameObject>("Characters/Hairdresser");
        if (playerPrefab == null)
            throw new MissingReferenceException("Hairdresser prefab is missing at Resources/Characters/Hairdresser.");
        GameObject playerInstance = Instantiate(playerPrefab, salon);
        playerInstance.name = "主控理发师";
        playerInstance.transform.position = new Vector3(0f, .05f, -3.2f);
        _player = playerInstance.transform;
        _playerCharacter = playerInstance.GetComponent<HairdresserCharacter>();
        BuildStage1CarryVisual(_player);
        _playerTarget = _player.position;
        ConfigureSimple2DPresentation();
    }

    private void BuildWalls(Transform root)
    {
        Block("Back Wall", root, new Vector3(0f, 2.2f, 7.65f), new Vector3(22.5f, 4.7f, .55f), CoralLight);
        Block("Back Wall Cream", root, new Vector3(0f, 3.7f, 7.34f), new Vector3(21.8f, 1.55f, .16f), Cream);
        Block("Left Wall", root, new Vector3(-10.85f, 2.2f, 1f), new Vector3(.55f, 4.7f, 13.7f), Coral);
        Block("Right Wall", root, new Vector3(10.85f, 2.2f, 1f), new Vector3(.55f, 4.7f, 13.7f), Coral);
        for (int x = -9; x <= 9; x += 3)
            Block("Wall Panel", root, new Vector3(x, 1.25f, 7.02f), new Vector3(2.65f, 1.8f, .18f), x % 2 == 0 ? Coral : CoralLight);
    }

    private void BuildWashZone(Transform root)
    {
        var zone = new GameObject("洗头区").transform;
        zone.SetParent(root, false);
        for (int i = 0; i < 2; i++)
        {
            int stationId = i == 0 ? 0 : 4;
            Vector3 position = i == 0 ? PrimaryWashBedPosition : SecondaryWashBedPosition;
            Vector3 customerAnchor = i == 0 ? WashCustomerAnchorPosition : SecondaryWashCustomerAnchorPosition;
            Vector3 playerAnchor = i == 0 ? WashPlayerAnchorPosition : SecondaryWashPlayerAnchorPosition;
            var station = new GameObject("Wash Workstation " + (i + 1)).transform;
            station.SetParent(zone, false);
            Block("Wash Base", station, position, new Vector3(2.2f, .65f, 3.05f), TealDark);
            Block("Wash Bed", station, position + new Vector3(0f, .62f, -.2f), new Vector3(1.75f, .35f, 2.45f), Cream, new Vector3(-8f, 0f, 0f));
            Cylinder("Basin", station, position + new Vector3(0f, 1.15f, 1.05f), new Vector3(1.35f, .22f, 1.35f), Cream);
            Transform target = Marker("CustomerSeatAnchor", station, customerAnchor);
            _customerSeatAnchors[stationId] = target;
            _playerServiceAnchors[stationId] = Marker("PlayerServiceAnchor", station, playerAnchor);
            _stationUiAnchors[stationId] = Marker("CustomerUIAnchor", station,
                customerAnchor + new Vector3(i == 0 ? -.18f : .18f, .9f, 0f));
            _stationRoots[stationId] = station.gameObject;
            var stationClick = station.gameObject.AddComponent<SalonStationClick>();
            stationClick.Owner = this;
            stationClick.StationId = stationId;
            _selectionPlates[stationId] = SelectionPlate(station,
                position + new Vector3(0f, -.32f, -.25f), new Vector3(2.7f, .08f, 3.5f));
            FurnitureShadow(station, "furniture-wash-station", new Vector3(position.x, 0f, position.z));
        }
    }

    private void BuildHaircutZone(Transform root)
    {
        var zone = new GameObject("中央剪发区").transform;
        zone.SetParent(root, false);
        CutStationManifest manifest = CutStationManifestLoader.LoadFromResources();
        IReadOnlyList<CutStationValidationIssue> issues = CutStationValidator.ValidateManifest(manifest);
        if (issues.Count > 0)
            throw new InvalidDataException("Cut station manifest validation failed: " + issues[0]);
        Vector3[] origins = { new Vector3(-2.1f, .2f, .2f), new Vector3(2.2f, .2f, .2f) };
        CutStationOrientation[] orientations =
        {
            CutStationOrientation.BackWall,
            CutStationOrientation.RightWall
        };
        bool showDebug = IsCutStationDebugRequested();
        for (int i = 0; i < origins.Length; i++)
        {
            int stationId = i + 1;
            CutStation station = CutStationFactory.Create(manifest, "cut-station-classic-poc",
                origins[i], orientations[i], zone, showDebug);
            station.gameObject.name = "Haircut Workstation " + stationId;
            _cutStations[stationId] = station;
            _customerSeatAnchors[stationId] = station.CustomerSeatAnchor;
            _playerServiceAnchors[stationId] = station.StylistWorkAnchor;
            _stationQueueAnchors[stationId] = station.QueueAnchor;
            _stationUiAnchors[stationId] = station.ServiceVfxAnchor;
            _stationRoots[stationId] = station.gameObject;
            var stationClick = station.gameObject.AddComponent<SalonStationClick>();
            stationClick.Owner = this;
            stationClick.StationId = stationId;
            ResolvedCutStationRect footprint = station.Layout.Footprint;
            _selectionPlates[stationId] = SelectionPlate(station.transform,
                new Vector3(footprint.Center.x, -.14f, footprint.Center.y),
                new Vector3(footprint.Size.x, .08f, footprint.Size.y));
        }
    }

    private static bool IsCutStationDebugRequested()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
            if (string.Equals(args[i], "--cut-station-debug", System.StringComparison.OrdinalIgnoreCase))
                return true;
        return !string.IsNullOrEmpty(Application.absoluteURL) &&
               Application.absoluteURL.IndexOf("cutStationDebug=1", System.StringComparison.OrdinalIgnoreCase) >= 0;
#else
        return false;
#endif
    }

    private void BuildProcessingZone(Transform root)
    {
        var zone = new GameObject("烫发区").transform;
        zone.SetParent(root, false);
        const int stationId = 3;
        Vector3 position = new Vector3(7.1f, .2f, 4.4f);
        var station = new GameObject("Perm Workstation").transform;
        station.SetParent(zone, false);
        Block("Perm Machine Base", station, position, new Vector3(2f, .55f, 2.25f), Purple);
        BuildChair(station, position + new Vector3(0f, .45f, -.1f), TealDark);
        Cylinder("Perm Processor Hood", station, position + new Vector3(0f, 2.35f, .05f),
            new Vector3(1.65f, .6f, 1.65f), Hex("A16AB5"), new Vector3(90f, 0f, 0f));
        Block("Perm Machine Column", station, position + new Vector3(0f, 1.2f, .85f),
            new Vector3(.5f, 2.2f, .5f), Purple);
        _customerSeatAnchors[stationId] = Marker("CustomerSeatAnchor", station, position);
        _playerServiceAnchors[stationId] = Marker("PlayerServiceAnchor", station,
            position + new Vector3(-1.65f, 0f, -.7f));
        _stationUiAnchors[stationId] = Marker("CustomerUIAnchor", station,
            _customerSeatAnchors[stationId].position + new Vector3(-.18f, .9f, 0f));
        _stationRoots[stationId] = station.gameObject;
        var stationClick = station.gameObject.AddComponent<SalonStationClick>();
        stationClick.Owner = this;
        stationClick.StationId = stationId;
        _selectionPlates[stationId] = SelectionPlate(station, position + new Vector3(0f, -.13f, 0f),
            new Vector3(2.45f, .08f, 2.7f));
        FurnitureShadow(station, "furniture-perm-station", new Vector3(position.x, 0f, position.z));
    }

    private void BuildWaitingZone(Transform root)
    {
        var zone = new GameObject("顾客等候区").transform;
        zone.SetParent(root, false);
        Block("Sofa Seat", zone, new Vector3(-7.3f, .55f, -4.5f), new Vector3(5.2f, .65f, 1.6f), DarkWood);
        Block("Sofa Cushion", zone, new Vector3(-7.3f, 1f, -4.3f), new Vector3(4.75f, .45f, 1.25f), Hex("D69A32"));
        Block("Sofa Back", zone, new Vector3(-7.3f, 1.55f, -3.85f), new Vector3(5f, 1.25f, .45f), Wood);
        Block("Coffee Table", zone, new Vector3(-6.9f, .35f, -2.35f), new Vector3(3.2f, .35f, 1.2f), Wood);
        FurnitureShadow(zone, "furniture-waiting-sofa", new Vector3(-7.3f, 0f, -4.3f));
        FurnitureShadow(zone, "furniture-coffee-table", new Vector3(-6.9f, 0f, -2.35f));
        for (int i = 0; i < WaitingPositions.Length; i++)
        {
            // Fixed staggered rows keep adjacent multi-step request groups visually distinct.
            float staggerY = (i & 1) == 0 ? .68f : -.52f;
            float nudgeX = (i & 1) == 0 ? -.14f : .14f;
            _waitingUiAnchors[i] = Marker("Waiting CustomerUIAnchor " + (i + 1), zone,
                WaitingPositions[i] + new Vector3(nudgeX, staggerY, 0f));
        }
    }

    private void BuildCashier(Transform root)
    {
        var zone = new GameObject("收银区").transform;
        zone.SetParent(root, false);
        Block("Counter", zone, new Vector3(7.2f, .9f, -3.9f), new Vector3(5.3f, 1.8f, 2.2f), Wood);
        Block("Counter Top", zone, new Vector3(7.2f, 1.9f, -3.9f), new Vector3(5.6f, .25f, 2.4f), Cream);
        Block("Register", zone, new Vector3(6.5f, 2.3f, -3.7f), new Vector3(1.2f, .75f, .75f), Ink, new Vector3(-10f, 0f, 0f));
        Block("Coin Screen", zone, new Vector3(6.5f, 2.35f, -3.25f), new Vector3(.75f, .42f, .08f), Hex("60B85C"));
        FurnitureShadow(zone, "furniture-cashier-counter", new Vector3(7.2f, 0f, -3.9f));
    }

    private void BuildShelvesAndPlants(Transform root)
    {
        for (int x = -8; x <= 8; x += 4)
        {
            Block("Shelf", root, new Vector3(x, 2.9f, 6.75f), new Vector3(3f, .22f, .65f), Wood);
            FurnitureShadow(root, "furniture-product-shelf", new Vector3(x, 0f, 6.75f));
            for (int i = -1; i <= 1; i++)
                Block("Bottle", root, new Vector3(x + i * .65f, 3.35f, 6.7f), new Vector3(.32f, .72f + .12f * (i + 1), .32f), i == 0 ? Purple : (i < 0 ? Coral : Teal));
        }
        Vector3[] plantPositions = { new Vector3(-9.5f, .25f, -2.4f), new Vector3(9.4f, .25f, -.9f), new Vector3(4.5f, .25f, -5f), new Vector3(-9.4f, .25f, 5.7f) };
        foreach (var position in plantPositions)
        {
            BuildPlant(root, position);
            FurnitureShadow(root, "prop-potted-plant", new Vector3(position.x, 0f, position.z));
        }
    }

    private void BuildHud()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var canvasObject = new GameObject("Salon HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _hudCanvas = canvasObject.GetComponent<Canvas>();
        _hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _hudCanvas.sortingOrder = 50;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        var safe = UiPanel("Safe Area", _hudCanvas.transform, Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        safe.AddComponent<SalonSafeArea>();
        SalonUiFactory.MakeClickThrough(safe);
        BuildOfficialTopHud(safe.transform);
        _businessClockLabel = UiLabel("03:00", safe.transform, 32, Cream, TextAnchor.MiddleCenter,
            new Vector2(0f, -168f), new Vector2(250f, 58f), Ink, new Vector2(.5f, 1f));
        _closingLabel = UiLabel("", safe.transform, 25, Color.white, TextAnchor.MiddleCenter,
            new Vector2(280f, -168f), new Vector2(270f, 58f), Coral, new Vector2(.5f, 1f));
        _closingLabel.transform.parent.gameObject.SetActive(false);
        _hintLabel = UiLabel("", safe.transform, 23, Cream, TextAnchor.MiddleCenter, new Vector2(0f, -125f), new Vector2(620f, 46f), new Color(.11f, .12f, .14f, .82f), new Vector2(.5f, 1f));
        _hintLabel.transform.parent.gameObject.SetActive(false);

        _toolBar = UiPanel("Workstation Tools", safe.transform, DarkWood, new Vector2(.5f, 0f),
            new Vector2(.5f, 0f), new Vector2(0f, 100f), new Vector2(720f, 190f));
        SalonUiFactory.StyleRoundedPanel(_toolBar, Hex("3A2118"), new Vector2(0f, -8f));
        _toolBarBacking = _toolBar.GetComponent<Image>();
        var traySurface = UiPanel("Tool Tray Surface", _toolBar.transform, Hex("F8E6C8"),
            new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, -5f), new Vector2(684f, 154f));
        SalonUiFactory.StyleRoundedPanel(traySurface);
        SalonUiFactory.MakeClickThrough(traySurface);
        var statusRibbon = UiPanel("Tool Status Ribbon", _toolBar.transform, Hex("68402A"),
            new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 65f), new Vector2(620f, 42f));
        SalonUiFactory.StyleRoundedPanel(statusRibbon);
        _focusLabel = UiLabel("", statusRibbon.transform, 22, Cream, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(590f, 34f));
        _toolButtonRoot = UiRect("Tool Buttons", _toolBar.transform, Vector2.zero, Vector2.one,
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        _toolBar.SetActive(false);
        BuildMobileHud(safe.transform);
        BuildDayStartPanel(safe.transform);
        BuildResultPanel(safe.transform);
        BuildMobileResultControls();
        BuildShopPanel(safe.transform);
        BuildPausePanel(safe.transform);
    }

    private void BuildOfficialTopHud(Transform parent)
    {
        RectTransform topHud = UiRect("Top HUD", parent, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(.5f, 1f), Vector2.zero, new Vector2(0f, 150f));
        RectTransform left = UiRect("Left Group", topHud, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(8f, 0f), new Vector2(440f, 140f));
        RectTransform right = UiRect("Right Group", topHud, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(.5f, 1f), Vector2.zero, new Vector2(0f, 140f));

        RectTransform calendar = UiRect("Calendar", left, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(.5f, .5f), new Vector2(79f, -77f), new Vector2(192f, 160f));
        HudArtwork("Calendar Artwork", calendar, "TopHUD/calendar-panel", Vector2.zero, new Vector2(192f, 160f));
        _topHudDayLabel = HudText("1", calendar, 60f, Hex("3B3026"), new Vector2(0f, -17f),
            new Vector2(94f, 68f));
        _dayNumberLabel = _topHudDayLabel;

        RectTransform weather = UiRect("Weather", left, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(.5f, .5f), new Vector2(250f, -73f), new Vector2(240f, 200f));
        HudArtwork("Weather Artwork", weather, "TopHUD/dark-panel", new Vector2(0f, -6f),
            new Vector2(240f, 200f), false, .82f);
        _weatherWeekdayLabel = HudText("周一", weather, 32f, Hex("FFF2CF"), new Vector2(-55f, 9f),
            new Vector2(82f, 52f));
        _weekdayLabel = _weatherWeekdayLabel;
        HudArtwork("Sun Artwork", weather, "TopHUD/sun-icon", new Vector2(43f, 7f), new Vector2(82f, 82f));

        RectTransform coins = UiRect("Coins", right, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, .5f), new Vector2(-42f, -67f), new Vector2(330f, 120f));
        HudArtwork("Coins Artwork", coins, "TopHUD/dark-panel", Vector2.zero,
            new Vector2(340f, 190f), false, .82f);
        HudArtwork("Coin Artwork", coins, "TopHUD/coin-icon", new Vector2(-94f, 0f), new Vector2(92f, 92f));
        _topHudCoinLabel = HudText(_game.Balance.ToString("N0"), coins, 38f, Color.white,
            new Vector2(4f, 0f), new Vector2(150f, 66f));
        _coinBalanceLabel = _topHudCoinLabel;
        if (!_mobileMode) HudImageButton("Add Coins", coins, "TopHUD/add-button", new Vector2(112f, 0f),
            new Vector2(64f, 64f), ToggleShop);
        _coinHudTarget = coins;

        RectTransform satisfaction = UiRect("Satisfaction", right, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(.5f, .5f), new Vector2(-302f, -60f), new Vector2(355f, 125f));
        HudArtwork("ReputationBar_BG", satisfaction, "TopHUD/ReputationBar/ReputationBar_BG",
            Vector2.zero, new Vector2(355f, 118f), false);
        Image track = HudArtwork("Progress_Track", satisfaction, "TopHUD/ReputationBar/Progress_Track",
            new Vector2(34f, -4f), new Vector2(204f, 48f), false);
        _satisfactionFill = HudArtwork("Progress_Fill", track.transform,
            "TopHUD/ReputationBar/Progress_Fill", Vector2.zero, new Vector2(190f, 34f), false);
        _satisfactionFill.type = Image.Type.Filled;
        _satisfactionFill.fillMethod = Image.FillMethod.Horizontal;
        _satisfactionFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _satisfactionFill.fillClockwise = true;
        _satisfactionHappySprite = LoadTopHudSprite("TopHUD/ReputationBar/Smile_Happy");
        _satisfactionNeutralSprite = LoadTopHudSprite("TopHUD/ReputationBar/Smile_Neutral");
        _satisfactionSadSprite = LoadTopHudSprite("TopHUD/ReputationBar/Smile_Sad");
        _satisfactionSmile = HudArtwork("Smile_Icon", satisfaction,
            "TopHUD/ReputationBar/Smile_Happy", new Vector2(-116f, -2f), new Vector2(78f, 78f));
        _satisfactionLabel = HudText("90/100", satisfaction, 28f, Color.white, new Vector2(42f, -4f),
            new Vector2(118f, 48f));
        _satisfactionLabel.gameObject.name = "ValueText";
        _reputationLabel = _satisfactionLabel;

        HudArtwork("Settings Backplate", right, "TopHUD/settings-backplate-final", new Vector2(-62f, -70f),
            new Vector2(108f, 108f), true, 1f, new Vector2(1f, 1f));
        Button settingsButton = HudImageButton("Settings", right, "TopHUD/settings-icon", new Vector2(-62f, -70f),
            new Vector2(96f, 96f), TogglePause, new Vector2(1f, 1f), new Vector2(72f, 72f));
        if (_mobileMode) BindMobilePauseButton(settingsButton);
        RefreshTopHudDay(1);
        RefreshTopHudSatisfaction();
    }

    private void BuildDayStartPanel(Transform parent)
    {
        _dayStartPanel = UiPanel("Day Start Overlay", parent, new Color(.10f, .08f, .07f, .72f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Transform surface = BuildModalSurface(_dayStartPanel.transform, "Opening Card", "Opening Surface",
            new Vector2(720f, 500f), Hex("FFF0D7"));
        _dayStartLabel = UiLabel("DAY 1\n开店准备", surface, 58, DarkWood,
            TextAnchor.MiddleCenter, new Vector2(0f, 120f), new Vector2(620f, 170f));
        _preOpenInfoLabel = UiLabel("今日准备\n基础设备已就绪", surface, 28, Ink,
            TextAnchor.MiddleCenter, new Vector2(0f, -15f), new Vector2(620f, 110f));
        _startBusinessButton = UiButton("开始营业", surface, new Vector2(.5f, .5f),
            new Vector2(0f, -170f), new Vector2(330f, 82f), Teal, StartBusinessDay);
        _dayStartPanel.SetActive(false);
    }

    private void BuildResultPanel(Transform parent)
    {
        _resultPanel = UiPanel("Day Result", parent, new Color(.10f, .08f, .07f, .78f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Transform surface = BuildModalSurface(_resultPanel.transform, "Result Card", "Result Surface",
            new Vector2(760f, 820f), Hex("FFF0D7"));
        _resultTitleLabel = UiLabel("DAY 1 营业结束", surface, 42, Color.white,
            TextAnchor.MiddleCenter, new Vector2(0f, 325f), new Vector2(620f, 72f), Coral);
        _resultSummaryLabel = UiLabel("", surface, 24, Ink, TextAnchor.MiddleLeft,
            new Vector2(0f, 5f), new Vector2(570f, 540f));
        _resultContinueButton = UiButton("继续前往商店", surface, new Vector2(.5f, .5f),
            new Vector2(0f, -335f), new Vector2(350f, 76f), Teal, ContinueToShop);
        _resultContinueButton.GetComponentInChildren<Text>().text = "进入闭店经营";
        _resultPanel.SetActive(false);
    }

    private void BuildShopPanel(Transform parent)
    {
        _shopPanel = UiPanel("Equipment Shop", parent, new Color(.10f, .08f, .07f, .78f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Transform surface = BuildModalSurface(_shopPanel.transform, "Shop Card", "Shop Surface",
            new Vector2(860f, 820f), Hex("352A28"));
        UiLabel("闭店经营", surface, 40, Cream, TextAnchor.MiddleCenter,
            new Vector2(0f, 330f), new Vector2(430f, 64f));
        _managementFundsLabel = UiLabel("当前资金  0", surface, 25, Gold, TextAnchor.MiddleCenter,
            new Vector2(0f, 282f), new Vector2(430f, 48f));
        EquipmentProductModel product = _game.AutoBlowStandProduct;
        UiLabel("A", surface, 72, Gold, TextAnchor.MiddleCenter,
            new Vector2(-300f, 205f), new Vector2(120f, 120f), DarkWood);
        UiLabel(product.DisplayName + (_mobileMode ? "升级\n启动更快，收尾时间更宽裕。\n基础自动吹发已免费提供。" : "\n启动后自动吹发，你可以腾出手处理其他顾客。\n超时后会自动安全停机。"),
            surface, 25, Color.white, TextAnchor.MiddleLeft,
            new Vector2(40f, 205f), new Vector2(500f, 125f));
        _shopStatusLabel = UiLabel("", surface, 22, Cream, TextAnchor.MiddleCenter,
            new Vector2(-110f, 125f), new Vector2(550f, 52f));
        _autoBlowPurchaseButton = UiButton("购买  " + product.Price.ToString("N0") + " 金币", surface,
            new Vector2(.5f, .5f), new Vector2(225f, 125f), new Vector2(250f, 58f), Gold, PurchaseAutoBlowStand);
        UiLabel(_mobileMode ? "基础吹发：已开放\n运行时可去接待其他顾客" : "染发设备    LOCKED\n尚未达到购买条件", surface, 25, Color.white,
            TextAnchor.MiddleLeft, new Vector2(0f, 10f), new Vector2(650f, 105f), Purple);
        UiLabel(_mobileMode ? "升级收益\n缩短启动时间，延长合适的收尾窗口" : "烫发设备    LOCKED\n尚未达到购买条件", surface, 25, Color.white,
            TextAnchor.MiddleLeft, new Vector2(0f, -115f), new Vector2(650f, 105f), DarkWood);
        UiLabel("所有商品始终可见 · 是否购买由你决定", surface, 20, Cream,
            TextAnchor.MiddleCenter, new Vector2(0f, -225f), new Vector2(650f, 44f));
        UiButton("准备下一天", surface, new Vector2(.5f, .5f), new Vector2(0f, -305f),
            new Vector2(330f, 72f), Teal, BeginNextDay);
        _shopPanel.SetActive(false);
    }

    private void BuildPausePanel(Transform parent)
    {
        _pausePanel = UiPanel("Pause Overlay", parent, new Color(.10f, .08f, .07f, .72f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Transform surface = BuildModalSurface(_pausePanel.transform, "Pause Card", "Pause Surface",
            new Vector2(620f, 390f), Hex("FFF0D7"));
        UiLabel("营业已暂停", surface, 52, DarkWood, TextAnchor.MiddleCenter,
            new Vector2(0f, 90f), new Vector2(520f, 90f));
        _resumeButton = UiButton("继续营业", surface, new Vector2(.5f, .5f),
            new Vector2(0f, -30f), new Vector2(300f, 76f), Teal, TogglePause);
        _pausePanel.SetActive(false);
    }

    private static Transform BuildModalSurface(Transform parent, string cardName, string surfaceName,
        Vector2 cardSize, Color surfaceColor)
    {
        var card = UiPanel(cardName, parent, DarkWood, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            Vector2.zero, cardSize);
        SalonUiFactory.StyleRoundedPanel(card, Hex("321E16"), new Vector2(0f, -10f));
        var surface = UiPanel(surfaceName, card.transform, surfaceColor, new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), Vector2.zero, cardSize - new Vector2(36f, 36f));
        SalonUiFactory.StyleRoundedPanel(surface);
        return surface.transform;
    }

    private void ToggleShop()
    {
        if (_dayController != null && _dayController.State == DayState.ClosedManagement)
            _shopPanel.SetActive(true);
    }

    private void UnlockShopForAcceptance()
    {
        _game.SetFirstDayCompleteForDebug(true);
        RefreshShopPanel();
    }

    private void PurchaseAutoBlowStand()
    {
        bool purchased = _game.PurchaseAutoBlowStand();
        if (purchased) SaveMobileCheckpoint(true);
        ShowToast(purchased ? "自动吹风支架已购买" : _game.AutoBlowStandProduct.LockReason);
        _coinBalanceLabel.text = _game.Balance.ToString("N0");
        RefreshShopPanel();
    }

    private void RefreshShopPanel()
    {
        EquipmentProductModel product = _game.AutoBlowStandProduct;
        if (_managementFundsLabel != null)
            _managementFundsLabel.text = "当前资金  🪙 " + _game.Balance.ToString("N0");
        if (product.Purchased)
            _shopStatusLabel.text = "已购买 · 下一营业日立即生效";
        else if (!_game.FirstDayCompleteForShop)
            _shopStatusLabel.text = "LOCKED · 完成 Day 1 后可购买";
        else if (_game.Balance < product.Price)
            _shopStatusLabel.text = "金币不足 · 还差 " + (product.Price - _game.Balance).ToString("N0");
        else
            _shopStatusLabel.text = "可购买 · 当前金币 " + _game.Balance.ToString("N0");
        if (_autoBlowPurchaseButton != null)
        {
            _autoBlowPurchaseButton.interactable = !product.Purchased && _game.FirstDayCompleteForShop &&
                                                  _game.Balance >= product.Price;
            Text purchaseText = _autoBlowPurchaseButton.GetComponentInChildren<Text>();
            if (purchaseText != null)
                purchaseText.text = product.Purchased ? "已购买" : "购买  " + product.Price.ToString("N0") + " 金币";
        }
    }

    private void HandleDayStateChanged(DayState state)
    {
        if (_dayStartPanel != null) _dayStartPanel.SetActive(state == DayState.PreOpen);
        if (_closingLabel != null) _closingLabel.transform.parent.gameObject.SetActive(state == DayState.ClosingGrace);
        if (state == DayState.PreOpen)
        {
            _resultPresentationCompleted = false;
            if (_dayStartLabel != null) _dayStartLabel.text = "DAY " + _dayController.DayNumber + "\n开店准备";
            if (_preOpenInfoLabel != null)
                _preOpenInfoLabel.text = "今日准备\n" + (_game.HasAutoBlowStand
                    ? "✓ 自动吹风支架已安装" : "基础设备已就绪");
            RefreshTopHudDay(_dayController.DayNumber);
            if (_resultPanel != null) _resultPanel.SetActive(false);
            if (_shopPanel != null) _shopPanel.SetActive(false);
            ShowToast("准备完成");
            if (_startBusinessButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_startBusinessButton.gameObject);
        }
        else if (state == DayState.Business)
        {
            _spawnCooldown = 0f;
            ShowToast("开门营业");
        }
        else if (state == DayState.ClosingGrace)
        {
            _spawnCooldown = float.MaxValue;
            ShowToast("打烊收尾");
        }
        else if (state == DayState.Result)
        {
            // A result notification is normally emitted only after the day
            // controller sees zero active customers. Keep this guard at the
            // presentation boundary as well: a visible Finished/Leaving
            // customer still owns a live exit route and must not be force
            // closed or have its view destroyed by a stale result callback.
            if (HasVisibleExitJourney())
            {
                _deferredResultPending = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[Salon] Result deferred while a customer is leaving.");
#endif
                return;
            }
            CompleteResultPresentation();
        }
        else if (state == DayState.ClosedManagement)
        {
            if (_resultPanel != null) _resultPanel.SetActive(false);
            if (_shopPanel != null) _shopPanel.SetActive(true);
            RefreshShopPanel();
            if (_autoBlowPurchaseButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_autoBlowPurchaseButton.gameObject);
        }
        HandleMobileDayState(state);
        UpdateDayHud();
    }

    private bool HasVisibleExitJourney()
    {
        if (_game == null || _game.Customers == null) return false;
        for (int i = 0; i < _game.Customers.Count; i++)
        {
            CustomerState state = _game.Customers[i].State;
            if (state == CustomerState.Finished || state == CustomerState.Leaving)
                return true;
        }
        return false;
    }

    private void TryFinalizeDeferredResult()
    {
        if (!_deferredResultPending || HasVisibleExitJourney()) return;
        CompleteResultPresentation();
        HandleMobileDayState(DayState.Result);
        UpdateDayHud();
    }

    private void CompleteResultPresentation()
    {
        if (_resultPresentationCompleted) return;
        _resultPresentationCompleted = true;
        _deferredResultPending = false;
        CancelHaircutInteraction(true);
        if (_game.Customers.Count > 0) _game.ForceCloseRemainingCustomers(_dayController.Stats);
        _dayController.FinalizeDayReputation();
        _game.SetFirstDayCompleteForDebug(true);
        ClearCustomerViews();
        ClearDayPayments();
        ShowResultPanel();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[Phase 7 DayStats] " + _dayController.Stats);
#endif
    }

    private void ShowResultPanel()
    {
        DayStats stats = _dayController.Stats;
        if (_resultPanel != null) _resultPanel.SetActive(true);
        if (_resultTitleLabel != null) _resultTitleLabel.text = "DAY " + stats.DayNumber + " 打烊";
        if (_resultSummaryLabel != null)
        {
            int dissatisfied = stats.UnhappyCustomers + stats.VeryUnhappyCustomers;
            int unfinished = stats.UnservedAtClose + stats.IncompleteAtClose;
            string ledger =
                "今日收支\n" +
                "订单收入          +" + stats.OrderIncome.ToString("N0") + "\n" +
                "小费收入          +" + stats.TipIncome.ToString("N0") + "\n";
            if (stats.OperatingRewardIncome > 0)
                ledger += "活动营业奖励      +" + stats.OperatingRewardIncome.ToString("N0") + "\n";
            if (stats.CompensationExpense > 0)
                ledger += "赔偿支出          -" + stats.CompensationExpense.ToString("N0") + "\n";
            if (stats.OtherOperatingExpense > 0)
                ledger += "其他营业支出      -" + stats.OtherOperatingExpense.ToString("N0") + "\n";
            ledger += "────────────\n" +
                "今日营业净收入    " + (stats.OperatingNetIncome >= 0 ? "+" : "") +
                stats.OperatingNetIncome.ToString("N0") + "\n\n" +
                "今日顾客\n" +
                "完成订单           " + stats.CompletedOrders + "\n" +
                "😊 开心            " + stats.HappyCustomers + "\n" +
                "🙂 正常            " + stats.NormalCustomers + "\n" +
                "😠 不满意          " + dissatisfied + "\n" +
                "未完成             " + unfinished + "\n\n" +
                "店铺声誉\n" +
                "⭐ " + stats.ReputationBefore.ToString("0.0") + " → " + stats.ReputationAfter.ToString("0.0");
            _resultSummaryLabel.text = ledger;
        }
        if (_resultContinueButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_resultContinueButton.gameObject);
    }

    private void ContinueToShop()
    {
        _dayController?.OpenClosedManagement();
    }

    private void BeginNextDay()
    {
        if (_dayController == null || _dayController.State != DayState.ClosedManagement) return;
        CancelHaircutInteraction(true);
        ClearCustomerViews();
        _game.ResetForNextDay();
        _dayController.PrepareNextDay();
        _coinBalanceLabel.text = _game.Balance.ToString("N0");
        ApplyOverview(true);
    }

    internal void RuntimeContinueToShop() => ContinueToShop();
    internal void RuntimeBeginNextDay() => BeginNextDay();
    internal void RuntimeStartBusinessDay() => StartBusinessDay();

    private void StartBusinessDay()
    {
        if (_dayController == null || _dayController.State != DayState.PreOpen) return;
        if (_mobileMode) SaveMobileCheckpoint(false);
        _dayController.StartBusiness();
    }

    private bool IsNormalGameplayMode => _phase7AcceptanceOptions == null &&
        _phase3WashAcceptanceOptions == null && _phase4AHaircutAcceptanceOptions == null &&
        _phase4A5AcceptanceOptions == null;

    private void TogglePause()
    {
        if (_dayController == null || (_dayController.State != DayState.Business &&
                                       _dayController.State != DayState.ClosingGrace)) return;
        bool paused = !_dayController.IsPaused;
        _dayController.SetPaused(paused);
        if (_mobileMode)
        {
            if (paused) SuspendMobileInput();
            else _mobileControls?.ResetInput();
        }
        else if (paused) CancelHaircutInteraction(false);
        if (_pausePanel != null) _pausePanel.SetActive(paused);
        if (paused && _resumeButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_resumeButton.gameObject);
    }

    private bool CanInteractWithSalon()
    {
        return _dayController != null && !_dayController.IsPaused &&
               (_dayController.State == DayState.Business || _dayController.State == DayState.ClosingGrace);
    }

    private void UpdateDayHud()
    {
        if (_dayController == null) return;
        if (_businessClockLabel != null)
        {
            if (_dayController.State == DayState.PreOpen)
                _businessClockLabel.text = "营业 未开始";
            else if (_dayController.State == DayState.ClosingGrace)
                _businessClockLabel.text = "营业 00:00";
            else
                _businessClockLabel.text = "营业 " + FormatClock(_dayController.BusinessRemainingTime);
        }
        if (_closingLabel != null && _dayController.State == DayState.ClosingGrace)
            _closingLabel.text = "打烊收尾 " + FormatClock(_dayController.ClosingGraceRemainingTime);
    }

    private void RefreshTopHudDay(int dayNumber)
    {
        int normalizedDay = Mathf.Max(1, dayNumber);
        string[] weekdays = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
        int calendarDay = _mobileMode ? normalizedDay : ((normalizedDay + 10) % 31) + 1;
        string weekday = weekdays[(normalizedDay + 1) % weekdays.Length];
        if (_weekdayLabel != null) _weekdayLabel.text = weekday;
        if (_weatherWeekdayLabel != null) _weatherWeekdayLabel.text = weekday;
        if (_topHudDayLabel != null) _topHudDayLabel.text = calendarDay.ToString();
    }

    private void RefreshTopHudSatisfaction()
    {
        if (_satisfaction == null) return;
        ApplySatisfactionSnapshot(new ShopSatisfactionSnapshot(_satisfaction.CurrentSatisfaction));
    }

    private void HandleSatisfactionChanged(ShopSatisfactionSnapshot snapshot) =>
        ApplySatisfactionSnapshot(snapshot);

    private void ApplySatisfactionSnapshot(ShopSatisfactionSnapshot snapshot)
    {
        if (_satisfactionLabel != null) _satisfactionLabel.text = snapshot.ValueText;
        if (_satisfactionFill != null) _satisfactionFill.fillAmount = snapshot.NormalizedProgress;
        if (_satisfactionSmile == null) return;
        Sprite moodSprite = snapshot.Mood == SatisfactionMood.Happy ? _satisfactionHappySprite :
            snapshot.Mood == SatisfactionMood.Neutral ? _satisfactionNeutralSprite : _satisfactionSadSprite;
        if (moodSprite != null) _satisfactionSmile.sprite = moodSprite;
    }

    private void ClearDayPayments()
    {
        _game?.Payments.ClearDayDrops();
    }

    private void ClearCustomerViews()
    {
        for (int i = _customerViews.Count - 1; i >= 0; i--)
            if (_customerViews[i] != null) Destroy(_customerViews[i].gameObject);
        _customerViews.Clear();
    }

    public static string FormatClock(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
    }

    private void SeedSalon()
    {
        int initial = Mathf.Clamp(FlowSettings.InitialCustomerCount, 0, FlowSettings.WaitingCapacity);
        for (int i = 0; i < initial; i++)
            SpawnRuntimeCustomer(i * .34f);
        ResetSpawnCooldown();
    }

    private void CreateCustomerView(CustomerModel customer, Vector3 position, Color hair, bool seated)
    {
        var root = BuildPerson("顾客 " + (customer.Id + 1), transform, position, hair, Hex("F2BC92"), Cream, seated);
        var collider = root.gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 1.2f, 0f);
        collider.size = new Vector3(2.2f, 3.4f, 2.2f);
        var view = root.gameObject.AddComponent<SalonCustomerView>();
        view.Owner = this;
        view.Customer = customer;
        view.BaseScale = root.localScale;
        view.InitializeHair(hair);
        var customerUi = root.gameObject.AddComponent<CustomerUIRootView>();
        customerUi.Initialize(customer, _camera);
        view.CustomerUI = customerUi;
        var orderDemand = customerUi.RequirementContainer.gameObject.AddComponent<OrderDemandBubbleView>();
        orderDemand.Initialize(customer, _camera);
        view.OrderDemand = orderDemand;
        var actionProgress = customerUi.RequirementContainer.gameObject.AddComponent<ActionProgressView>();
        actionProgress.Initialize(_camera);
        view.ActionProgress = actionProgress;
        var emotionView = customerUi.EmotionContainer.gameObject.AddComponent<CustomerEmotionView>();
        emotionView.Initialize(customer, _camera);
        view.EmotionView = emotionView;
        var feedback = root.gameObject.AddComponent<HaircutRuntimeFeedback>();
        feedback.Initialize(view, HaircutSettings, _camera);
        view.HaircutFeedback = feedback;
        view.LastStationPosition = position;
        _customerViews.Add(view);
    }

    private bool SpawnRuntimeCustomer(float entranceOffset = 0f)
    {
        int id = _nextCustomerId;
        string orderId = _mobileMode
            ? SalonMobileDayConfig.PickOrderForSpawn(DaySettings, id, _dayController.BusinessProgress)
            : DaySettings.PickOrder(Random.value);
        var needs = new List<ServiceType>(SalonOrderCatalog.Get(orderId));
        CustomerModel customer = _game.Spawn(id, needs);
        if (customer == null) return false;
        _nextCustomerId++;
        _dayController.Stats.RecordSpawn(customer);
        if (customer.CurrentNeed == ServiceType.Cut)
        {
            SalonTool[] haircutTools = _mobileMode
                ? SalonMobileDayConfig.GetHaircutToolsForSpawn(DaySettings.MobileDayNumber, id)
                : (id & 1) == 0
                    ? new[] { SalonTool.Scissors, SalonTool.ThinningShears }
                    : new[] { SalonTool.Clippers };
            _game.ConfigureHaircutOrder(customer, haircutTools);
        }
        Color[] hairColors = { Hex("C7569B"), Hex("F2B83D"), Hex("6E43A1"), Hex("353137"), Hex("E46D45") };
        CreateCustomerView(customer, EntrancePosition + new Vector3(-entranceOffset, 0f, 0f), hairColors[id % hairColors.Length], false);
        return true;
    }

    private void MaintainCustomerFlow(float dt)
    {
        if (_dayController == null) return;
        _spawnCooldown = Mathf.Max(0f, _spawnCooldown - Mathf.Max(0f, dt));
        if (!_dayController.CanSpawnCustomers) return;
        if (_spawnCooldown > 0f) return;
        TrafficDecision decision = _trafficDirector.Evaluate(_dayController.BusinessProgress,
            BuildTrafficSnapshot(), Random.value, _dayController.Reputation.CurrentStars);
        if (decision.ShouldSpawn && SpawnRuntimeCustomer())
        {
            _spawnCooldown = decision.SpawnInterval;
        }
        else
        {
            _spawnCooldown = decision.IsOverloaded ? 1.25f : .6f;
        }
    }

    private void ResetSpawnCooldown()
    {
        TrafficDecision decision = _trafficDirector == null
            ? new TrafficDecision(true, DaySettings.MaxSpawnInterval, DayPressurePhase.OpeningLight, false, false)
            : _trafficDirector.Evaluate(_dayController == null ? 0f : _dayController.BusinessProgress,
                BuildTrafficSnapshot(), Random.value, _dayController == null ? 3f : _dayController.Reputation.CurrentStars);
        _spawnCooldown = decision.SpawnInterval;
    }

    private TrafficSnapshot BuildTrafficSnapshot()
    {
        int active = 0;
        int waiting = 0;
        int occupied = 0;
        int angry = 0;
        if (_game != null)
        {
            for (int i = 0; i < _game.Customers.Count; i++)
            {
                CustomerModel customer = _game.Customers[i];
                if (customer.State == CustomerState.Exited) continue;
                active++;
                if (customer.State == CustomerState.Entering || customer.State == CustomerState.Waiting) waiting++;
                if (customer.Emotion == CustomerEmotion.Angry) angry++;
            }
            for (int i = 0; i < _game.Workstations.Count; i++)
                if (_game.Workstations[i].Occupied)
                    occupied++;
        }
        return new TrafficSnapshot(active, waiting, occupied, angry, 4);
    }

    private int CountActiveCustomers()
    {
        if (!_mobileMode) return BuildTrafficSnapshot().ActiveCustomers;
        // Finished and Leaving customers still have a visible exit journey.
        // Keep the day open until they reach Exited so the result panel does
        // not make the last customer disappear on the completion frame.
        int activeUntilExit = 0;
        foreach (CustomerModel customer in _game.Customers)
            if (customer.State != CustomerState.Exited) activeUntilExit++;
        return activeUntilExit;
    }

    private int CountUnfinishedCustomers()
    {
        if (!_mobileMode) return BuildTrafficSnapshot().ActiveCustomers;
        int unfinished = 0;
        foreach (CustomerModel customer in _game.Customers)
        {
            if (customer.State == CustomerState.Finished ||
                customer.State == CustomerState.Leaving ||
                customer.State == CustomerState.Exited) continue;
            unfinished++;
        }
        return unfinished;
    }

    private void HandleCustomerChanged(CustomerModel customer)
    {
        if (customer != null && customer.Id == _toastCustomerId &&
            _toastExpectedCustomerState.HasValue &&
            customer.State != _toastExpectedCustomerState.Value)
            ClearToast();
        _dayController?.Stats.RecordCustomerSnapshot(customer);
        _satisfaction?.TrySettleCustomer(customer);
        if (customer != null && customer.State == CustomerState.Serving && customer.Station >= 0 &&
            customer.Station < _game.Workstations.Count && _game.Workstations[customer.Station].Type == WorkstationType.Wash)
            _shampooTutorial?.TryBegin(customer);
        _shampooTutorial?.NotifyCustomerStageChanged(customer);
        for (int i = 0; i < _customerViews.Count; i++)
        {
            SalonCustomerView view = _customerViews[i];
            if (view == null || view.Customer != customer) continue;
            view.EmotionView?.Refresh();
            view.HaircutDemand?.Refresh();
            view.OrderDemand?.Refresh();
            view.SetTowelWrapped(customer.TowelWrapped);
            view.SetWashVisual(customer);
            if (_game.SelectedCustomer == customer && _toolBar != null && _toolBar.activeSelf &&
                customer.State != CustomerState.Finished && customer.State != CustomerState.Leaving &&
                customer.ActiveServiceAction == ActiveServiceAction.None &&
                (_haircutInteraction == null || _haircutInteraction.State != HaircutInteractionState.Holding) &&
                !HasArmedToolSelection(_selectedToolIndex, _selectedServiceAction,
                    _haircutInteraction == null ? HaircutInteractionState.Idle : _haircutInteraction.State))
                BuildToolBar(customer);
            break;
        }
        RefreshTutorialPresentation();
    }

    private void HandleViewChanged(SalonViewState state, CustomerModel customer)
    {
        if (state == SalonViewState.WorkstationFocus && customer != null)
            ApplyFocus(customer);
        else
            ApplyOverview(false);
    }

    private Image BuildDemandBubble(Transform parent, ServiceType service)
    {
        var canvasObject = new GameObject("需求气泡", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        canvasObject.transform.localPosition = new Vector3(0f, 3.2f, 0f);
        canvasObject.transform.rotation = _camera.transform.rotation;
        canvasObject.transform.localScale = Vector3.one * .0115f;
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;
        var rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(130f, 130f);
        var circle = SalonUiFactory.GetCircleSprite();
        var track = UiPanel("Ring Track", canvasObject.transform, Hex("35423D"), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(118f, 118f));
        track.GetComponent<Image>().sprite = circle;
        var fill = UiPanel("Ring Progress", canvasObject.transform, Hex("58C56B"), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(108f, 108f)).GetComponent<Image>();
        fill.sprite = circle;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = 2;
        fill.fillClockwise = false;
        fill.fillAmount = 1f;
        UiPanel("Bubble", canvasObject.transform, Hex("FFF5E6"), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(78f, 78f)).GetComponent<Image>().sprite = circle;
        UiLabel(ServiceSymbol(service), canvasObject.transform, 48, Ink, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(72f, 72f));
        SalonUiFactory.MakeClickThrough(canvasObject);
        return fill;
    }

    private void ApplyFocus(CustomerModel customer)
    {
        if (_mobileMode)
        {
            _toolBar.SetActive(false);
            return;
        }
        CancelHaircutInteraction(true);
        foreach (var plate in _selectionPlates.Values) plate.SetActive(false);
        if (customer.Station >= 0 && _selectionPlates.TryGetValue(customer.Station, out var selected)) selected.SetActive(true);
        Vector3 target = customer.Station >= 0 && _customerSeatAnchors.TryGetValue(customer.Station, out var marker) ? marker.position : FindCustomerPosition(customer);
        _cameraPositionTarget = _overviewCameraPosition + new Vector3(target.x * .23f, -.8f, target.z * .2f);
        _cameraSizeTarget = 7.25f;
        _playerTarget = customer.Station >= 0 && _playerServiceAnchors.TryGetValue(customer.Station, out var playerAnchor)
            ? playerAnchor.position
            : target + new Vector3(1.35f, 0f, -.4f);
        BuildToolBar(customer);
        _toolBar.SetActive(true);
        RefreshTutorialPresentation();
    }

    private void ApplyOverview(bool immediate)
    {
        if (!_mobileMode) CancelHaircutInteraction(true);
        foreach (var plate in _selectionPlates.Values) plate.SetActive(false);
        _cameraPositionTarget = _overviewCameraPosition;
        _cameraSizeTarget = 8.35f;
        _toolBar.SetActive(false);
        if (_toastRoutine == null && _hintLabel != null)
        {
            _hintLabel.text = string.Empty;
            _hintLabel.transform.parent.gameObject.SetActive(false);
        }
        if (immediate)
        {
            _camera.transform.position = _overviewCameraPosition;
            _camera.transform.rotation = _overviewCameraRotation;
            _camera.orthographicSize = _cameraSizeTarget;
        }
    }

    private void BuildToolBar(CustomerModel customer)
    {
        if (_mobileMode) return;
        Transform buttonParent = ToolButtonParent();
        for (int i = buttonParent.childCount - 1; i >= 0; i--)
        {
            var child = buttonParent.GetChild(i);
            if (_toolButtonRoot != null)
                SalonUiFactory.HideAndDestroy(child.gameObject);
            else
            {
                Transform focusRoot = SalonUiFactory.DirectChildRoot(_focusLabel.transform, _toolBar.transform);
                if (child != focusRoot) SalonUiFactory.HideAndDestroy(child.gameObject);
            }
        }
        ResetToolSelection();
        if (customer.State == CustomerState.Finished)
        {
            _focusLabel.text = customer.ServiceResult == CustomerServiceResult.Failed
                ? "发生事故 · 服务已终止"
                : customer.ServiceFeedback == CustomerServiceFeedback.Dissatisfied
                    ? "订单完成 · 顾客不满意" : "订单完成 · 收入已结算";
        }
        else if (customer.State == CustomerState.Waiting || customer.Station < 0)
        {
            _focusLabel.text = customer.CurrentNeed == ServiceType.Wash ? "订单目标：洗头 · 可安排到洗头床" :
                customer.CurrentNeed == ServiceType.Perm ? "订单目标：烫发 · 只能安排到烫发工位" :
                "订单目标：" + (customer.CurrentNeed == ServiceType.Cut ? "剪发" :
                    customer.CurrentNeed == ServiceType.Dye ? "染发" : "吹发") + " · 可安排到理发椅";
        }
        else if (customer.State == CustomerState.MovingToStation)
        {
            _focusLabel.text = "顾客正在前往工位";
        }
        else if (customer.Station >= 0 && customer.Station < _game.Workstations.Count)
        {
            WorkstationModel station = _game.Workstations[customer.Station];
            if (station.Type == WorkstationType.Wash)
            {
                AddServiceExecutionButton(customer, ServiceExecutionType.Wash, "🫧", "洗头");
            }
            else if (station.Type == WorkstationType.Haircut)
            {
                string stationHint = customer.IsDyeCleanupReady ? "显色完成 · 请处理染膏" :
                    customer.ProcessStage == ServiceProcessStage.DyeProcessing
                        ? "染发中 · 玩家可去服务其他顾客"
                        : "理发工位 · 请选择工具";
                bool autoBlowAwaitingCollection = customer.AutoBlowRunning ||
                    customer.AutoBlowSafetyStopped;
                bool canOperate = !customer.IsProcessing &&
                    customer.ActiveServiceAction == ActiveServiceAction.None &&
                    !autoBlowAwaitingCollection &&
                    (customer.ServiceExecution == null ||
                     customer.ServiceExecution.State != ServiceExecutionState.Executing);
                int toolIndex = 1;
                foreach (SalonTool stationTool in HaircutStationTools)
                {
                    float x = -252f + (toolIndex - 1) * 126f;
                    if (stationTool == SalonTool.BlowDryer)
                    {
                        if (_game.HasAutoBlowStand)
                        {
                            string label = autoBlowAwaitingCollection ? "收取吹发" : "自动吹风";
                            AddActionButton("≋", label, toolIndex, x,
                                () => HandleBlowAction(customer),
                                canOperate || autoBlowAwaitingCollection, 116f);
                        }
                        else
                        {
                            AddServiceAction("≋", "吹风机", toolIndex, x,
                                ActiveServiceAction.ManualBlow, canOperate, 116f);
                        }
                    }
                    else if (stationTool == SalonTool.DyeBottle && customer.IsDyeCleanupReady)
                        AddActionButton("◆", "处理染膏", toolIndex, x, () =>
                        {
                            if (!_game.ResolveDyeCleanup(customer)) return;
                            ShowToast("染膏处理完成");
                            FindCustomerView(customer)?.OrderDemand?.Refresh();
                        }, true, 116f);
                    else if (stationTool == SalonTool.DyeBottle)
                        AddServiceAction("◆", "染发刷", toolIndex, x,
                            ActiveServiceAction.ApplyDye, canOperate, 116f);
                    else
                        AddTool(stationTool == SalonTool.Clippers ? "▣" : "✂",
                            ToolName(stationTool), toolIndex, x, stationTool, canOperate, 116f);
                    toolIndex++;
                }
                _focusLabel.text = stationHint;
            }
            else if (station.Type == WorkstationType.Perm)
            {
                _focusLabel.text = customer.IsProcessing ? "烫发中 · 玩家可去服务其他顾客" :
                    "选择烫发工具 · 长按顾客上卷";
                if (!customer.IsProcessing)
                    AddServiceAction("◎", "开始烫发", 1, 0f, ActiveServiceAction.ApplyPerm, true, 220f);
            }
            else _focusLabel.text = "当前工位暂不可操作";
            if (customer.OrderRequirementsCompleted && !customer.ExitReady)
                _focusLabel.text = "订单完成 ✓ · " + ExitBlockerText(customer.ExitBlockReason);
        }
        else
        {
            _focusLabel.text = "当前服务暂不可操作";
        }
        RefreshToolButtons();
        RefreshTutorialPresentation();
    }

    private void AddServiceExecutionButton(
        CustomerModel customer, ServiceExecutionType type, string symbol, string label,
        int index = 0, float x = 0f, float width = 220f, bool interactable = true)
    {
        ServiceExecution execution = customer.ServiceExecution;
        bool executing = execution != null && execution.State == ServiceExecutionState.Executing;
        string buttonLabel = executing ? label + "中..." : label;
        _focusLabel.text = executing ? buttonLabel : "点击开始" + label;
        AddActionButton(symbol, buttonLabel, index, x, () =>
        {
            if (!_game.BeginServiceExecution(customer, type)) return;
            FindCustomerView(customer)?.ActionProgress?.SetServiceExecution(customer.ServiceExecution);
            BuildToolBar(customer);
        }, interactable && !executing, width);
    }

    public static ActiveServiceAction GetDefaultHoldAction(CustomerModel customer)
    {
        return ActiveServiceAction.None;
    }

    private void AddServiceAction(string symbol, string label, int index, float x, ActiveServiceAction action,
        bool interactable = true, float width = 178f)
    {
        AddActionButton(symbol, label, index, x, () =>
        {
            if (!interactable) return;
            CustomerModel customer = _game.SelectedCustomer;
            if (customer == null) return;
            if (action == ActiveServiceAction.WrapTowel || action == ActiveServiceAction.RemoveTowel)
            {
                ResetToolSelection();
                ServiceActionResult quickResult = _game.PerformQuickAction(customer, action);
                if (quickResult != ServiceActionResult.QuickActionCompleted)
                {
                    ShowToast("当前快捷动作不可用", 1f);
                    return;
                }
                _shampooTutorial?.NotifyCustomerStageChanged(customer);
                SalonCustomerView view = FindCustomerView(customer);
                if (action == ActiveServiceAction.WrapTowel) view?.PlayTowelSnap();
                else view?.PlayTowelRemoval();
                BuildToolBar(customer);
                return;
            }
            if (action == ActiveServiceAction.Shower || action == ActiveServiceAction.Shampoo)
            {
                if (action == ActiveServiceAction.Shampoo)
                    FindCustomerView(customer)?.PlayShampooSqueeze();
            }
            _selectedToolIndex = index;
            _selectedServiceAction = action;
            PlayerContext context = _game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId);
            SalonTool? selectedTool = action == ActiveServiceAction.Shampoo ? SalonTool.Shampoo :
                action == ActiveServiceAction.ManualBlow ? SalonTool.BlowDryer :
                action == ActiveServiceAction.ApplyDye ? SalonTool.DyeBottle :
                action == ActiveServiceAction.ApplyPerm ? SalonTool.PermSolution : (SalonTool?)null;
            context.SelectServiceAction(customer, action, selectedTool);
            FindCustomerView(customer)?.ActionProgress?.ArmTool();
            _shampooTutorial?.NotifyToolSelected(customer, action);
            _focusLabel.text = SelectedActionHint(action);
            RefreshToolButtons();
            RefreshTutorialPresentation();
        }, interactable, width);
        Transform added = ToolButtonParent().Find("Tool " + index);
        if (added != null) added.gameObject.AddComponent<SalonTutorialToolButton>().Action = action;
    }

    private void AddActionButton(string symbol, string label, int index, float x,
        UnityEngine.Events.UnityAction action, bool interactable = true, float width = 178f)
    {
        var button = UiButton(symbol + "\n" + label, ToolButtonParent(), new Vector2(.5f, .5f),
            new Vector2(x, -24f), new Vector2(width, 114f),
            interactable ? Cream : new Color(.45f, .42f, .39f, .55f), action);
        button.interactable = interactable;
        button.gameObject.name = "Tool " + index;
        StyleToolButton(button, symbol, label, width);
    }

    private void HandleBlowAction(CustomerModel customer)
    {
        if (customer == null || customer != _game.SelectedCustomer) return;
        if (!customer.AutoBlowRunning && !customer.AutoBlowSafetyStopped)
        {
            if (_game.StartAutoBlow(customer))
            {
                ShowToast("自动吹发已启动");
                BuildToolBar(customer);
            }
            return;
        }

        BlowResult result = _game.FinishAutoBlow(customer);
        if (result == BlowResult.Undone)
            ShowToast("自动吹发还没完成");
        else if (result == BlowResult.Good)
            ShowToast("吹发完成");
        else if (result == BlowResult.Minor)
            ShowToast("吹发轻度超时");
        else if (result == BlowResult.Moderate)
            ShowToast("自动设备已安全停机");
        if (customer.State == CustomerState.Finished)
            StartCoroutine(ReturnToOverviewAfterResult(FindCustomerView(customer), HaircutSettings.ResultFeedbackSeconds));
        else BuildToolBar(customer);
    }

    private void AddTool(string symbol, string label, int index, float x, SalonTool? tool,
        bool interactable = true, float width = 178f)
    {
        var button = UiButton(symbol + "\n" + label, ToolButtonParent(), new Vector2(.5f, .5f),
            new Vector2(x, -24f), new Vector2(width, 114f),
            interactable ? Cream : new Color(.45f, .42f, .39f, .55f), () =>
        {
            if (!interactable) return;
            _selectedToolIndex = index;
            if (tool.HasValue && _game.SelectedCustomer != null && _game.SelectedCustomer.Station >= 0 &&
                _game.Workstations[_game.SelectedCustomer.Station].Type == WorkstationType.Haircut)
            {
                _haircutInteraction.SelectTool(tool.Value);
                PlayerContext context = _game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId);
                context.SelectServiceAction(_game.SelectedCustomer, ActiveServiceAction.None, tool.Value);
                FindCustomerView(_game.SelectedCustomer)?.ActionProgress?.ArmTool();
                _focusLabel.text = "已选择" + ToolName(tool.Value) + " · 长按顾客开始操作";
            }
            RefreshToolButtons();
        });
        button.interactable = interactable;
        button.gameObject.name = "Tool " + index;
        StyleToolButton(button, symbol, label, width);
    }

    private Transform ToolButtonParent() => _toolButtonRoot != null ? _toolButtonRoot : _toolBar.transform;

    private static void StyleToolButton(Button button, string symbol, string label, float width)
    {
        Text text = button.GetComponentInChildren<Text>();
        text.color = Ink;
        text.fontSize = width < 140f ? 20 : 24;
        string iconPath = label.Contains("剪") ? "DemandBubble/tool-scissors" :
            label.Contains("吹风") ? "DemandBubble/tool-dryer" :
            label.Contains("染") ? "DemandBubble/tool-brush" : null;
        if (iconPath == null)
        {
            text.text = (symbol == "🫧" ? "≈" : symbol) + "\n" + label;
            return;
        }
        text.text = label;
        RectTransform textRect = (RectTransform)text.transform;
        textRect.anchoredPosition = new Vector2(0f, -34f);
        textRect.sizeDelta = new Vector2(width - 12f, 38f);
        HudArtwork("Tool Icon", button.transform, iconPath, new Vector2(0f, 18f), new Vector2(56f, 56f));
    }

    private void RefreshToolButtons()
    {
        foreach (Transform child in ToolButtonParent())
        {
            if (!child.name.StartsWith("Tool ")) continue;
            int index = int.Parse(child.name.Substring(5));
            Button button = child.GetComponent<Button>();
            child.GetComponent<Image>().color = button != null && !button.interactable
                ? new Color(.45f, .42f, .39f, .55f)
                : index == _selectedToolIndex ? Gold : Cream;
            child.localScale = index == _selectedToolIndex ? Vector3.one * 1.06f : Vector3.one;
        }
    }

    private void RefreshTutorialPresentation()
    {
        if (_hintLabel == null) return;
        if (_shampooTutorial == null || !_shampooTutorial.IsActive)
        {
            _lastTutorialToastStep = _shampooTutorial == null
                ? ShampooTutorialStep.Inactive : _shampooTutorial.Step;
            if (_toastRoutine == null)
            {
                _hintLabel.text = string.Empty;
                _hintLabel.transform.parent.gameObject.SetActive(false);
            }
            return;
        }
        if (_game == null || _game.SelectedCustomer == null ||
            _game.SelectedCustomer.Id != _shampooTutorial.CustomerId)
        {
            if (_toastRoutine == null)
            {
                _hintLabel.text = string.Empty;
                _hintLabel.transform.parent.gameObject.SetActive(false);
            }
            return;
        }
        if (_toastRoutine == null)
        {
            if (_lastTutorialToastStep != _shampooTutorial.Step &&
                !string.IsNullOrEmpty(_shampooTutorial.Prompt))
            {
                _lastTutorialToastStep = _shampooTutorial.Step;
                ShowToast(_shampooTutorial.Prompt);
            }
            else
            {
                _hintLabel.text = string.Empty;
                _hintLabel.transform.parent.gameObject.SetActive(false);
            }
        }
        if (_shampooTutorial.Step == ShampooTutorialStep.MoveToNextStation)
        {
            foreach (var pair in _selectionPlates)
                if (pair.Key >= 0 && pair.Key < _game.Workstations.Count &&
                    _game.Workstations[pair.Key].Type == WorkstationType.Haircut)
                    pair.Value.SetActive(true);
        }
        if (_toolBar == null || !_toolBar.activeSelf) return;
        foreach (Transform child in ToolButtonParent())
        {
            var marker = child.GetComponent<SalonTutorialToolButton>();
            if (marker == null) continue;
            bool expected = _shampooTutorial.IsToolExpected(marker.Action);
            var image = child.GetComponent<Image>();
            if (image != null && marker.Action != _selectedServiceAction)
                image.color = expected ? Gold : new Color(Cream.r, Cream.g, Cream.b, .38f);
            Transform oldFinger = child.Find("Tutorial Finger");
            if (oldFinger != null) SalonUiFactory.HideAndDestroy(oldFinger.gameObject);
            if (expected && (_shampooTutorial.Step == ShampooTutorialStep.SelectShowerForWet ||
                             _shampooTutorial.Step == ShampooTutorialStep.SelectShampoo ||
                             _shampooTutorial.Step == ShampooTutorialStep.SelectShowerForRinse ||
                             _shampooTutorial.Step == ShampooTutorialStep.SelectTowel))
            {
                Text finger = UiLabel("☝", child, 35, Color.white, TextAnchor.MiddleCenter,
                    new Vector2(65f, 50f), new Vector2(48f, 48f), Coral);
                finger.transform.parent.gameObject.name = "Tutorial Finger";
                finger.transform.parent.gameObject.AddComponent<SalonTutorialFingerPulse>();
                SalonUiFactory.MakeClickThrough(finger.transform.parent.gameObject);
            }
        }
    }

    private void ShowToast(string message, float duration = 1.25f)
    {
        if (_hintLabel == null) return;
        ClearToast(false);
        int version = ++_toastVersion;
        _hintLabel.transform.parent.gameObject.SetActive(true);
        _toastRoutine = StartCoroutine(ToastRoutine(message, duration, version));
    }

    private void ShowCustomerStateToast(
        string message, CustomerModel customer, CustomerState expectedState, float duration = 1.25f)
    {
        ShowToast(message, duration);
        _toastCustomerId = customer == null ? -1 : customer.Id;
        _toastExpectedCustomerState = expectedState;
    }

    private void ClearToast(bool refreshPresentation = true)
    {
        _toastVersion++;
        if (_toastRoutine != null) StopCoroutine(_toastRoutine);
        _toastRoutine = null;
        _toastCustomerId = -1;
        _toastExpectedCustomerState = null;
        if (_hintLabel != null)
        {
            _hintLabel.text = string.Empty;
            _hintLabel.transform.parent.gameObject.SetActive(false);
        }
        if (refreshPresentation) RefreshTutorialPresentation();
    }

    private IEnumerator ToastRoutine(string message, float duration, int version)
    {
        _hintLabel.text = message ?? string.Empty;
        yield return new WaitForSecondsRealtime(GetToastDuration(duration));
        if (version != _toastVersion) yield break;
        _toastRoutine = null;
        _toastCustomerId = -1;
        _toastExpectedCustomerState = null;
        RefreshTutorialPresentation();
    }

    private void UpdateCustomerViews()
    {
        for (int viewIndex = _customerViews.Count - 1; viewIndex >= 0; viewIndex--)
        {
            SalonCustomerView view = _customerViews[viewIndex];
            if (view == null || view.Customer == null)
            {
                _customerViews.RemoveAt(viewIndex);
                continue;
            }
            CustomerModel customer = view.Customer;
            Vector3 target;
            int waitingSlot = -1;
            if (customer.State == CustomerState.Entering || customer.State == CustomerState.Waiting)
            {
                waitingSlot = _game.GetWaitingSlot(customer);
                target = waitingSlot >= 0 ? WaitingPositions[waitingSlot] : view.transform.position;
            }
            else if (customer.State == CustomerState.MovingToStation || customer.State == CustomerState.Serving)
            {
                if (customer.Station >= 0 && _customerSeatAnchors.TryGetValue(customer.Station, out var stationTarget))
                {
                    bool usesElevatedWashAnchor = customer.Station < _game.Workstations.Count &&
                                                  _game.Workstations[customer.Station].Type == WorkstationType.Wash;
                    float stationHeight = usesElevatedWashAnchor ? stationTarget.position.y : view.transform.position.y;
                    view.LastStationPosition = new Vector3(stationTarget.position.x, stationHeight, stationTarget.position.z);
                    _stationUiAnchors.TryGetValue(customer.Station, out var serviceUiAnchor);
                    view.RememberServiceAnchor(customer.Station, serviceUiAnchor);
                    view.SetServiceQueueAnchor(_stationQueueAnchors.TryGetValue(customer.Station, out var queueAnchor)
                        ? queueAnchor
                        : null);
                }
                target = view.LastStationPosition;
            }
            else if (customer.State == CustomerState.Finished)
            {
                target = view.LastStationPosition;
            }
            else
            {
                target = ExitPosition;
            }

            Vector3 customerPositionBeforeMove = view.transform.position;
            view.MoveAlongCurrentRoute(customer.State, target, 5.2f, Time.deltaTime);
            // The cut-station collision volumes protect active customers and
            // the player. They must not roll back an exit route: the door path
            // deliberately leaves the chair through that footprint.
            if (customer.State != CustomerState.Leaving && customer.State != CustomerState.Exited &&
                IsAnyCutStationMovementBlocked(view.transform.position))
                view.transform.position = customerPositionBeforeMove;
            // A compatible chair can never be the source of a wrong-station question mark.
            // Enforce this immediately before presentation as well as at model arrival so a
            // late event cannot make the invalid reaction visible for even one frame.
            if (customer.State == CustomerState.MovingToStation || customer.State == CustomerState.Serving)
                _game.ClearCompatibleStationConfusion(customer);
            if (customer.State == CustomerState.MovingToStation && view.IsAtMovementDestination)
                _game.ConfirmStationArrival(customer);
            WorkstationType poseStation = customer.Station >= 0 && customer.Station < _game.Workstations.Count
                ? _game.Workstations[customer.Station].Type
                : WorkstationType.Haircut;
            view.ApplyStationPose(customer.State, view.IsAtMovementDestination, poseStation);
            if (view.IsAtMovementDestination && _cutStations.TryGetValue(customer.Station, out var poseStationData))
                view.transform.rotation = Quaternion.LookRotation(poseStationData.Layout.CustomerFacing, Vector3.up);
            if (customer.State == CustomerState.Exited)
            {
                view.CustomerUI?.SetAnchor(null);
                view.gameObject.SetActive(false);
                _customerViews.RemoveAt(viewIndex);
                Destroy(view.gameObject);
                continue;
            }
            UpdateCustomerUiAnchor(view, customer, waitingSlot);
            view.SetTowelWrapped(customer.TowelWrapped);
            view.SetWashVisual(customer);
            view.ApplyDyeFailureVisual(customer.HasDyeFailed);
            if (customer.IsProcessing)
            {
                bool cleanupReady = customer.IsDyeCleanupReady;
                string label = cleanupReady ? "等待处理染膏" :
                    customer.ProcessStage == ServiceProcessStage.DyeProcessing ? "染发中" : "烫发中";
                float progress = cleanupReady ? customer.DyeCleanupRiskProgress : customer.ProcessingProgress;
                Color progressColor = cleanupReady && progress >= .7f
                    ? SalonPalette.Danger : cleanupReady ? SalonPalette.Warning : SalonPalette.Success;
                view.ActionProgress?.ClearProgress();
                view.ActionProgress?.SetBackgroundWaitProgress(
                    label, progress, progressColor);
            }
            else if (customer.ServiceExecution != null &&
                customer.ServiceExecution.State == ServiceExecutionState.Executing)
            {
                view.ActionProgress?.SetServiceExecution(customer.ServiceExecution);
            }
            else if (customer.CurrentNeed == ServiceType.Wash &&
                (customer.WashStage == WashStage.FoamWait || customer.WashStage == WashStage.ReadyToRinse) &&
                customer.BackgroundTask.State == BackgroundTaskState.Running)
            {
                float ideal = Mathf.Max(.01f, ServiceSettings.FoamOptimalStart);
                float elapsed = customer.BackgroundTask.Elapsed;
                Color color = elapsed < ideal ? SalonPalette.Warning :
                    elapsed < ServiceSettings.FoamMinorLateThreshold ? SalonPalette.Success :
                    elapsed < ServiceSettings.FoamModerateLateThreshold ? SalonPalette.Warning : SalonPalette.Danger;
                view.ActionProgress?.SetProgressForService(
                    ServiceType.Wash, "♨", elapsed / ideal, color);
            }
            else if (customer.CurrentNeed == ServiceType.Dry &&
                customer.BackgroundTask.State == BackgroundTaskState.Running)
            {
                float progress = customer.BackgroundTask.Elapsed / Mathf.Max(.01f, ServiceSettings.BlowLateEnd);
                Color color = customer.BlowStage == BlowStage.Minor ? SalonPalette.Warning :
                    customer.BlowStage == BlowStage.Moderate || customer.BlowStage == BlowStage.SafetyStopped
                        ? SalonPalette.Danger : SalonPalette.Success;
                view.ActionProgress?.SetProgressForService(
                    ServiceType.Dry, "♨", progress, color);
            }
            else if (view.ActionProgress != null &&
                !(_activeHaircutView == view && _haircutInteraction != null &&
                  _haircutInteraction.State == HaircutInteractionState.Holding) &&
                !(_activeServiceView == view && customer.ActiveServiceAction != ActiveServiceAction.None))
            {
                view.ActionProgress.ClearProgress();
                view.ActionProgress.ClearBackgroundWaitProgress();
            }
            Vector3 targetScale = customer == _game.SelectedCustomer ? view.BaseScale * 1.08f : view.BaseScale;
            view.transform.localScale = Vector3.Lerp(view.transform.localScale, targetScale, 1f - Mathf.Exp(-16f * Time.deltaTime));
        }
        TryFinalizeDeferredResult();
    }

    private void UpdateCustomerUiAnchor(SalonCustomerView view, CustomerModel customer, int waitingSlot)
    {
        if (view.CustomerUI == null) return;
        if (customer.State == CustomerState.Waiting && view.IsAtMovementDestination &&
            _waitingUiAnchors.TryGetValue(waitingSlot, out var waitingAnchor))
        {
            view.CustomerUI.SetAnchor(waitingAnchor);
        }
        else if (customer.State == CustomerState.Serving && view.IsAtMovementDestination &&
            _stationUiAnchors.TryGetValue(customer.Station, out var seatAnchor))
        {
            view.CustomerUI.SetAnchor(seatAnchor);
        }
        else if (customer.State == CustomerState.Finished && view.LastServiceUiAnchor != null)
        {
            view.CustomerUI.SetAnchor(view.LastServiceUiAnchor);
        }
        else if (customer.State == CustomerState.Entering || customer.State == CustomerState.MovingToStation ||
            customer.State == CustomerState.Serving)
        {
            view.CustomerUI.SetMobileAnchor(view.transform.position);
        }
        else
        {
            view.CustomerUI.SetAnchor(null);
        }
        view.CustomerUI.SetSelected(ShouldShowCustomerStatusAtStation(
            customer, customer == _game.SelectedCustomer, view.IsAtMovementDestination));
        if (_mobileMode)
        {
            bool waiting = customer.State == CustomerState.Waiting || customer.State == CustomerState.Entering;
            view.CustomerUI.VisualRoot.localScale = Vector3.one * (waiting ? .65f : 1f);
            view.CustomerUI.UrgencyContainer.gameObject.SetActive(!waiting);
        }
    }

    public bool HandleHaircutPointerDown(SalonCustomerView view, int pointerId)
    {
        if (_mobileMode) return false;
        if (view == null || view.Customer == null || _game == null || _haircutInteraction == null) return false;
        if (_game.ViewState != SalonViewState.WorkstationFocus || _game.SelectedCustomer != view.Customer) return false;
        var customer = view.Customer;
        if (customer.State == CustomerState.MovingToStation && view.IsAtMovementDestination)
            _game.ConfirmStationArrival(customer);
        if (customer.State != CustomerState.Serving) return false;
        if (!view.IsAtMovementDestination) return false;
        if (_cutStations.TryGetValue(customer.Station, out var cutStation))
        {
            CutStationAlignmentResult alignment = cutStation.ValidateServiceStart(
                view.transform.position, view.transform.forward,
                _player.position, _player.forward, .14f, .9f);
            if (!alignment.IsAligned)
            {
                ShowToast("人物正在对齐剪发工位，请稍候");
                return false;
            }
        }
        _game.ClearCompatibleStationConfusion(customer);
        if (_selectedServiceAction != ActiveServiceAction.None)
        {
            PlayerContext context = _game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId);
            // The per-player context is the newest source when it still owns this customer.
            // Arrival/focus refreshes may clear that context after a toolbar button was armed;
            // in that case restore it from the visible toolbar state instead of dropping the hold.
            ActiveServiceAction selectedAction = context.OwnsSelection(customer) &&
                context.ActiveAction != ActiveServiceAction.None
                    ? context.ActiveAction
                    : _selectedServiceAction;
            if (!context.OwnsSelection(customer))
            {
                context.SelectServiceAction(customer, selectedAction,
                    ToolForServiceAction(selectedAction));
            }
            _selectedServiceAction = selectedAction;
            bool began = selectedAction == ActiveServiceAction.ManualBlow
                ? _game.StartManualBlow(customer)
                : selectedAction == ActiveServiceAction.ApplyDye || selectedAction == ActiveServiceAction.ApplyPerm
                    ? _game.BeginProcessingServiceApply(customer)
                    : false;
            if (!began)
            {
                if (selectedAction == ActiveServiceAction.Shampoo &&
                    _game.ApplyWrongStationTool(customer, SalonTool.Shampoo))
                {
                    ShowToast("错误工位使用工具 · 顾客不满");
                    return true;
                }
                return false;
            }
            _activeServiceView = view;
            _activeServicePointer = pointerId;
            view.ActionProgress?.ClearProgress();
            ServiceType? selectedService = ServiceForAction(selectedAction);
            if (selectedService.HasValue)
                view.ActionProgress?.SetProgressForService(selectedService.Value,
                    GetActiveServiceActionSymbol(selectedAction), 0f, SalonPalette.Warning);
            return true;
        }
        if (customer.Station < 0 || customer.Station >= _game.Workstations.Count ||
            _game.Workstations[customer.Station].Type != WorkstationType.Haircut) return false;
        PlayerContext haircutContext = _game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId);
        if (!haircutContext.OwnsSelection(customer))
        {
            ResetToolSelection();
            BuildToolBar(customer);
            return false;
        }
        if (!_haircutInteraction.SelectedTool.HasValue) return false;
        SalonTool selectedTool = _haircutInteraction.SelectedTool.Value;
        if (customer.HairStage == HairStage.Overcut) return false;
        if (!_haircutInteraction.BeginHold()) return false;
        if (!_game.BeginHaircutAction(customer, selectedTool, HaircutSettings))
        {
            _haircutInteraction.CancelHold();
            return false;
        }
        _activeHaircutView = view;
        _activeHaircutPointer = pointerId;
        view.HaircutDemand?.ClearHoldProgress();
        view.ActionProgress?.ClearProgress();
        view.HaircutFeedback.BeginHold(selectedTool);
        _focusLabel.text = "正在剪发 · 在绿色区间松手";
        return true;
    }

    public void HandleHaircutPointerUp(SalonCustomerView view, int pointerId)
    {
        if (_activeServiceView == view && _activeServicePointer == pointerId)
        {
            BlowResult blowResult = view.Customer.ActiveServiceAction == ActiveServiceAction.ManualBlow
                ? _game.EndManualBlowHold(view.Customer) : BlowResult.None;
            if (blowResult == BlowResult.None) _game.CancelActiveServiceAction(view.Customer);
            view.ActionProgress?.ClearProgress();
            _activeServiceView = null;
            _activeServicePointer = int.MinValue;
            _focusLabel.text = blowResult == BlowResult.Undone
                ? "吹发尚未完成 · 继续长按可累积进度"
                : blowResult == BlowResult.None ? "动作未完成 · 请重新长按" : "吹发完成";
            if (_game.SelectedCustomer == view.Customer) BuildToolBar(view.Customer);
            return;
        }
        if (_haircutInteraction == null || _haircutInteraction.State != HaircutInteractionState.Holding) return;
        if (view != _activeHaircutView || pointerId != _activeHaircutPointer) return;
        _haircutInteraction.ReleaseHold();
        _activeHaircutPointer = int.MinValue;
    }

    public void HandleHaircutPointerCancel(SalonCustomerView view, int pointerId)
    {
        if (_activeServiceView == view && _activeServicePointer == pointerId)
        {
            if (view.Customer.ActiveServiceAction == ActiveServiceAction.ManualBlow)
                _game.EndManualBlowHold(view.Customer);
            else _game.CancelActiveServiceAction(view.Customer);
            view.ActionProgress?.ClearProgress();
            _activeServiceView = null;
            _activeServicePointer = int.MinValue;
            return;
        }
        if (view != _activeHaircutView || pointerId != _activeHaircutPointer) return;
        CancelHaircutInteraction(false);
    }

    private void UpdateHaircutInteraction()
    {
        if (_haircutInteraction == null || _haircutInteraction.State != HaircutInteractionState.Holding) return;
        if (_game.ViewState != SalonViewState.WorkstationFocus)
        {
            CancelHaircutInteraction(false);
            ApplyOverview(false);
            return;
        }
        if (_activeHaircutPointer >= 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.fingerId == _activeHaircutPointer && touch.phase == TouchPhase.Canceled)
                {
                    CancelHaircutInteraction(false);
                    return;
                }
            }
        }
        if (_activeHaircutView == null || _game.SelectedCustomer != _activeHaircutView.Customer ||
            _activeHaircutView.Customer.State != CustomerState.Serving)
        {
            CancelHaircutInteraction(false);
            return;
        }
        _haircutInteraction.Tick(Time.deltaTime);
        if (_haircutInteraction.State == HaircutInteractionState.Holding)
        {
            _activeHaircutView.HaircutFeedback.TickHold(_haircutInteraction.HoldTime, _haircutInteraction.HoldProgress);
            bool ready = _haircutInteraction.HoldTime >= HaircutSettings.GetPerfectMin(_haircutInteraction.SelectedTool.Value);
            if (_haircutInteraction.HoldProgress >= .3f &&
                _game.IsWrongHaircutHoldAction(_activeHaircutView.Customer, _haircutInteraction.HoldTime))
            {
                CancelHaircutInteraction(false);
                return;
            }
            _activeHaircutView.ActionProgress?.SetProgressForService(
                ServiceType.Cut,
                HaircutDemandBubbleView.ToolSymbol(_haircutInteraction.SelectedTool.Value),
                _haircutInteraction.HoldProgress,
                ready ? SalonPalette.Success : SalonPalette.Warning);
            _focusLabel.text = !ready
                ? "正在操作 · 继续长按" : "操作状态合适 · 现在松手";
        }
    }

    private void UpdateActiveServiceInteraction()
    {
        if (_activeServiceView == null || _activeServiceView.Customer == null) return;
        CustomerModel customer = _activeServiceView.Customer;
        if (_game.SelectedCustomer != customer || customer.State != CustomerState.Serving)
        {
            _game.CancelActiveServiceAction(customer);
            _activeServiceView.ActionProgress?.ClearProgress();
            _activeServiceView = null;
            _activeServicePointer = int.MinValue;
            return;
        }
        ActiveServiceAction action = customer.ActiveServiceAction;
        BlowResult forcedBlowResult = BlowResult.None;
        float duration;
        if (action == ActiveServiceAction.ManualBlow)
        {
            duration = Mathf.Max(.01f, ServiceSettings.ManualBlowGoodEnd);
            _game.TickManualBlow(customer, Time.deltaTime);
            if (customer.ManualBlowElapsed > Mathf.Max(
                    ServiceSettings.ManualBlowGoodEnd, ServiceSettings.ManualBlowMinorEnd))
                forcedBlowResult = _game.EndManualBlowHold(customer);
        }
        else
        {
            // SalonGameModel.Tick now advances every timed service action for both input
            // paths. This method only reads the result for presentation.
            duration = Mathf.Max(.01f, customer.ActiveServiceDuration);
        }
        if (action != ActiveServiceAction.WrapTowel && action != ActiveServiceAction.RemoveTowel)
        {
            float actionElapsed = action == ActiveServiceAction.ManualBlow ? customer.ManualBlowElapsed : customer.ActiveServiceElapsed;
            float progress = actionElapsed / duration;
            string actionSymbol = GetActiveServiceActionSymbol(action);
            ServiceType? actionService = ServiceForAction(action);
            if (actionService.HasValue)
                _activeServiceView.ActionProgress?.SetProgressForService(
                    actionService.Value,
                    actionSymbol,
                    progress,
                    GetActiveServiceProgressColor(action,
                        actionElapsed,
                        duration, ServiceSettings));
        }
        if (customer.ActiveServiceAction != ActiveServiceAction.None) return;
        _activeServiceView.ActionProgress?.ClearProgress();
        _activeServiceView.OrderDemand?.Refresh();
        _activeServiceView.SetTowelWrapped(customer.TowelWrapped);
        _activeServiceView = null;
        _activeServicePointer = int.MinValue;
        if (forcedBlowResult == BlowResult.Moderate)
        {
            ShowToast("吹风过久 · 顾客不满");
            _focusLabel.text = "吹发过热 · 服务质量下降";
        }
        if (_game.SelectedCustomer == customer) BuildToolBar(customer);
    }

    public static Color GetActiveServiceProgressColor(
        ActiveServiceAction action, float elapsed, float requiredDuration, SalonServiceConfig config)
    {
        if (action == ActiveServiceAction.ManualBlow)
        {
            float validStart = config == null ? requiredDuration : config.ManualBlowGoodStart;
            float validEnd = config == null ? requiredDuration : config.ManualBlowGoodEnd;
            if (elapsed < validStart) return SalonPalette.Warning;
            return elapsed <= validEnd ? SalonPalette.Success : SalonPalette.Warning;
        }
        return elapsed < Mathf.Max(.01f, requiredDuration)
            ? SalonPalette.Warning
            : SalonPalette.Success;
    }

    private void HandleHaircutResult(HaircutResult result)
    {
        if (_activeHaircutView == null || _activeHaircutView.Customer == null) return;
        var view = _activeHaircutView;
        bool fulfilledRequiredCut = !view.Customer.IsComplete && view.Customer.CurrentNeed == ServiceType.Cut;
        int extraServiceCountBefore = view.Customer.ExtraServiceCount;
        if (!_haircutInteraction.SelectedTool.HasValue)
        {
            _game.EndActiveOperation(view.Customer);
            ResetToolSelection();
            BuildToolBar(view.Customer);
            ShowToast("工具选择已失效");
            return;
        }
        SalonTool selectedTool = _haircutInteraction.SelectedTool.Value;
        HaircutResult domainResult = _game.CompleteHaircutAction(
            view.Customer, _haircutInteraction.HoldTime, false);
        if (domainResult == HaircutResult.None)
        {
            _game.EndActiveOperation(view.Customer);
            CancelHaircutInteraction(false);
            return;
        }
        result = domainResult;
        if (view.Customer.WrongServiceKind == CustomerWrongServiceKind.Haircut)
        {
            view.HaircutDemand?.ClearHoldProgress();
            view.ActionProgress?.ClearProgress();
            view.HaircutFeedback.Stop(result);
            _activeHaircutPointer = int.MinValue;
            _activeHaircutView = null;
            _game.EndActiveOperation(view.Customer);
            if (view.Customer.State == CustomerState.Serving && _game.SelectedCustomer == view.Customer)
                BuildToolBar(view.Customer);
            return;
        }

        view.ApplyHairStage(view.Customer.HairStage);
        view.HaircutDemand?.ClearHoldProgress();
        view.ActionProgress?.ClearProgress();
        view.OrderDemand?.Refresh();
        view.HaircutDemand?.Refresh();
        view.HaircutFeedback.Stop(result);
        _activeHaircutPointer = int.MinValue;
        _game.EndActiveOperation(view.Customer);
        if (view.Customer.State == CustomerState.Leaving || view.Customer.State == CustomerState.Exited)
            return;
        if (result == HaircutResult.Resisted)
        {
            _focusLabel.text = "顾客正在抗拒 · 松手不会造成剪发";
            _haircutInteraction.PrepareRetry();
            BuildToolBar(view.Customer);
        }
        else if (result == HaircutResult.Undercut)
        {
            ShowToast("剪得还不够");
            _focusLabel.text = "剪发工位 · 再长按一次补剪";
            _haircutInteraction.PrepareRetry();
        }
        else if (result == HaircutResult.Perfect)
        {
            if (view.Customer.HaircutService.State == HaircutServiceState.Active)
            {
                SalonTool next = view.Customer.HaircutService.CurrentRequiredTool;
                ResetToolSelection();
                ShowToast("本步骤完成");
                _focusLabel.text = "顾客仍需要：" + ToolName(next);
            }
            else if (ShouldReportExtraHaircut(view.Customer, fulfilledRequiredCut, extraServiceCountBefore))
            {
                ShowToast("额外剪发 · 未推进需求");
                _focusLabel.text = "多余服务 · 满意度已受影响";
                BuildToolBar(view.Customer);
            }
            else if (view.Customer.State == CustomerState.Serving)
            {
                BuildToolBar(view.Customer);
                ShowToast("剪发步骤完成");
            }
            else
            {
                PaymentDropModel payment = FindLatestPayment(view.Customer.Id);
                string rating = view.Customer.ServiceResult == CustomerServiceResult.HappyCompletion
                    ? "HAPPY · 基础 " + payment.BaseReward + " + 小费 " + payment.TipReward
                    : "NORMAL · 基础收入 " + payment.BaseReward;
                ShowToast("收入已结算");
                _focusLabel.text = rating;
                StartCoroutine(ReturnToOverviewAfterResult(view, HaircutSettings.ResultFeedbackSeconds));
            }
        }
        else
        {
            ShowToast("剪太短 · 顾客非常生气");
            _focusLabel.text = "FAILED · 未完成，不结算金币";
            StartCoroutine(ReturnToOverviewAfterResult(view, HaircutSettings.ResultFeedbackSeconds));
        }
    }

    private void ResolveWrongTool(SalonCustomerView view, SalonTool selectedTool)
    {
        if (!_game.ApplyHaircutResult(view.Customer, selectedTool, HaircutResult.WrongTool, HaircutSettings)) return;
        view.HaircutDemand?.Refresh();
        view.HaircutFeedback.PlayImmediateResult(HaircutResult.WrongTool, selectedTool);
        ResetToolSelection();
        BuildToolBar(view.Customer);
        ShowToast("工具错误 · 未推进当前剪发步骤");
        _activeHaircutPointer = int.MinValue;
        _activeHaircutView = null;
    }

    private void HandlePaymentCreated(PaymentDropModel drop)
    {
        if (drop == null || _game == null || !_game.Payments.BeginCollection(drop.Id)) return;
        if (!_game.Payments.CompleteCollection(drop.Id)) return;
        _dayController?.Stats.RecordPaymentCollected(drop);
        if (_coinBalanceLabel != null) _coinBalanceLabel.text = _game.Balance.ToString("N0");
    }

    private PaymentDropModel FindLatestPayment(int customerId)
    {
        for (int i = _game.Payments.Drops.Count - 1; i >= 0; i--)
            if (_game.Payments.Drops[i].CustomerId == customerId)
                return _game.Payments.Drops[i];
        return new PaymentDropModel();
    }

    private IEnumerator ReturnToOverviewAfterResult(SalonCustomerView view, float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(.3f, delay));
        if (view == null || _game.SelectedCustomer != view.Customer ||
            _game.ViewState != SalonViewState.WorkstationFocus) yield break;
        _game.ClearFocus();
    }

    private void CancelHaircutInteraction(bool resetTool)
    {
        if (_haircutInteraction == null) return;
        if (_haircutInteraction.State == HaircutInteractionState.Holding)
        {
            if (_game != null && _activeHaircutView != null && _activeHaircutView.Customer != null)
                _game.CompleteHaircutAction(
                    _activeHaircutView.Customer, _haircutInteraction.HoldTime, true);
            _haircutInteraction.CancelHold();
        }
        if (_game != null && _activeHaircutView != null && _activeHaircutView.Customer != null)
            _game.EndActiveOperation(_activeHaircutView.Customer);
        if (_activeHaircutView != null && _activeHaircutView.HaircutFeedback != null) _activeHaircutView.HaircutFeedback.Cancel();
        if (_activeHaircutView != null && _activeHaircutView.HaircutDemand != null) _activeHaircutView.HaircutDemand.ClearHoldProgress();
        _activeHaircutView = null;
        _activeHaircutPointer = int.MinValue;
        if (_activeServiceView != null && _activeServiceView.Customer != null)
        {
            _game?.CancelActiveServiceAction(_activeServiceView.Customer);
            _activeServiceView.ActionProgress?.ClearProgress();
        }
        _activeServiceView = null;
        _activeServicePointer = int.MinValue;
        _selectedServiceAction = ActiveServiceAction.None;
        if (resetTool) ResetToolSelection();
    }

    private void ResetToolSelection()
    {
        _selectedToolIndex = -1;
        _selectedServiceAction = ActiveServiceAction.None;
        _haircutInteraction?.Reset();
        if (_game != null)
        {
            PlayerContext context = _game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId);
            context.ClearInteractionContext();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            if (_mobileMode) OnApplicationPause(true);
            else CancelHaircutInteraction(false);
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            if (_mobileMode) SuspendMobileInput();
            else CancelHaircutInteraction(false);
            if (_dayController != null && (_dayController.State == DayState.Business ||
                                           _dayController.State == DayState.ClosingGrace))
            {
                _dayController.SetPaused(true);
                if (_pausePanel != null) _pausePanel.SetActive(true);
            }
        }
    }

    private void OnDestroy()
    {
        if (_haircutInteraction != null) _haircutInteraction.ResultResolved -= HandleHaircutResult;
        if (_game != null) _game.PaymentCreated -= HandlePaymentCreated;
        if (_game != null) _game.CustomerChanged -= HandleCustomerChanged;
        if (_game != null) _game.ViewChanged -= HandleViewChanged;
        if (_dayController != null) _dayController.StateChanged -= HandleDayStateChanged;
        if (_satisfaction != null) _satisfaction.Changed -= HandleSatisfactionChanged;
        if (_mobileSupplies != null) _mobileSupplies.Changed -= HandleMobileSupplyChanged;
    }

    public void SetSatisfactionForDebug(int value) => _satisfaction?.SetCurrent(value);

    public DevelopmentDebugContext CreateDevelopmentDebugContext()
    {
        string target = "none";
        if (_game?.SelectedCustomer != null &&
            _stationRoots.TryGetValue(_game.SelectedCustomer.Station, out GameObject station) && station != null)
            target = station.name;
        string buildVersion = Application.version;
        try { buildVersion = AssetManifestLoader.LoadFromResources().BuildVersion; }
        catch (System.Exception) { }
        return new DevelopmentDebugContext
        {
            Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            CharacterPosition = _player == null ? Vector3.zero : _player.position,
            CharacterState = _playerCharacter == null ? "missing" : _playerCharacter.CurrentState.ToString(),
            ServiceState = (_dayController == null ? "initializing" : _dayController.State.ToString()) +
                           "/" + _selectedServiceAction,
            TargetStation = target,
            Viewport = new Vector2Int(Screen.width, Screen.height),
            BuildVersion = buildVersion
        };
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Debug/Satisfaction/Happy 90")]
    private void PreviewHappySatisfaction() => SetSatisfactionForDebug(90);

    [ContextMenu("Debug/Satisfaction/Neutral 65")]
    private void PreviewNeutralSatisfaction() => SetSatisfactionForDebug(65);

    [ContextMenu("Debug/Satisfaction/Sad 25")]
    private void PreviewSadSatisfaction() => SetSatisfactionForDebug(25);
#endif

    private void UpdateCamera()
    {
        float blend = 1f - Mathf.Exp(-13f * Time.deltaTime);
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, _cameraPositionTarget, blend);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, _overviewCameraRotation, blend);
        _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _cameraSizeTarget, blend);
    }

    private void UpdatePlayer()
    {
        if (_player == null) return;
        Vector3 flatTarget = new Vector3(_playerTarget.x, _player.position.y, _playerTarget.z);
        Vector3 navigationTarget = ResolvePlayerNavigationTarget(flatTarget);
        if (_playerCharacter != null)
        {
            Vector3 positionBeforeMove = _player.position;
            _playerCharacter.MoveTowards(navigationTarget, Time.deltaTime);
            if (IsAnyCutStationMovementBlocked(_player.position))
                _player.position = positionBeforeMove;
            FacePlayerTowardStructuredStation(flatTarget);
            return;
        }
        Vector3 delta = navigationTarget - _player.position;
        if (delta.sqrMagnitude < .015f) return;
        Vector3 fallbackPositionBeforeMove = _player.position;
        _player.position = Vector3.MoveTowards(_player.position, navigationTarget, 5.5f * Time.deltaTime);
        if (IsAnyCutStationMovementBlocked(_player.position))
            _player.position = fallbackPositionBeforeMove;
        if (delta.sqrMagnitude > .01f) _player.rotation = Quaternion.Slerp(_player.rotation, Quaternion.LookRotation(delta), 1f - Mathf.Exp(-12f * Time.deltaTime));
        FacePlayerTowardStructuredStation(flatTarget);
    }

    private Vector3 ResolvePlayerNavigationTarget(Vector3 destination)
    {
        if (!_hasPlayerRoute || (_playerRouteDestination - destination).sqrMagnitude > .001f)
        {
            var layouts = new List<ResolvedCutStationLayout>();
            foreach (CutStation station in _cutStations.Values)
                if (station != null) layouts.Add(station.Layout);
            _playerRoute = SalonPlayerRoute.Build(_player.position, destination, layouts);
            _playerRouteDestination = destination;
            _playerRouteIndex = 0;
            _hasPlayerRoute = true;
        }
        while (_playerRouteIndex < _playerRoute.Length &&
               Vector3.Distance(_player.position, _playerRoute[_playerRouteIndex]) < .04f) _playerRouteIndex++;
        return _playerRouteIndex < _playerRoute.Length ? _playerRoute[_playerRouteIndex] :
            _playerRoute.Length > 0 ? destination : _player.position;
    }

    public static bool IsCutStationMovementBlocked(ResolvedCutStationLayout layout, Vector3 worldPoint)
    {
        if (layout == null) return false;
        Vector2 center = new Vector2(
            layout.Origin.x + layout.Collision.Center.x,
            layout.Origin.z + layout.Collision.Center.y);
        Vector2 half = layout.Collision.Size * .5f;
        return worldPoint.x > center.x - half.x && worldPoint.x < center.x + half.x &&
               worldPoint.z > center.y - half.y && worldPoint.z < center.y + half.y;
    }

    private bool IsAnyCutStationMovementBlocked(Vector3 worldPoint)
    {
        foreach (CutStation station in _cutStations.Values)
            if (station != null && IsCutStationMovementBlocked(station.Layout, worldPoint)) return true;
        return false;
    }

    private void FacePlayerTowardStructuredStation(Vector3 flatTarget)
    {
        if (_player == null || Vector3.Distance(_player.position, flatTarget) > .06f ||
            _game == null || _game.SelectedCustomer == null ||
            !_cutStations.TryGetValue(_game.SelectedCustomer.Station, out var station)) return;
        if (_playerCharacter != null) _playerCharacter.FaceTowards(station.Layout.StylistFacing);
        else _player.rotation = Quaternion.LookRotation(station.Layout.StylistFacing, Vector3.up);
    }

    private Vector3 FindCustomerPosition(CustomerModel customer)
    {
        foreach (var view in _customerViews) if (view.Customer == customer) return view.transform.position;
        return Vector3.zero;
    }

    private SalonCustomerView FindCustomerView(CustomerModel customer)
    {
        for (int i = 0; i < _customerViews.Count; i++)
            if (_customerViews[i] != null && _customerViews[i].Customer == customer) return _customerViews[i];
        return null;
    }

    private IEnumerator CaptureFromCommandLine()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        string capturePath = null;
        int focusCustomer = -1;
        float haircutHold = -1f;
        float capturePatience = -1f;
        bool captureWhileHolding = false;
        bool completeHaircut = false;
        bool captureBusinessHud = false;
        SalonTool captureTool = SalonTool.Scissors;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-salonCapture") capturePath = args[i + 1];
            if (args[i] == "-salonFocus") int.TryParse(args[i + 1], out focusCustomer);
            if (args[i] == "-salonHaircutHold") float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out haircutHold);
            if (args[i] == "-salonPatience") float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out capturePatience);
            if (args[i] == "-salonTool")
            {
                string tool = args[i + 1].ToLowerInvariant();
                captureTool = tool == "thinning" ? SalonTool.ThinningShears : tool == "clippers" ? SalonTool.Clippers : SalonTool.Scissors;
            }
        }
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-salonCaptureHolding") captureWhileHolding = true;
            if (args[i] == "-salonCompleteHaircut") completeHaircut = true;
            if (args[i] == "-salonCaptureBusinessHud") captureBusinessHud = true;
        }
        if (string.IsNullOrEmpty(capturePath)) yield break;
        if (captureBusinessHud) StartBusinessDay();
        // Give the fixed waypoint lanes time to reach their service anchors before automated captures interact.
        yield return new WaitForSecondsRealtime(SalonGameModel.EnteringSeconds + SalonGameModel.MovingToStationSeconds + 2.8f);
        if (haircutHold >= 0f && focusCustomer >= 0 && focusCustomer < _game.Customers.Count)
        {
            CustomerModel captureCustomer = _game.Customers[focusCustomer];
            if (captureCustomer.Station < 0 && captureCustomer.CurrentNeed == ServiceType.Cut &&
                _game.Assign(captureCustomer, 1))
                yield return new WaitForSecondsRealtime(SalonGameModel.MovingToStationSeconds + .25f);
        }
        if (focusCustomer >= 0 && focusCustomer < _game.Customers.Count) HandleCustomerClick(_game.Customers[focusCustomer]);
        yield return new WaitForSecondsRealtime(1f);
        if (haircutHold >= 0f && focusCustomer >= 0 && focusCustomer < _customerViews.Count)
        {
            if (capturePatience >= 0f)
            {
                CustomerModel captureCustomer = _customerViews[focusCustomer].Customer;
                captureCustomer.Patience = Mathf.Clamp(capturePatience, 0f, 100f);
                float angryAt = Mathf.Max(0f, PatienceSettings.AngryAtPatience);
                float impatientAt = Mathf.Max(angryAt, PatienceSettings.ImpatientAtPatience);
                if (captureCustomer.Patience <= impatientAt)
                {
                    captureCustomer.Emotion = captureCustomer.Patience > angryAt
                        ? CustomerEmotion.Impatient
                        : CustomerEmotion.Angry;
                    captureCustomer.WasImpatientBeforeService = true;
                }
                _customerViews[focusCustomer].EmotionView?.Refresh();
            }
            _selectedToolIndex = HaircutToolIndex(captureTool);
            _haircutInteraction.SelectTool(captureTool);
            RefreshToolButtons();
            var view = _customerViews[focusCustomer];
            if (HandleHaircutPointerDown(view, -1))
            {
                float elapsed = 0f;
                while (elapsed < haircutHold && _haircutInteraction.State == HaircutInteractionState.Holding)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!captureWhileHolding) HandleHaircutPointerUp(view, -1);
            }
            yield return new WaitForSecondsRealtime(.18f);

            if (completeHaircut && view.Customer.HaircutService != null &&
                view.Customer.HaircutService.State == HaircutServiceState.Active)
            {
                SalonTool nextTool = view.Customer.HaircutService.CurrentRequiredTool;
                _selectedToolIndex = HaircutToolIndex(nextTool);
                _haircutInteraction.SelectTool(nextTool);
                RefreshToolButtons();
                if (HandleHaircutPointerDown(view, -2))
                {
                    float targetHold = (HaircutSettings.GetPerfectMin(nextTool) + HaircutSettings.GetPerfectMax(nextTool)) * .5f;
                    float elapsed = 0f;
                    while (elapsed < targetHold && _haircutInteraction.State == HaircutInteractionState.Holding)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    HandleHaircutPointerUp(view, -2);
                }
                yield return new WaitForSecondsRealtime(.25f);
            }

        }
        else yield return new WaitForSecondsRealtime(.4f);
        Directory.CreateDirectory(Path.GetDirectoryName(capturePath));
        UnityEngine.ScreenCapture.CaptureScreenshot(capturePath);
        yield return new WaitForSecondsRealtime(.75f);
        Application.Quit();
    }

    private static int HaircutToolIndex(SalonTool tool)
    {
        if (tool == SalonTool.ThinningShears) return 2;
        if (tool == SalonTool.Clippers) return 3;
        return 1;
    }

    private static Transform BuildPerson(string name, Transform parent, Vector3 position, Color hair, Color skin, Color clothes, bool seated)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.position = position;
        Block("Body", root, new Vector3(0f, seated ? .65f : .95f, 0f), new Vector3(1.05f, seated ? 1.15f : 1.65f, .72f), clothes);
        Block("Apron", root, new Vector3(0f, seated ? .67f : .95f, -.38f), new Vector3(.8f, seated ? .85f : 1.25f, .12f), TealDark);
        Block("Head", root, new Vector3(0f, seated ? 1.62f : 2.05f, -.03f), new Vector3(.93f, .88f, .82f), skin, new Vector3(0f, 8f, 0f));
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI * 2f / 6f;
            Block("Faceted Hair", root, new Vector3(Mathf.Sin(angle) * .37f, (seated ? 2.03f : 2.46f) + (i % 2) * .08f, -.02f + Mathf.Cos(angle) * .26f), new Vector3(.48f, .42f, .5f), hair, new Vector3(i * 11f, i * 23f, i * 7f));
        }
        Block("Eye L", root, new Vector3(-.2f, seated ? 1.66f : 2.09f, -.46f), new Vector3(.09f, .09f, .06f), Ink);
        Block("Eye R", root, new Vector3(.2f, seated ? 1.66f : 2.09f, -.46f), new Vector3(.09f, .09f, .06f), Ink);
        if (!seated)
        {
            Block("Leg L", root, new Vector3(-.25f, .15f, 0f), new Vector3(.32f, .7f, .38f), Ink);
            Block("Leg R", root, new Vector3(.25f, .15f, 0f), new Vector3(.32f, .7f, .38f), Ink);
        }
        return root;
    }

    private static void BuildChair(Transform parent, Vector3 position, Color color)
    {
        Cylinder("Chair Base", parent, position + new Vector3(0f, .15f, 0f), new Vector3(1.25f, .2f, 1.25f), Ink);
        Block("Chair Seat", parent, position + new Vector3(0f, .63f, 0f), new Vector3(1.45f, .42f, 1.45f), color);
        Block("Chair Back", parent, position + new Vector3(0f, 1.45f, .55f), new Vector3(1.5f, 1.45f, .38f), color, new Vector3(-8f, 0f, 0f));
    }

    private static void BuildPlant(Transform parent, Vector3 position)
    {
        Block("Plant Pot", parent, position, new Vector3(.85f, .7f, .85f), Wood);
        for (int i = 0; i < 5; i++)
            Block("Faceted Leaf", parent, position + new Vector3((i - 2) * .2f, .75f + (i % 2) * .28f, (i % 3 - 1) * .18f), new Vector3(.42f, .9f, .28f), i % 2 == 0 ? Hex("477B37") : Hex("6B993F"), new Vector3(i * 12f, i * 28f, (i - 2) * 18f));
    }

    private static Transform FurnitureShadow(Transform parent, string assetId, Vector3 localFloorPosition)
    {
        AssetDefinition asset;
        try { asset = AssetManifestLoader.LoadFromResources().Find(assetId); }
        catch (System.Exception) { return null; }
        if (asset == null) return null;
        var anchor = new GameObject("Shadow Anchor [" + assetId + "]").transform;
        anchor.SetParent(parent, false);
        anchor.localPosition = localFloorPosition;
        if (asset.Collision != null) anchor.gameObject.AddComponent<SalonFurnitureObstacle>().Initialize(asset);
        if (asset.Shadow != null && asset.Shadow.Enabled)
            ContactShadow.Apply(anchor, asset.Shadow, asset.Sorting?.Order - 1 ?? -1);
        return anchor;
    }

    private static GameObject SelectionPlate(Transform parent, Vector3 position, Vector3 scale)
    {
        var outline = new GameObject("Selected Workstation Outline");
        outline.transform.SetParent(parent, false);
        outline.transform.localPosition = position;
        float width = scale.x;
        float depth = scale.z;
        Block("Glow Front", outline.transform, new Vector3(0f, 0f, -depth * .5f), new Vector3(width, .08f, .1f), Gold);
        Block("Glow Back", outline.transform, new Vector3(0f, 0f, depth * .5f), new Vector3(width, .08f, .1f), Gold);
        Block("Glow Left", outline.transform, new Vector3(-width * .5f, 0f, 0f), new Vector3(.1f, .08f, depth), Gold);
        Block("Glow Right", outline.transform, new Vector3(width * .5f, 0f, 0f), new Vector3(.1f, .08f, depth), Gold);
        outline.SetActive(false);
        return outline;
    }

    private static Transform Marker(string name, Transform parent, Vector3 position)
    {
        var marker = new GameObject(name).transform;
        marker.SetParent(parent, false);
        marker.position = position;
        return marker;
    }

    private static GameObject Block(string name, Transform parent, Vector3 position, Vector3 scale, Color color, Vector3 euler = default)
    {
        var go = SalonPrimitiveFactory.CreateCube(parent, position, scale, color);
        go.name = name;
        go.transform.localEulerAngles = euler;
        return go;
    }

    private static GameObject Cylinder(string name, Transform parent, Vector3 position, Vector3 scale, Color color, Vector3 euler = default)
    {
        var go = SalonPrimitiveFactory.CreateCylinder(parent, position, scale, color);
        go.name = name;
        go.transform.localEulerAngles = euler;
        return go;
    }

    private static GameObject UiPanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static RectTransform UiRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image HudArtwork(string name, Transform parent, string resourcePath, Vector2 position,
        Vector2 size, bool preserveAspect = true, float opacity = 1f, Vector2? anchor = null)
    {
        Vector2 chosenAnchor = anchor ?? new Vector2(.5f, .5f);
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = chosenAnchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = LoadTopHudSprite(resourcePath);
        image.preserveAspect = preserveAspect;
        image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(opacity));
        image.raycastTarget = false;
        return image;
    }

    private static Button HudImageButton(string name, Transform parent, string resourcePath, Vector2 position,
        Vector2 size, UnityEngine.Events.UnityAction action, Vector2? anchor = null, Vector2? artworkSize = null)
    {
        Vector2 chosenAnchor = anchor ?? new Vector2(.5f, .5f);
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = chosenAnchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var hitArea = go.GetComponent<Image>();
        hitArea.color = new Color(1f, 1f, 1f, .001f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = hitArea;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, .96f, .82f, 1f);
        colors.pressedColor = new Color(.86f, .82f, .72f, 1f);
        button.colors = colors;
        if (action != null) button.onClick.AddListener(action);
        HudArtwork(name + " Artwork", go.transform, resourcePath, Vector2.zero, artworkSize ?? size);
        return button;
    }

    private static Image HudPill(string name, Transform parent, Color color, Vector2 position, Vector2 size,
        Vector2? anchor = null)
    {
        Vector2 chosenAnchor = anchor ?? new Vector2(.5f, .5f);
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = chosenAnchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = SalonUiFactory.GetCircleSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text HudText(string text, Transform parent, float size, Color color, Vector2 position,
        Vector2 dimensions)
    {
        var go = new GameObject("Dynamic TMP Text", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = TopHudFont();
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(12f, size * .55f);
        label.fontSizeMax = size;
        label.raycastTarget = false;
        label.outlineColor = new Color32(54, 31, 17, 180);
        label.outlineWidth = .09f;
        return label;
    }

    private static Sprite LoadTopHudSprite(string resourcePath)
    {
        if (TopHudSprites.TryGetValue(resourcePath, out Sprite cached) && cached != null) return cached;
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogError("Missing Top HUD texture at Resources/" + resourcePath);
            return null;
        }
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(.5f, .5f), 100f);
        sprite.name = resourcePath.Substring(resourcePath.LastIndexOf('/') + 1);
        TopHudSprites[resourcePath] = sprite;
        return sprite;
    }

    private static TMP_FontAsset TopHudFont()
    {
        if (_topHudFont != null) return _topHudFont;
        Font packagedUiFont = SalonUiFactory.GetPackagedUiFont();
        if (packagedUiFont != null)
            _topHudFont = TMP_FontAsset.CreateFontAsset(packagedUiFont);
        if (_topHudFont != null)
        {
            _topHudFont.name = "Top HUD Packaged CJK Font";
            return _topHudFont;
        }
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        _topHudFont = TMP_FontAsset.CreateFontAsset(
            "/System/Library/Fonts/Supplemental/Arial Unicode.ttf", 0, 90, 9,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048);
#else
        _topHudFont = TMP_FontAsset.CreateFontAsset("Microsoft YaHei", "Regular", 90) ??
                      TMP_FontAsset.CreateFontAsset("Noto Sans CJK SC", "Regular", 90);
#endif
        if (_topHudFont == null) _topHudFont = GetPackagedTopHudFallback();
        if (_topHudFont == null)
            throw new System.InvalidOperationException("A packaged TextMeshPro font is required for the Top HUD.");
        _topHudFont.name = "Top HUD Dynamic CJK Font";
        return _topHudFont;
    }

    public static TMP_FontAsset GetPackagedTopHudFallback()
        => Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") ?? TMP_Settings.defaultFontAsset;

    private static Text UiLabel(string text, Transform parent, int size, Color color, TextAnchor alignment, Vector2 position, Vector2 dimensions, Color? backing = null, Vector2? anchor = null)
    {
        Vector2 chosenAnchor = anchor ?? new Vector2(.5f, .5f);
        Text label;
        RectTransform rect;
        if (backing.HasValue)
        {
            label = SalonUiFactory.CreateLabel(text, parent, color, backing.Value);
            rect = (RectTransform)label.transform.parent;
        }
        else
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            label = go.GetComponent<Text>();
            rect = go.GetComponent<RectTransform>();
        }
        rect.anchorMin = rect.anchorMax = chosenAnchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        label.text = text;
        label.font = SalonUiFactory.GetPackagedUiFont();
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(12, size / 2);
        label.resizeTextMaxSize = size;
        return label;
    }

    private static Button UiButton(string text, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction action)
    {
        var go = UiPanel("Button", parent, color, anchor, anchor, position, size);
        SalonUiFactory.StyleRoundedPanel(go, new Color(.22f, .12f, .07f, .48f), new Vector2(0f, -4f));
        var button = go.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, .16f);
        colors.pressedColor = Color.Lerp(color, Color.black, .12f);
        colors.disabledColor = new Color(.45f, .42f, .39f, .55f);
        colors.fadeDuration = .08f;
        button.colors = colors;
        UiLabel(text, go.transform, 27, Color.white, TextAnchor.MiddleCenter, Vector2.zero, size - new Vector2(12f, 12f));
        if (action != null) button.onClick.AddListener(action);
        return button;
    }

    private static string ServiceSymbol(ServiceType service)
    {
        if (service == ServiceType.Cut) return "✂";
        if (service == ServiceType.Wash) return "♨";
        if (service == ServiceType.Dye) return "◆";
        if (service == ServiceType.Perm) return "◎";
        return "◌";
    }

    private static string ToolName(SalonTool tool)
    {
        if (tool == SalonTool.Scissors) return "剪刀";
        if (tool == SalonTool.ThinningShears) return "分齿剪";
        if (tool == SalonTool.Clippers) return "推子";
        if (tool == SalonTool.BlowDryer) return "吹风机";
        if (tool == SalonTool.Shampoo) return "洗头工具";
        if (tool == SalonTool.DyeBottle) return "染发刷";
        if (tool == SalonTool.PermSolution) return "烫发工具";
        return "工具";
    }

    private static string WashStageLabel(WashStage stage)
    {
        if (stage == WashStage.Dry) return "洗头工位";
        if (stage == WashStage.Wetting) return "洗头中...";
        if (stage == WashStage.Wet) return "洗头工位 · 头发已湿";
        if (stage == WashStage.ShampooApplied) return "洗头中...";
        if (stage == WashStage.Shampooing) return "正在揉洗起泡";
        if (stage == WashStage.Foamy) return "洗头工位 · 泡沫状态";
        if (stage == WashStage.Rinsing) return "正在冲洗";
        if (stage == WashStage.Rinsed) return "洗头工位 · 已冲洗";
        return "洗头步骤完成";
    }

    private static string ExitBlockerText(ExitBlockReason reason)
    {
        if (reason == ExitBlockReason.FoamRemaining) return "仍有泡沫，需要收尾";
        if (reason == ExitBlockReason.ShampooResidue) return "仍有洗发残留";
        if (reason == ExitBlockReason.TowelWrapped) return "毛巾还没拆";
        if (reason == ExitBlockReason.WetHair) return "头发还太湿";
        if (reason == ExitBlockReason.Moving) return "顾客仍在移动";
        if (reason == ExitBlockReason.ActiveAction) return "当前操作尚未结束";
        return "仍有未处理的离店条件";
    }

    private static string ServiceActionName(ActiveServiceAction action)
    {
        if (action == ActiveServiceAction.Shower) return "洗头";
        if (action == ActiveServiceAction.Shampoo) return "洗头";
        if (action == ActiveServiceAction.WrapTowel) return "洗头";
        if (action == ActiveServiceAction.RemoveTowel) return "洗头";
        if (action == ActiveServiceAction.ApplyDye) return "染发";
        if (action == ActiveServiceAction.ApplyPerm) return "烫发";
        return "操作";
    }

    private static string GetActiveServiceActionSymbol(ActiveServiceAction action)
    {
        if (action == ActiveServiceAction.Shower) return "♨";
        if (action == ActiveServiceAction.Shampoo) return "≈";
        if (action == ActiveServiceAction.ManualBlow) return "≋";
        if (action == ActiveServiceAction.WrapTowel || action == ActiveServiceAction.RemoveTowel) return "▱";
        if (action == ActiveServiceAction.ApplyDye) return "◆";
        if (action == ActiveServiceAction.ApplyPerm) return "◎";
        return "⌁";
    }

    private static SalonTool? ToolForServiceAction(ActiveServiceAction action)
    {
        switch (action)
        {
            case ActiveServiceAction.Shampoo: return SalonTool.Shampoo;
            case ActiveServiceAction.ManualBlow: return SalonTool.BlowDryer;
            case ActiveServiceAction.ApplyDye: return SalonTool.DyeBottle;
            case ActiveServiceAction.ApplyPerm: return SalonTool.PermSolution;
            default: return null;
        }
    }

    private static ServiceType? ServiceForAction(ActiveServiceAction action)
    {
        switch (action)
        {
            case ActiveServiceAction.Shower:
            case ActiveServiceAction.Shampoo:
            case ActiveServiceAction.WrapTowel:
            case ActiveServiceAction.RemoveTowel:
                return ServiceType.Wash;
            case ActiveServiceAction.ManualBlow:
                return ServiceType.Dry;
            case ActiveServiceAction.ApplyDye:
                return ServiceType.Dye;
            case ActiveServiceAction.ApplyPerm:
                return ServiceType.Perm;
            default:
                return null;
        }
    }

    private static string SelectedActionHint(ActiveServiceAction action)
    {
        if (action == ActiveServiceAction.Shampoo) return "点击开始洗头";
        if (action == ActiveServiceAction.Shower) return "点击开始洗头";
        if (action == ActiveServiceAction.ManualBlow) return "已选择手动吹发 · 长按顾客头部";
        if (action == ActiveServiceAction.ApplyDye) return "已选择染发刷 · 长按顾客涂抹染膏";
        if (action == ActiveServiceAction.ApplyPerm) return "已选择烫发工具 · 长按顾客上卷";
        return string.Empty;
    }

    private static string BlowStageLabel(BlowStage stage)
    {
        if (stage == BlowStage.AwaitingStart) return "理发工位 · 启动吹发";
        if (stage == BlowStage.Early || stage == BlowStage.AutoRunning) return "自动吹发运行中 · 尚未完成";
        if (stage == BlowStage.ManualHolding) return "正在手动吹发 · 保持长按";
        if (stage == BlowStage.Good) return "吹发已到最佳结束窗口";
        if (stage == BlowStage.Minor) return "吹发轻度超时 · 尽快结束";
        if (stage == BlowStage.Moderate) return "吹发中度超时";
        if (stage == BlowStage.SafetyStopped) return "自动设备已安全停机 · 点击完成";
        return "吹发完成";
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out var color);
        return color;
    }
}

public sealed class SalonCustomerView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public SalonDemo Owner;
    public CustomerModel Customer;
    public Image RingFill;
    public Vector3 BaseScale = Vector3.one;
    public Color HairColor;
    public HaircutRuntimeFeedback HaircutFeedback;
    public HaircutDemandBubbleView HaircutDemand;
    public OrderDemandBubbleView OrderDemand;
    public ActionProgressView ActionProgress;
    public CustomerEmotionView EmotionView;
    public CustomerUIRootView CustomerUI;
    public Vector3 LastStationPosition;
    public int LastStationId = -1;
    public Transform LastServiceUiAnchor;
    public bool IsAtMovementDestination { get; private set; }
    public bool IsTowelVisualVisible => _towelVisual != null && _towelVisual.activeSelf;
    public int TowelVisualPartCount => _towelVisual == null ? 0 : _towelVisual.transform.childCount;
    public int VisibleFoamPartCount
    {
        get
        {
            int visible = 0;
            for (int i = 0; i < _foamParts.Count; i++)
                if (_foamParts[i] != null && _foamParts[i].activeSelf) visible++;
            return visible;
        }
    }
    public bool IsWashPose { get; private set; }
    private readonly List<Transform> _hairParts = new List<Transform>();
    private readonly List<Vector3> _hairScales = new List<Vector3>();
    private readonly List<Vector3> _hairPositions = new List<Vector3>();
    private readonly List<Color> _hairBaseColors = new List<Color>();
    private readonly List<GameObject> _foamParts = new List<GameObject>();
    private readonly List<GameObject> _wetDroplets = new List<GameObject>();
    private GameObject _towelVisual;
    private GameObject _waterVisual;
    private GameObject _shampooBottleVisual;
    private HairStage _hairStage = HairStage.Original;
    private bool _isSeated;
    private bool _towelRemovalAnimating;
    private bool _suppressClick;
    private bool _dragging;
    private Vector3[] _route = new Vector3[0];
    private Transform _serviceQueueAnchor;
    private int _routeIndex;
    private int _routeKind = -1;
    private Vector3 _routeDestination;

    public void RememberServiceAnchor(int stationId, Transform uiAnchor)
    {
        LastStationId = stationId;
        LastServiceUiAnchor = uiAnchor;
    }

    public void SetServiceQueueAnchor(Transform queueAnchor)
    {
        _serviceQueueAnchor = queueAnchor;
    }

    public static bool ShouldUseSeatedPose(CustomerState state, bool reachedDestination)
    {
        if (!reachedDestination) return false;
        return state == CustomerState.Waiting || state == CustomerState.Serving || state == CustomerState.Finished;
    }

    public void ApplyStationPose(CustomerState state, bool reachedDestination, WorkstationType stationType)
    {
        IsWashPose = reachedDestination && stationType == WorkstationType.Wash &&
                     (state == CustomerState.Serving || state == CustomerState.Finished);
        SetSeatedPose(ShouldUseSeatedPose(state, reachedDestination));
        transform.localRotation = IsWashPose ? Quaternion.Euler(72f, 0f, 0f) : Quaternion.identity;
    }

    public void SetSeatedPose(bool seated)
    {
        if (_isSeated == seated) return;
        _isSeated = seated;
        foreach (Transform part in transform)
        {
            if (part.name == "Body")
            {
                part.localPosition = new Vector3(part.localPosition.x, seated ? .65f : .95f, part.localPosition.z);
                part.localScale = new Vector3(part.localScale.x, seated ? 1.15f : 1.65f, part.localScale.z);
            }
            else if (part.name == "Apron")
            {
                part.localPosition = new Vector3(part.localPosition.x, seated ? .67f : .95f, part.localPosition.z);
                part.localScale = new Vector3(part.localScale.x, seated ? .85f : 1.25f, part.localScale.z);
            }
            else if (part.name == "Head")
                part.localPosition = new Vector3(part.localPosition.x, seated ? 1.62f : 2.05f, part.localPosition.z);
            else if (part.name == "Eye L" || part.name == "Eye R")
                part.localPosition = new Vector3(part.localPosition.x, seated ? 1.66f : 2.09f, part.localPosition.z);
            else if (part.name == "Leg L" || part.name == "Leg R")
                part.gameObject.SetActive(!seated);
        }
        ApplyHairStage(_hairStage);
        if (_towelVisual != null) _towelVisual.transform.localPosition = Vector3.down * (seated ? .43f : 0f);
    }

    public void MoveAlongCurrentRoute(CustomerState state, Vector3 destination, float speed, float dt)
    {
        int kind = state == CustomerState.Entering ? 0 :
                   state == CustomerState.Waiting ? 1 :
                   state == CustomerState.MovingToStation || state == CustomerState.Serving ? 2 :
                   state == CustomerState.Finished ? 3 : 4;
        if (_routeKind != kind || (_routeDestination - destination).sqrMagnitude > .01f)
        {
            _routeKind = kind;
            _routeDestination = destination;
            _routeIndex = 0;
            _route = kind == 0 ? SalonCustomerPath.BuildEnteringRoute(transform.position, destination) :
                     kind == 2 && _serviceQueueAnchor != null
                         ? SalonCustomerPath.BuildServiceRoute(transform.position, _serviceQueueAnchor.position, destination) :
                     kind == 2 ? SalonCustomerPath.BuildServiceRoute(transform.position, destination) :
                     kind == 4 ? SalonCustomerPath.BuildLeavingRoute(transform.position, destination) :
                     new[] { destination };
        }

        float remaining = Mathf.Max(0f, speed) * Mathf.Max(0f, dt);
        while (_routeIndex < _route.Length && remaining > 0f)
        {
            Vector3 waypoint = _route[_routeIndex];
            float distance = Vector3.Distance(transform.position, waypoint);
            if (distance <= .001f)
            {
                _routeIndex++;
                continue;
            }
            float step = Mathf.Min(remaining, distance);
            transform.position = Vector3.MoveTowards(transform.position, waypoint, step);
            remaining -= step;
            if (step >= distance - .001f) _routeIndex++;
        }
        IsAtMovementDestination = _routeIndex >= _route.Length &&
                                  Vector3.Distance(transform.position, destination) <= .06f;
    }

    public void InitializeHair(Color color)
    {
        HairColor = color;
        foreach (Transform child in transform)
        {
            if (!child.name.StartsWith("Faceted Hair")) continue;
            _hairParts.Add(child);
            _hairScales.Add(child.localScale);
            _hairPositions.Add(child.localPosition);
            _hairBaseColors.Add(child.GetComponent<Renderer>().sharedMaterial.color);
            child.gameObject.SetActive(!IsTowelVisualVisible);
        }
        BuildWashFeedback();
    }

    public void SetTowelWrapped(bool wrapped)
    {
        if (_towelVisual == null)
        {
            _towelVisual = new GameObject("Low Poly Towel Wrap");
            _towelVisual.transform.SetParent(transform, false);
            SalonPrimitiveFactory.CreateCube(_towelVisual.transform, new Vector3(0f, 2.35f, -.02f),
                new Vector3(1.08f, .48f, .94f), new Color(.96f, .9f, .78f));
            SalonPrimitiveFactory.CreateCube(_towelVisual.transform, new Vector3(.42f, 2.58f, .08f),
                new Vector3(.38f, .42f, .38f), new Color(.88f, .8f, .68f));
            SalonPrimitiveFactory.CreateCube(_towelVisual.transform, new Vector3(-.5f, 2.34f, -.01f),
                new Vector3(.22f, .6f, .76f), new Color(.9f, .83f, .71f));
            SalonPrimitiveFactory.CreateCube(_towelVisual.transform, new Vector3(.52f, 2.35f, -.01f),
                new Vector3(.2f, .58f, .74f), new Color(.84f, .75f, .64f));
        }
        if (!wrapped && _towelRemovalAnimating) return;
        _towelVisual.SetActive(wrapped);
        if (wrapped) _towelVisual.transform.localScale = Vector3.one;
        _towelVisual.transform.localPosition = Vector3.down * (_isSeated ? .43f : 0f);
        for (int i = 0; i < _hairParts.Count; i++)
            _hairParts[i].gameObject.SetActive(!wrapped);
    }

    public void SetWashVisual(CustomerModel customer)
    {
        if (customer == null) return;
        float actionProgress = customer.ActiveServiceDuration <= .001f ? 0f :
            Mathf.Clamp01(customer.ActiveServiceElapsed / customer.ActiveServiceDuration);
        CustomerPhysicalStateSnapshot physical = customer.ServicePhysicalState;
        float wetness = physical == null ? (customer.HairWet ? 1f : 0f) : physical.Wetness;
        float foam = physical == null ? (customer.ShampooApplied ? 1f : 0f) : physical.FoamAmount;
        if (customer.WashStage == WashStage.Wetting)
            wetness = Mathf.Lerp(wetness, 1f, actionProgress);
        else if (customer.WashStage == WashStage.Shampooing && wetness >= .35f)
            foam = Mathf.Lerp(foam, 1f, actionProgress);
        else if (customer.WashStage == WashStage.Rinsing)
            foam = Mathf.Lerp(foam, 0f, actionProgress);
        else if (physical != null && physical.ShampooState == ShampooState.ClumpedOnDryHair)
            foam = Mathf.Max(foam, .15f);

        for (int i = 0; i < _hairParts.Count; i++)
        {
            Renderer renderer = _hairParts[i].GetComponent<Renderer>();
            Color baseColor = i < _hairBaseColors.Count ? _hairBaseColors[i] : HairColor;
            renderer.sharedMaterial.color = Color.Lerp(baseColor, baseColor * .57f, wetness);
        }
        int visibleFoam = Mathf.CeilToInt(_foamParts.Count * Mathf.Clamp01(foam));
        for (int i = 0; i < _foamParts.Count; i++)
            _foamParts[i].SetActive(i < visibleFoam && !customer.TowelWrapped);
        bool showDroplets = customer.HairWet && foam <= .01f && !customer.TowelWrapped;
        for (int i = 0; i < _wetDroplets.Count; i++)
            _wetDroplets[i].SetActive(showDroplets);
        if (_waterVisual != null)
        {
            bool spraying = customer.WashStage == WashStage.Wetting || customer.WashStage == WashStage.Rinsing;
            _waterVisual.SetActive(spraying);
            if (spraying)
                _waterVisual.transform.localPosition = new Vector3(0f, 2.65f - Mathf.Repeat(Time.time * 2.8f, .42f), -.12f);
        }
    }

    public void ApplyDyeFailureVisual(bool failed)
    {
        if (!failed) return;
        Color[] rainbow =
        {
            new Color(.96f, .2f, .28f),
            new Color(1f, .65f, .12f),
            new Color(.2f, .78f, .35f),
            new Color(.2f, .58f, .96f),
            new Color(.65f, .28f, .92f),
            new Color(.96f, .25f, .68f)
        };
        for (int i = 0; i < _hairParts.Count; i++)
        {
            Renderer renderer = _hairParts[i].GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial.color = rainbow[i % rainbow.Length];
        }
    }

    public void PlayShampooSqueeze()
    {
        if (_shampooBottleVisual == null) return;
        StopCoroutine(nameof(ShampooSqueezeRoutine));
        StartCoroutine(nameof(ShampooSqueezeRoutine));
    }

    public void PlayTowelSnap()
    {
        if (_towelVisual == null) return;
        _towelVisual.transform.localScale = Vector3.one * 1.18f;
        StartCoroutine(TowelSnapRoutine());
    }

    public void PlayTowelRemoval()
    {
        if (_towelVisual == null) return;
        StopCoroutine(nameof(TowelRemovalRoutine));
        _towelRemovalAnimating = true;
        _towelVisual.SetActive(true);
        _towelVisual.transform.localScale = Vector3.one;
        StartCoroutine(nameof(TowelRemovalRoutine));
    }

    private void BuildWashFeedback()
    {
        var washRoot = new GameObject("Wash Visual Feedback").transform;
        washRoot.SetParent(transform, false);
        Vector3[] foamPositions =
        {
            new Vector3(-.42f, 2.35f, -.48f), new Vector3(-.12f, 2.58f, -.52f),
            new Vector3(.22f, 2.5f, -.48f), new Vector3(.46f, 2.3f, -.42f),
            new Vector3(-.34f, 2.68f, -.22f), new Vector3(.34f, 2.67f, -.18f),
            new Vector3(0f, 2.78f, -.3f), new Vector3(.08f, 2.28f, -.63f)
        };
        for (int i = 0; i < foamPositions.Length; i++)
        {
            float size = .18f + (i % 3) * .035f;
            GameObject foam = SalonPrimitiveFactory.CreateCube(washRoot, foamPositions[i],
                new Vector3(size * 1.25f, size, size * .9f), new Color(.96f, .98f, 1f));
            foam.name = "Foam " + i;
            foam.transform.localRotation = Quaternion.Euler(i * 13f, i * 29f, i * 17f);
            foam.SetActive(false);
            _foamParts.Add(foam);
        }
        Vector3[] dropletPositions =
        {
            new Vector3(-.48f, 2.24f, -.5f),
            new Vector3(.5f, 2.32f, -.46f),
            new Vector3(.18f, 2.08f, -.66f)
        };
        for (int i = 0; i < dropletPositions.Length; i++)
        {
            GameObject droplet = SalonPrimitiveFactory.CreateCube(washRoot, dropletPositions[i],
                new Vector3(.07f, .16f, .06f), new Color(.32f, .72f, .9f));
            droplet.name = "Wet Hair Droplet " + i;
            droplet.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            droplet.SetActive(false);
            _wetDroplets.Add(droplet);
        }
        _waterVisual = SalonPrimitiveFactory.CreateCylinder(washRoot, new Vector3(0f, 2.6f, -.12f),
            new Vector3(.06f, .34f, .06f), new Color(.38f, .78f, .92f));
        _waterVisual.name = "Shower Water Stream";
        _waterVisual.SetActive(false);
        _shampooBottleVisual = SalonPrimitiveFactory.CreateCylinder(washRoot, new Vector3(.68f, 2.55f, -.2f),
            new Vector3(.18f, .38f, .18f), new Color(.72f, .42f, .78f));
        _shampooBottleVisual.name = "Shampoo Squeeze";
        _shampooBottleVisual.SetActive(false);
    }

    private IEnumerator ShampooSqueezeRoutine()
    {
        _shampooBottleVisual.SetActive(true);
        _shampooBottleVisual.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
        yield return new WaitForSeconds(.28f);
        _shampooBottleVisual.SetActive(false);
    }

    private IEnumerator TowelSnapRoutine()
    {
        float elapsed = 0f;
        while (elapsed < .18f)
        {
            elapsed += Time.deltaTime;
            _towelVisual.transform.localScale = Vector3.Lerp(Vector3.one * 1.18f, Vector3.one, elapsed / .18f);
            yield return null;
        }
        _towelVisual.transform.localScale = Vector3.one;
    }

    private IEnumerator TowelRemovalRoutine()
    {
        float elapsed = 0f;
        Vector3 startPosition = Vector3.down * (_isSeated ? .43f : 0f);
        while (elapsed < .2f)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / .2f);
            _towelVisual.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * .15f, progress);
            _towelVisual.transform.localPosition = startPosition + Vector3.up * (.32f * progress);
            yield return null;
        }
        _towelRemovalAnimating = false;
        _towelVisual.SetActive(false);
        _towelVisual.transform.localScale = Vector3.one;
        _towelVisual.transform.localPosition = startPosition;
    }

    public void ApplyHairStage(HairStage stage)
    {
        _hairStage = stage;
        float scale = stage == HairStage.Trimmed ? .84f : stage == HairStage.Complete ? .7f : stage == HairStage.Overcut ? .43f : 1f;
        float lower = stage == HairStage.Overcut ? .18f : stage == HairStage.Complete ? .1f : stage == HairStage.Trimmed ? .05f : 0f;
        float seatedOffset = _isSeated ? .43f : 0f;
        for (int i = 0; i < _hairParts.Count; i++)
        {
            _hairParts[i].localScale = _hairScales[i] * scale;
            _hairParts[i].localPosition = _hairPositions[i] + Vector3.down * (lower + seatedOffset);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _suppressClick = Owner != null && Owner.HandleHaircutPointerDown(this, eventData.pointerId);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_suppressClick) Owner?.HandleHaircutPointerUp(this, eventData.pointerId);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_suppressClick)
        {
            _suppressClick = false;
            return;
        }
        Owner?.HandleCustomerClick(Customer);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Once pointer-down has successfully started a hold action, small mouse/touch drift
        // belongs to that hold. Unity still emits BeginDrag after crossing its drag threshold;
        // treating it as customer movement used to cancel ApplyDye/ApplyPerm intermittently.
        if (_suppressClick) return;
        if (Customer == null ||
            (Customer.State != CustomerState.Waiting && Customer.State != CustomerState.Serving)) return;
        if (Customer.ActiveServiceAction != ActiveServiceAction.None)
            Owner?.HandleHaircutPointerCancel(this, eventData.pointerId);
        if (Customer.ActiveServiceAction != ActiveServiceAction.None) return;
        _dragging = true;
        _suppressClick = true;
        transform.localScale = BaseScale * 1.12f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        transform.localScale = BaseScale * (1.1f + Mathf.Sin(Time.unscaledTime * 10f) * .025f);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;
        transform.localScale = BaseScale;
        Camera camera = Camera.main;
        if (camera == null) return;
        Ray ray = camera.ScreenPointToRay(eventData.position);
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) return;
        SalonStationClick station = hit.collider.GetComponentInParent<SalonStationClick>();
        if (station != null) Owner?.HandleCustomerDrop(Customer, station.StationId);
    }

}

public sealed class SalonTutorialToolButton : MonoBehaviour
{
    public ActiveServiceAction Action;
}

public sealed class SalonTutorialFingerPulse : MonoBehaviour
{
    private RectTransform _rect;
    private Vector2 _basePosition;

    private void Awake()
    {
        _rect = transform as RectTransform;
        if (_rect != null) _basePosition = _rect.anchoredPosition;
    }

    private void Update()
    {
        if (_rect == null) return;
        float wave = (Mathf.Sin(Time.unscaledTime * 5.5f) + 1f) * .5f;
        _rect.anchoredPosition = _basePosition + Vector2.up * Mathf.Lerp(0f, 8f, wave);
        transform.localScale = Vector3.one * Mathf.Lerp(.94f, 1.06f, wave);
    }
}

public sealed class SalonFloorClick : MonoBehaviour, IPointerClickHandler
{
    public SalonDemo Owner;

    public void OnPointerClick(PointerEventData eventData)
    {
        Owner?.HandleBlankClick();
    }
}

public sealed class SalonStationClick : MonoBehaviour, IPointerClickHandler
{
    public SalonDemo Owner;
    public int StationId;

    public void OnPointerClick(PointerEventData eventData)
    {
        Owner?.HandleStationClick(StationId);
    }
}

public sealed class SalonSafeArea : MonoBehaviour
{
    private Rect _lastSafeArea;

    private void Start() => Apply();

    private void Update()
    {
        if (Screen.safeArea != _lastSafeArea) Apply();
    }

    private void Apply()
    {
        _lastSafeArea = Screen.safeArea;
        var rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(_lastSafeArea.xMin / Screen.width, _lastSafeArea.yMin / Screen.height);
        rect.anchorMax = new Vector2(_lastSafeArea.xMax / Screen.width, _lastSafeArea.yMax / Screen.height);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}

public static class SalonUiFactory
{
    private static Sprite _circleSprite;
    private static Sprite _roundedPanelSprite;
    private static Font _packagedUiFont;

    public static Font GetPackagedUiFont()
    {
        if (_packagedUiFont != null) return _packagedUiFont;
        _packagedUiFont = Resources.Load<Font>("Fonts/NotoSansSC-UI");
        return _packagedUiFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    public static void StyleRoundedPanel(GameObject target, Color? outlineColor = null,
        Vector2? outlineDistance = null)
    {
        if (target == null) return;
        Image image = target.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = GetRoundedPanelSprite();
            image.type = Image.Type.Sliced;
        }
        if (!outlineColor.HasValue) return;
        Outline outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
        outline.effectColor = outlineColor.Value;
        outline.effectDistance = outlineDistance ?? new Vector2(0f, -5f);
        outline.useGraphicAlpha = true;
    }

    public static Transform DirectChildRoot(Transform element, Transform container)
    {
        if (element == null || container == null) return null;
        Transform current = element;
        while (current.parent != null && current.parent != container)
            current = current.parent;
        return current.parent == container ? current : null;
    }

    public static void MakeClickThrough(GameObject root)
    {
        if (root == null) return;
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
    }

    public static void HideAndDestroy(GameObject target)
    {
        if (target == null) return;
        target.SetActive(false);
        if (Application.isPlaying) Object.Destroy(target);
    }

    public static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Procedural Low Poly UI Circle";
        var pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * .5f, (size - 1) * .5f);
        float radius = size * .49f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), center);
            float alpha = distance <= radius - 1f ? 1f : Mathf.Clamp01(radius - distance);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        _circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
        _circleSprite.name = "Low Poly UI Circle";
        return _circleSprite;
    }

    public static Sprite GetRoundedPanelSprite()
    {
        if (_roundedPanelSprite != null) return _roundedPanelSprite;
        const int size = 64;
        const float radius = 13f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Procedural Rounded UI Panel";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * .5f, (size - 1) * .5f);
        Vector2 inner = new Vector2((size - 1) * .5f - radius, (size - 1) * .5f - radius);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 delta = new Vector2(Mathf.Abs(x - center.x), Mathf.Abs(y - center.y));
            Vector2 corner = new Vector2(Mathf.Max(delta.x - inner.x, 0f),
                Mathf.Max(delta.y - inner.y, 0f));
            float signedDistance = corner.magnitude - radius;
            float alpha = Mathf.Clamp01(.75f - signedDistance);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        _roundedPanelSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(16f, 16f, 16f, 16f));
        _roundedPanelSprite.name = "Rounded UI Panel";
        return _roundedPanelSprite;
    }

    public static Text CreateLabel(string text, Transform parent, Color textColor, Color backingColor)
    {
        var panel = new GameObject("Label Backing", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        var image = panel.GetComponent<Image>();
        image.color = backingColor;
        image.raycastTarget = false;
        StyleRoundedPanel(panel);
        var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panel.transform, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var label = textObject.GetComponent<Text>();
        label.text = text;
        label.color = textColor;
        label.font = GetPackagedUiFont();
        label.raycastTarget = false;
        return label;
    }
}

public static class SalonPrimitiveFactory
{
    public static GameObject CreateCube(Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        return Create(PrimitiveType.Cube, parent, localPosition, localScale, color);
    }

    public static GameObject CreateCylinder(Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        return Create(PrimitiveType.Cylinder, parent, localPosition, localScale, color);
    }

    public static GameObject CreateSphere(Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        return Create(PrimitiveType.Sphere, parent, localPosition, localScale, color);
    }

    private static GameObject Create(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        var shader = Resources.Load<Shader>("SalonLowPoly");
        go.GetComponent<Renderer>().material = new Material(shader) { color = color };
        return go;
    }
}
