using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HairSalon;
using HairSalon.ServiceArchitecture;
using UnityEngine;

public sealed class Stage4A5AcceptanceOptions
{
    public string OutputDirectory;
    public string Scenario;
}

[Serializable]
public sealed class Stage4A5AcceptanceReport
{
    public string scenario;
    public bool passed;
    public int wrongStationCount;
    public int extraServiceCount;
    public string reaction;
    public float wetness;
    public float foamAmount;
    public bool orderRequirementsCompleted;
    public bool exitReady;
    public string exitBlockReason;
    public int actionHistoryCount;
    public float satisfaction;
    public bool cleanupVisible;
    public string cleanupText;
}

/// <summary>仅由命令行启用的阶段4A.5错误反馈与离店规则录屏驱动。</summary>
public sealed class Stage4A5FreedomRuntimeAcceptance : MonoBehaviour
{
    private SalonDemo _owner;
    private Stage4A5AcceptanceOptions _options;
    private CustomerModel _customer;
    private string _frameDirectory;
    private string _status;
    private int _frame;
    private bool _passed;

    public static Stage4A5AcceptanceOptions ConfigureFromCommandLine(DayConfig config)
    {
        string[] args = Environment.GetCommandLineArgs();
        string output = null;
        string scenario = null;
        for (int index = 0; index < args.Length; index++)
        {
            if (args[index] == "-salonPhase4A5Record" && index + 1 < args.Length)
                output = args[index + 1];
            if (args[index] == "-salonPhase4A5Scenario" && index + 1 < args.Length)
                scenario = args[index + 1];
        }
        if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(scenario)) return null;
        config.StartingDuration = .05f;
        config.BusinessDuration = 60f;
        config.ClosingGraceDuration = 2f;
        config.MinSpawnInterval = 100f;
        config.MaxSpawnInterval = 100f;
        config.MaxConcurrentCustomers = 4;
        return new Stage4A5AcceptanceOptions
        {
            OutputDirectory = output,
            Scenario = scenario.ToUpperInvariant()
        };
    }

    public void Initialize(SalonDemo owner, Stage4A5AcceptanceOptions options)
    {
        _owner = owner;
        _options = options;
        _frameDirectory = Path.Combine(options.OutputDirectory, options.Scenario, "frames");
        Directory.CreateDirectory(_frameDirectory);
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        _owner.RuntimeStartBusinessDay();
        yield return new WaitForSecondsRealtime(.2f);
        if (_options.Scenario == "A") yield return WrongWash();
        else if (_options.Scenario == "B") yield return CompletedButBlocked();
        else if (_options.Scenario == "C") yield return NormalCustomer();
        else throw new ArgumentException("Unknown scenario " + _options.Scenario);

        CustomerPhysicalStateSnapshot physical =
            _owner.RuntimeGame.GetServicePhysicalSnapshot(_customer);
        OrderDemandBubbleView bubble = FindOrderBubble();
        var report = new Stage4A5AcceptanceReport
        {
            scenario = _options.Scenario,
            passed = _passed,
            wrongStationCount = _customer.WrongStationCount,
            extraServiceCount = _customer.ExtraServiceCount,
            reaction = _customer.ReactionKind.ToString(),
            wetness = physical.Wetness,
            foamAmount = physical.FoamAmount,
            orderRequirementsCompleted = _customer.OrderRequirementsCompleted,
            exitReady = _customer.ExitReady,
            exitBlockReason = _customer.ExitBlockReason.ToString(),
            actionHistoryCount = _owner.RuntimeGame.GetServiceActionHistory(_customer).Count,
            satisfaction = _customer.Satisfaction,
            cleanupVisible = bubble != null && bubble.CleanupVisible,
            cleanupText = bubble == null ? string.Empty : bubble.CleanupText
        };
        yield return RecordFor(_passed ? "验收通过" : "验收失败", 1f);
        string scenarioDirectory = Path.Combine(_options.OutputDirectory, _options.Scenario);
        File.WriteAllText(Path.Combine(scenarioDirectory, "report.json"),
            JsonUtility.ToJson(report, true));
        yield return new WaitForSecondsRealtime(.2f);
        Application.Quit(_passed ? 0 : 2);
    }

    private IEnumerator WrongWash()
    {
        _customer = SpawnCutCustomer(4451);
        yield return RecordFor("A 剪发顾客进入", SalonGameModel.EnteringSeconds + .2f);
        float satisfaction = _customer.Satisfaction;
        if (!_owner.RuntimeGame.Assign(_customer, 0))
            throw new InvalidOperationException("A wrong-station assignment failed.");
        yield return RecordFor("A 干发后上洗发水", .8f);
        _owner.RuntimeGame.SelectCustomer(_customer);
        if (!_owner.RuntimeGame.BeginWashAction(_customer, WashAction.Shampoo))
            throw new InvalidOperationException("A shampoo failed.");
        if (!_owner.RuntimeGame.TickActiveServiceAction(_customer, _owner.RuntimeGame.ServiceConfig.ShampooDuration))
            throw new InvalidOperationException("A shampoo hold failed.");
        yield return RecordFor("A 洗发水完成", .8f);
        if (!_owner.RuntimeGame.BeginWashAction(_customer, WashAction.Shower))
            throw new InvalidOperationException("A shower failed.");
        if (!_owner.RuntimeGame.TickActiveServiceAction(_customer, _owner.RuntimeGame.ServiceConfig.RinseDuration * .28f))
            throw new InvalidOperationException("A partial rinse failed.");
        if (_owner.RuntimeGame.PerformQuickAction(_customer, ActiveServiceAction.WrapTowel) != ServiceActionResult.QuickActionCompleted)
            throw new InvalidOperationException("A wrap towel failed.");
        yield return RecordFor("A 产生泡沫残留后包毛巾", 1f);
        if (!_owner.RuntimeGame.Assign(_customer, 1))
            throw new InvalidOperationException("A haircut transfer failed.");
        yield return RecordFor("A 转到剪发区继续订单", SalonGameModel.MovingToStationSeconds + .2f);
        _owner.RuntimeGame.SelectCustomer(_customer);
        Haircut(SalonTool.Scissors);
        yield return RecordFor("A 剪刀需求完成", .7f);
        Haircut(SalonTool.ThinningShears);
        OrderDemandBubbleView bubble = FindOrderBubble();
        bubble?.Refresh();
        yield return RecordFor("A 订单完成但仍需处理残留", 1.2f);
        _passed = _customer.OrderRequirementsCompleted
            && _customer.IsComplete
            && _customer.ExtraServiceCount > 0
            && _owner.RuntimeGame.GetServicePhysicalSnapshot(_customer).FoamAmount > 0f
            && _owner.RuntimeGame.GetServicePhysicalSnapshot(_customer).IsTowelWrapped
            && bubble != null
            && bubble.CleanupVisible
            && bubble.CleanupText.Contains("🫧")
            && _customer.Satisfaction < satisfaction;
    }

    private IEnumerator CompletedButBlocked()
    {
        _customer = SpawnCutCustomer(4452);
        yield return RecordFor("B 剪刀 + 分齿剪订单进入", SalonGameModel.EnteringSeconds + .2f);
        if (!_owner.RuntimeGame.Assign(_customer, 0))
            throw new InvalidOperationException("B wash assignment failed.");
        yield return RecordFor("B 错误进入洗头区后误洗", .8f);
        _owner.RuntimeGame.SelectCustomer(_customer);
        CompleteShower();
        yield return RecordFor("B 未要求洗头导致湿发", .8f);
        if (!_owner.RuntimeGame.Assign(_customer, 1))
            throw new InvalidOperationException("B haircut transfer failed.");
        yield return RecordFor("B 湿度保持并移动到剪发区",
            SalonGameModel.MovingToStationSeconds + .2f);
        _owner.RuntimeGame.SelectCustomer(_customer);
        Haircut(SalonTool.Scissors);
        yield return RecordFor("B 剪刀需求完成 ✓", .7f);
        Haircut(SalonTool.ThinningShears);
        yield return RecordFor("B 订单全部完成 · 允许离店", 1.4f);
        _passed = _customer.IsComplete
            && _customer.OrderRequirementsCompleted
            && _customer.ExtraServiceCount == 1
            && _customer.ExitBlockReason == ExitBlockReason.WetHair
            && _customer.State == CustomerState.Finished
            && _owner.RuntimeGame.Payments.Drops.Count > 0
            && _customer.Satisfaction < 70f;
    }

    private IEnumerator NormalCustomer()
    {
        _customer = _owner.RuntimeGame.Spawn(
            4453, new List<ServiceType> { ServiceType.Cut });
        if (_customer == null
            || !_owner.RuntimeGame.ConfigureHaircutOrder(_customer, SalonTool.Scissors))
            throw new InvalidOperationException("C customer setup failed.");
        _owner.RuntimeAttachCustomerView(_customer);
        yield return RecordFor("C 正常剪发顾客进入", SalonGameModel.EnteringSeconds + .2f);
        if (!_owner.RuntimeGame.Assign(_customer, 1))
            throw new InvalidOperationException("C haircut assignment failed.");
        yield return RecordFor("C 正确进入剪发工位 · 无错误反馈",
            SalonGameModel.MovingToStationSeconds + .2f);
        _owner.RuntimeGame.SelectCustomer(_customer);
        Haircut(SalonTool.Scissors);
        yield return RecordFor("C 正确工具完成订单 · 无多余提示", 1.2f);
        _passed = _customer.WrongStationCount == 0
            && _customer.ExtraServiceCount == 0
            && _customer.ReactionKind == CustomerReactionKind.None
            && _customer.ExitReady
            && _customer.State == CustomerState.Finished;
    }

    private CustomerModel SpawnCutCustomer(int id)
    {
        CustomerModel customer = _owner.RuntimeGame.Spawn(
            id, new List<ServiceType> { ServiceType.Cut });
        if (customer == null || !_owner.RuntimeGame.ConfigureHaircutOrder(
                customer, SalonTool.Scissors, SalonTool.ThinningShears))
            throw new InvalidOperationException("Cut customer setup failed.");
        _owner.RuntimeAttachCustomerView(customer);
        return customer;
    }

    private void CompleteShower()
    {
        if (!_owner.RuntimeGame.BeginWashAction(_customer, WashAction.Shower)
            || !_owner.RuntimeGame.TickActiveServiceAction(
                _customer, _owner.RuntimeGame.ServiceConfig.RinseDuration))
            throw new InvalidOperationException("Unrequested shower failed.");
    }

    private void Haircut(SalonTool tool)
    {
        var config = new HaircutConfig();
        if (!_owner.RuntimeGame.BeginHaircutAction(_customer, tool, config))
            throw new InvalidOperationException("Haircut begin failed: " + tool);
        HaircutResult result = _owner.RuntimeGame.CompleteHaircutAction(_customer, 2f, false);
        _owner.RuntimeGame.EndActiveOperation(_customer);
        if (result != HaircutResult.Perfect)
            throw new InvalidOperationException("Haircut did not resolve PERFECT: " + result);
    }

    private ActionResult LastAction()
    {
        IReadOnlyList<ActionHistoryEntry> history =
            _owner.RuntimeGame.GetServiceActionHistory(_customer);
        return history[history.Count - 1].ActionResult;
    }

    private OrderDemandBubbleView FindOrderBubble()
    {
        foreach (OrderDemandBubbleView bubble in FindObjectsByType<OrderDemandBubbleView>(
                     FindObjectsInactive.Include))
            if (bubble.CustomerId == _customer.Id) return bubble;
        return null;
    }

    private IEnumerator RecordFor(string status, float seconds)
    {
        _status = status;
        for (float elapsed = 0f; elapsed < seconds; elapsed += .1f)
        {
            CaptureFrame();
            yield return new WaitForSecondsRealtime(.1f);
        }
    }

    private void CaptureFrame()
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        string path = Path.Combine(_frameDirectory, $"frame_{_frame++:D5}.png");
        const int width = 1280;
        const int height = 720;
        var target = new RenderTexture(width, height, 24);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        Destroy(texture);
        Destroy(target);
    }

    private void OnGUI()
    {
        if (!string.IsNullOrEmpty(_status))
            GUI.Box(new Rect(25f, 25f, 680f, 62f), "阶段4A.5录屏测试\n" + _status);
    }
}
