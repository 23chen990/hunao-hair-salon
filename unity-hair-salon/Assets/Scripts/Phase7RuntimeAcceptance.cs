using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HairSalon;
using UnityEngine;

public sealed class Phase7AcceptanceOptions
{
    public string ReportPath;
    public bool BuyAutoBlow;
    public bool CollectDay1Income;
}

[Serializable]
public sealed class Phase7AcceptanceReport
{
    public string mode;
    public int day1Spawned;
    public int day1Served;
    public int day1OrderIncome;
    public int day1TipIncome;
    public int day1OperatingNetIncome;
    public int day1UnservedAtClose;
    public int day1IncompleteAtClose;
    public bool reachedDay1Result;
    public bool reachedDay1Shop;
    public bool purchaseSucceeded;
    public int day2Number;
    public bool purchasePersistedOnDay2;
    public bool autoBlowStartedOnDay2;
    public bool autoBlowAdvancedInBackground;
    public bool pauseFrozeBusinessClock;
    public bool reachedDay2Result;
    public bool reachedDay2Shop;
}

public sealed class Phase7RuntimeAcceptance : MonoBehaviour
{
    private SalonDemo _owner;
    private Phase7AcceptanceOptions _options;

    public static Phase7AcceptanceOptions ConfigureFromCommandLine(DayConfig config)
    {
        string[] args = Environment.GetCommandLineArgs();
        string reportPath = null;
        bool buy = false;
        bool collect = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-salonPhase7Buy") buy = true;
            if (args[i] == "-salonPhase7Collect") collect = true;
            if (args[i] == "-salonPhase7Smoke" && i + 1 < args.Length) reportPath = args[i + 1];
        }
        if (string.IsNullOrEmpty(reportPath)) return null;
        config.StartingDuration = .15f;
        config.BusinessDuration = 4f;
        config.ClosingGraceDuration = 1f;
        config.MinSpawnInterval = .35f;
        config.MaxSpawnInterval = .5f;
        config.MaxConcurrentCustomers = 6;
        config.PressurePhase2Start = .2f;
        config.PressurePhase3Start = .5f;
        config.PressurePhase4Start = .8f;
        config.Rush.StartProgress = .45f;
        config.Rush.DurationProgress = .2f;
        config.Rush.IntensityMultiplier = 1.7f;
        return new Phase7AcceptanceOptions
        {
            ReportPath = reportPath,
            BuyAutoBlow = buy,
            CollectDay1Income = collect
        };
    }

    public void Initialize(SalonDemo owner, Phase7AcceptanceOptions options)
    {
        _owner = owner;
        _options = options;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        var report = new Phase7AcceptanceReport { mode = _options.BuyAutoBlow ? "buy" : "skip" };
        _owner.RuntimeStartBusinessDay();
        yield return WaitForState(DayState.Business, 2f);
        yield return CompleteOneCutWithoutCollecting();
        if (_options.CollectDay1Income)
        {
            CoinPileView pile = FindAnyObjectByType<CoinPileView>();
            if (pile != null) pile.OnPointerClick(null);
            yield return new WaitForSecondsRealtime(.95f);
        }
        yield return WaitForState(DayState.Result, 8f);
        report.reachedDay1Result = _owner.RuntimeDay.State == DayState.Result;
        DayStats day1 = _owner.RuntimeDay.Stats;
        report.day1Spawned = day1.SpawnedCustomers;
        report.day1Served = day1.ServedCustomers;
        report.day1OrderIncome = day1.OrderIncome;
        report.day1TipIncome = day1.TipIncome;
        report.day1OperatingNetIncome = day1.OperatingNetIncome;
        report.day1UnservedAtClose = day1.UnservedAtClose;
        report.day1IncompleteAtClose = day1.IncompleteAtClose;

        _owner.RuntimeContinueToShop();
        yield return null;
        report.reachedDay1Shop = _owner.RuntimeDay.State == DayState.ClosedManagement;
        if (_options.BuyAutoBlow)
            report.purchaseSucceeded = _owner.RuntimeGame.PurchaseAutoBlowStand();
        _owner.RuntimeBeginNextDay();
        yield return null;
        _owner.RuntimeStartBusinessDay();
        yield return WaitForState(DayState.Business, 2f);
        report.day2Number = _owner.RuntimeDay.DayNumber;
        report.purchasePersistedOnDay2 = _owner.RuntimeGame.HasAutoBlowStand == _options.BuyAutoBlow;

        float beforePause = _owner.RuntimeDay.BusinessRemainingTime;
        _owner.RuntimeDay.SetPaused(true);
        yield return new WaitForSecondsRealtime(.35f);
        report.pauseFrozeBusinessClock = Math.Abs(_owner.RuntimeDay.BusinessRemainingTime - beforePause) < .001f;
        _owner.RuntimeDay.SetPaused(false);

        int customerId = 9000 + _owner.RuntimeDay.DayNumber;
        CustomerModel dryCustomer = _owner.RuntimeGame.Spawn(customerId,
            new List<ServiceType> { ServiceType.Dry });
        if (dryCustomer != null)
        {
            _owner.RuntimeDay.Stats.RecordSpawn(dryCustomer);
            yield return new WaitForSecondsRealtime(SalonGameModel.EnteringSeconds + .1f);
            int station = FindFreeHaircutStation();
            if (station >= 0 && _owner.RuntimeGame.Assign(dryCustomer, station))
            {
                yield return new WaitForSecondsRealtime(SalonGameModel.MovingToStationSeconds + .1f);
                report.autoBlowStartedOnDay2 = _owner.RuntimeGame.StartAutoBlow(dryCustomer);
                float elapsed = dryCustomer.BackgroundTask.Elapsed;
                yield return new WaitForSecondsRealtime(.25f);
                report.autoBlowAdvancedInBackground = report.autoBlowStartedOnDay2 &&
                                                      dryCustomer.BackgroundTask.Elapsed > elapsed;
            }
        }

        yield return WaitForState(DayState.Result, 8f);
        report.reachedDay2Result = _owner.RuntimeDay.State == DayState.Result;
        _owner.RuntimeContinueToShop();
        yield return null;
        report.reachedDay2Shop = _owner.RuntimeDay.State == DayState.ClosedManagement;

        string directory = Path.GetDirectoryName(_options.ReportPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(_options.ReportPath, JsonUtility.ToJson(report, true));
        ScreenCapture.CaptureScreenshot(Path.ChangeExtension(_options.ReportPath, ".png"));
        yield return new WaitForSecondsRealtime(.75f);
        Application.Quit();
    }

    private IEnumerator CompleteOneCutWithoutCollecting()
    {
        CustomerModel customer = _owner.RuntimeGame.Spawn(8001,
            new List<ServiceType> { ServiceType.Cut });
        if (customer == null) yield break;
        _owner.RuntimeDay.Stats.RecordSpawn(customer);
        yield return new WaitForSecondsRealtime(SalonGameModel.EnteringSeconds + .1f);
        int station = FindFreeHaircutStation();
        if (station < 0 || !_owner.RuntimeGame.Assign(customer, station)) yield break;
        yield return new WaitForSecondsRealtime(SalonGameModel.MovingToStationSeconds + .1f);
        _owner.RuntimeGame.ApplyHaircutResult(customer, SalonTool.Scissors,
            HaircutResult.Perfect, _owner.HaircutSettings);
    }

    private IEnumerator WaitForState(DayState state, float timeout)
    {
        float elapsed = 0f;
        while (_owner.RuntimeDay.State != state && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private int FindFreeHaircutStation()
    {
        for (int i = 0; i < _owner.RuntimeGame.Workstations.Count; i++)
            if (_owner.RuntimeGame.Workstations[i].Type == WorkstationType.Haircut &&
                !_owner.RuntimeGame.IsStationOccupied(i)) return i;
        return -1;
    }
}
