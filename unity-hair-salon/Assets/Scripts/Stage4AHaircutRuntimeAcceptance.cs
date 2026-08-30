using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HairSalon;
using HairSalon.ServiceArchitecture;
using UnityEngine;

public sealed class Stage4AHaircutAcceptanceOptions
{
    public string OutputDirectory;
    public string Scenario;
}

[Serializable]
public sealed class Stage4AHaircutAcceptanceReport
{
    public string scenario;
    public bool passed;
    public string result;
    public bool towelWrapped;
    public string towelCondition;
    public float foamAmount;
    public float scissorsProgress;
    public float thinningProgress;
    public float clippersProgress;
    public float hairLengthDeviation;
    public int physicalRevision;
    public int actionHistoryCount;
    public float satisfaction;
    public string failure;
}

/// <summary>仅由命令行启用的阶段4A正式剪发链录屏驱动。</summary>
public sealed class Stage4AHaircutRuntimeAcceptance : MonoBehaviour
{
    private SalonDemo _owner;
    private Stage4AHaircutAcceptanceOptions _options;
    private CustomerModel _customer;
    private string _frameDirectory;
    private string _status;
    private string _lastResult;
    private int _frame;
    private bool _scenarioPassed;

    public static Stage4AHaircutAcceptanceOptions ConfigureFromCommandLine(DayConfig config)
    {
        string[] args = Environment.GetCommandLineArgs();
        string output = null;
        string scenario = null;
        for (int index = 0; index < args.Length; index++)
        {
            if (args[index] == "-salonPhase4ARecord" && index + 1 < args.Length) output = args[index + 1];
            if (args[index] == "-salonPhase4AScenario" && index + 1 < args.Length) scenario = args[index + 1];
        }
        if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(scenario)) return null;
        config.StartingDuration = .05f;
        config.BusinessDuration = 60f;
        config.ClosingGraceDuration = 2f;
        config.MinSpawnInterval = 100f;
        config.MaxSpawnInterval = 100f;
        config.MaxConcurrentCustomers = 5;
        return new Stage4AHaircutAcceptanceOptions
        {
            OutputDirectory = output,
            Scenario = scenario.ToUpperInvariant()
        };
    }

    public void Initialize(SalonDemo owner, Stage4AHaircutAcceptanceOptions options)
    {
        _owner = owner;
        _options = options;
        _frameDirectory = Path.Combine(options.OutputDirectory, options.Scenario, "frames");
        Directory.CreateDirectory(_frameDirectory);
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        var report = new Stage4AHaircutAcceptanceReport { scenario = _options.Scenario };
        _owner.RuntimeStartBusinessDay();
        yield return new WaitForSecondsRealtime(.2f);
        switch (_options.Scenario)
        {
            case "A": yield return NormalUntowelAndHaircut(); break;
            case "B": yield return HaircutThroughTowel(); break;
            case "C": yield return UnderPerfectOver(); break;
            case "D": yield return WrongToolRecovery(); break;
            case "E": yield return FoamyTransferAndHaircut(); break;
            default: throw new ArgumentException("Unknown scenario " + _options.Scenario);
        }

        if (_customer != null)
        {
            CustomerPhysicalStateSnapshot physical = _owner.RuntimeGame.GetServicePhysicalSnapshot(_customer);
            report.towelWrapped = physical.IsTowelWrapped;
            report.towelCondition = physical.TowelCondition.ToString();
            report.foamAmount = physical.FoamAmount;
            report.scissorsProgress = physical.HaircutProgress(HaircutTool.Scissors);
            report.thinningProgress = physical.HaircutProgress(HaircutTool.ThinningShears);
            report.clippersProgress = physical.HaircutProgress(HaircutTool.Clippers);
            report.hairLengthDeviation = physical.HairLengthDeviation;
            report.physicalRevision = physical.PhysicalStateRevision;
            report.actionHistoryCount = _owner.RuntimeGame.GetServiceActionHistory(_customer).Count;
            report.satisfaction = _customer.Satisfaction;
        }
        report.result = _lastResult;
        report.passed = _scenarioPassed;
        yield return RecordFor(report.passed ? "验收通过" : "验收失败", 1f);
        string scenarioDirectory = Path.Combine(_options.OutputDirectory, _options.Scenario);
        File.WriteAllText(Path.Combine(scenarioDirectory, "report.json"), JsonUtility.ToJson(report, true));
        yield return new WaitForSecondsRealtime(.3f);
        Application.Quit(report.passed ? 0 : 2);
    }

    private IEnumerator NormalUntowelAndHaircut()
    {
        yield return PrepareWashedAndWrappedCustomer(4401);
        yield return MoveToHaircut(_customer, 1, "A 移动到剪发区");
        QuickRemoveTowel("A 正常拆毛巾");
        yield return RecordFor(_status, .8f);
        HaircutResult result = Haircut(_customer, SalonTool.Scissors, 2f, "A 剪刀 PERFECT");
        yield return RecordFor(_status, .9f);
        CustomerPhysicalStateSnapshot physical = _owner.RuntimeGame.GetServicePhysicalSnapshot(_customer);
        _scenarioPassed = result == HaircutResult.Perfect && !physical.IsTowelWrapped
            && physical.HaircutProgress(HaircutTool.Scissors) >= 1f;
    }

    private IEnumerator HaircutThroughTowel()
    {
        yield return PrepareWashedAndWrappedCustomer(4402);
        yield return MoveToHaircut(_customer, 1, "B 毛巾未拆移动到剪发区");
        float satisfaction = _customer.Satisfaction;
        HaircutResult result = Haircut(_customer, SalonTool.Scissors, 2f, "B 毛巾未拆直接剪");
        yield return RecordFor(_status, 1f);
        CustomerPhysicalStateSnapshot physical = _owner.RuntimeGame.GetServicePhysicalSnapshot(_customer);
        _scenarioPassed = result == HaircutResult.WrongTool && physical.IsTowelWrapped
            && physical.TowelCondition == TowelCondition.Damaged
            && physical.HaircutProgress(HaircutTool.Scissors) == 0f
            && _customer.Satisfaction < satisfaction;
    }

    private IEnumerator UnderPerfectOver()
    {
        CustomerModel recovered = SpawnCutCustomer(4403, SalonTool.Scissors);
        CustomerModel overcut = SpawnCutCustomer(4404, SalonTool.Scissors);
        yield return RecordFor("C 两位剪发顾客进入", SalonGameModel.EnteringSeconds + .2f);
        if (!_owner.RuntimeGame.Assign(recovered, 1) || !_owner.RuntimeGame.Assign(overcut, 2))
            throw new InvalidOperationException("C haircut station assignment failed.");
        yield return RecordFor("C 两位顾客到达剪发区", SalonGameModel.MovingToStationSeconds + .2f);

        _customer = recovered;
        _owner.RuntimeGame.SelectCustomer(recovered);
        HaircutResult under = Haircut(recovered, SalonTool.Scissors, .5f, "C UNDER");
        yield return RecordFor(_status, .8f);
        HaircutResult perfect = Haircut(recovered, SalonTool.Scissors, 2f, "C PERFECT 补剪");
        yield return RecordFor(_status, .8f);

        _customer = overcut;
        _owner.RuntimeGame.SelectCustomer(overcut);
        HaircutResult over = Haircut(overcut, SalonTool.Scissors, 3f, "C OVER");
        yield return RecordFor(_status, 1f);
        CustomerPhysicalStateSnapshot physical = _owner.RuntimeGame.GetServicePhysicalSnapshot(overcut);
        _scenarioPassed = under == HaircutResult.Undercut && perfect == HaircutResult.Perfect
            && over == HaircutResult.Overcut && physical.OvercutSeverity > 0f;
    }

    private IEnumerator WrongToolRecovery()
    {
        _customer = SpawnCutCustomer(4405, SalonTool.Scissors);
        yield return RecordFor("D 顾客进入", SalonGameModel.EnteringSeconds + .2f);
        yield return MoveToHaircut(_customer, 1, "D 到达剪发区");
        float satisfaction = _customer.Satisfaction;
        HaircutResult wrong = Haircut(_customer, SalonTool.Clippers, 1f, "D 错误推子");
        yield return RecordFor(_status, .8f);
        HaircutResult recovered = Haircut(_customer, SalonTool.Scissors, 2f, "D 剪刀补救");
        yield return RecordFor(_status, .9f);
        CustomerPhysicalStateSnapshot physical = _owner.RuntimeGame.GetServicePhysicalSnapshot(_customer);
        _scenarioPassed = wrong == HaircutResult.WrongTool && recovered == HaircutResult.Perfect
            && physical.HaircutProgress(HaircutTool.Clippers) > 0f
            && physical.HaircutProgress(HaircutTool.Scissors) >= 1f
            && _customer.Satisfaction < satisfaction;
    }

    private IEnumerator FoamyTransferAndHaircut()
    {
        _customer = SpawnCustomer(4406, new List<ServiceType> { ServiceType.Wash, ServiceType.Cut });
        yield return RecordFor("E 顾客进入", SalonGameModel.EnteringSeconds + .2f);
        if (!_owner.RuntimeGame.Assign(_customer, 0))
            throw new InvalidOperationException("E wash station assignment failed.");
        yield return RecordFor("E 到达洗头床", SalonGameModel.MovingToStationSeconds + .2f);
        _owner.RuntimeGame.SelectCustomer(_customer);
        CompleteWash(WashAction.Shower, _owner.RuntimeGame.ServiceConfig.RinseDuration);
        yield return RecordFor("E 花洒打湿", .6f);
        CompleteWash(WashAction.Shampoo, _owner.RuntimeGame.ServiceConfig.ShampooDuration);
        yield return RecordFor("E 头发起泡", .8f);
        float foam = _owner.RuntimeGame.GetServicePhysicalSnapshot(_customer).FoamAmount;
        float satisfaction = _customer.Satisfaction;
        yield return MoveToHaircut(_customer, 1, "E 泡沫顾客搬到剪发区");
        HaircutResult result = Haircut(_customer, SalonTool.Scissors, 2f, "E 带泡沫继续剪发");
        yield return RecordFor(_status, 1f);
        CustomerPhysicalStateSnapshot physical = _owner.RuntimeGame.GetServicePhysicalSnapshot(_customer);
        _scenarioPassed = result == HaircutResult.Perfect && physical.FoamAmount == foam
            && physical.HaircutProgress(HaircutTool.Scissors) >= 1f
            && _customer.Satisfaction < satisfaction;
    }

    private IEnumerator PrepareWashedAndWrappedCustomer(int id)
    {
        _customer = SpawnCustomer(id, new List<ServiceType> { ServiceType.Wash, ServiceType.Cut });
        yield return RecordFor("顾客进入", SalonGameModel.EnteringSeconds + .2f);
        if (!_owner.RuntimeGame.Assign(_customer, 0))
            throw new InvalidOperationException("Wash station assignment failed.");
        yield return RecordFor("移动到洗头床", SalonGameModel.MovingToStationSeconds + .2f);
        _owner.RuntimeGame.SelectCustomer(_customer);
        CompleteWash(WashAction.Shower, _owner.RuntimeGame.ServiceConfig.RinseDuration);
        CompleteWash(WashAction.Shampoo, _owner.RuntimeGame.ServiceConfig.ShampooDuration);
        CompleteWash(WashAction.Shower, _owner.RuntimeGame.ServiceConfig.RinseDuration);
        if (_owner.RuntimeGame.PerformQuickAction(_customer, ActiveServiceAction.WrapTowel)
            != ServiceActionResult.QuickActionCompleted)
            throw new InvalidOperationException("Wrap towel failed.");
        yield return RecordFor("洗净并包毛巾", .9f);
    }

    private CustomerModel SpawnCutCustomer(int id, SalonTool requiredTool)
    {
        CustomerModel customer = SpawnCustomer(id, new List<ServiceType> { ServiceType.Cut });
        if (!_owner.RuntimeGame.ConfigureHaircutOrder(customer, requiredTool))
            throw new InvalidOperationException("Configure haircut order failed.");
        return customer;
    }

    private CustomerModel SpawnCustomer(int id, IList<ServiceType> needs)
    {
        CustomerModel customer = _owner.RuntimeGame.Spawn(id, needs);
        if (customer == null) throw new InvalidOperationException("Customer spawn failed.");
        return customer;
    }

    private IEnumerator MoveToHaircut(CustomerModel customer, int station, string status)
    {
        _status = status;
        if (!_owner.RuntimeGame.Assign(customer, station))
            throw new InvalidOperationException("Haircut station assignment failed.");
        yield return RecordFor(status, SalonGameModel.MovingToStationSeconds + .3f);
        _owner.RuntimeGame.SelectCustomer(customer);
    }

    private void CompleteWash(WashAction action, float duration)
    {
        if (!_owner.RuntimeGame.BeginWashAction(_customer, action)
            || !_owner.RuntimeGame.TickActiveServiceAction(_customer, duration))
            throw new InvalidOperationException("Wash action failed: " + action);
    }

    private void QuickRemoveTowel(string status)
    {
        _status = status;
        if (_owner.RuntimeGame.PerformQuickAction(_customer, ActiveServiceAction.RemoveTowel)
            != ServiceActionResult.QuickActionCompleted)
            throw new InvalidOperationException("Remove towel failed.");
    }

    private HaircutResult Haircut(CustomerModel customer, SalonTool tool, float elapsed, string status)
    {
        _status = status;
        var config = new HaircutConfig();
        if (!_owner.RuntimeGame.BeginHaircutAction(customer, tool, config))
            throw new InvalidOperationException("Begin haircut failed: " + tool);
        HaircutResult result = _owner.RuntimeGame.CompleteHaircutAction(customer, elapsed, false);
        _owner.RuntimeGame.EndActiveOperation(customer);
        _lastResult = result.ToString();
        return result;
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
            GUI.Box(new Rect(25f, 25f, 560f, 62f), "阶段4A录屏测试\n" + _status);
    }
}
