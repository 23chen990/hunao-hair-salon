using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HairSalon;
using HairSalon.ServiceArchitecture;
using UnityEngine;

public sealed class Stage3WashAcceptanceOptions
{
    public string OutputDirectory;
    public string Scenario;
}

[Serializable]
public sealed class Stage3WashAcceptanceReport
{
    public string scenario;
    public bool passed;
    public float wetness;
    public float foamAmount;
    public string shampooState;
    public bool towelWrapped;
    public string towelContamination;
    public int physicalRevision;
    public int actionHistoryCount;
    public int station;
    public string customerState;
    public string failure;
}

/// <summary>仅由命令行启用的阶段3正式运行链录屏驱动。</summary>
public sealed class Stage3WashRuntimeAcceptance : MonoBehaviour
{
    private SalonDemo _owner;
    private Stage3WashAcceptanceOptions _options;
    private CustomerModel _customer;
    private string _frameDirectory;
    private int _frame;
    private string _status;

    public static Stage3WashAcceptanceOptions ConfigureFromCommandLine(DayConfig config)
    {
        string[] args = Environment.GetCommandLineArgs();
        string output = null;
        string scenario = null;
        for (int index = 0; index < args.Length; index++)
        {
            if (args[index] == "-salonPhase3Record" && index + 1 < args.Length) output = args[index + 1];
            if (args[index] == "-salonPhase3Scenario" && index + 1 < args.Length) scenario = args[index + 1];
        }
        if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(scenario)) return null;
        config.StartingDuration = .05f;
        config.BusinessDuration = 45f;
        config.ClosingGraceDuration = 2f;
        config.MinSpawnInterval = 100f;
        config.MaxSpawnInterval = 100f;
        config.MaxConcurrentCustomers = 5;
        return new Stage3WashAcceptanceOptions
        {
            OutputDirectory = output,
            Scenario = scenario.ToUpperInvariant()
        };
    }

    public void Initialize(SalonDemo owner, Stage3WashAcceptanceOptions options)
    {
        _owner = owner;
        _options = options;
        _frameDirectory = Path.Combine(options.OutputDirectory, options.Scenario, "frames");
        Directory.CreateDirectory(_frameDirectory);
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        var report = new Stage3WashAcceptanceReport { scenario = _options.Scenario };
        _owner.RuntimeStartBusinessDay();
        yield return new WaitForSecondsRealtime(.2f);
        _customer = _owner.RuntimeGame.Spawn(
            3300 + _options.Scenario[0],
            new List<ServiceType> { ServiceType.Wash, ServiceType.Cut });
        if (_customer == null) throw new InvalidOperationException("Customer spawn failed.");
        yield return RecordFor("顾客进入", SalonGameModel.EnteringSeconds + .15f);
        if (!_owner.RuntimeGame.Assign(_customer, 0))
            throw new InvalidOperationException("Wash station assignment failed.");
        yield return RecordFor("移动到洗头床", SalonGameModel.MovingToStationSeconds + .15f);
        _owner.RuntimeGame.SelectCustomer(_customer);
        yield return RecordFor("聚焦顾客", .6f);

        switch (_options.Scenario)
        {
            case "A":
                yield return NormalWash();
                break;
            case "B":
                yield return DryShampooRecovery();
                break;
            case "C":
                yield return FoamyTowelRecovery();
                break;
            case "D":
                yield return FoamyTransfer();
                break;
            default:
                throw new ArgumentException("Unknown scenario " + _options.Scenario);
        }

        CustomerPhysicalStateSnapshot physical =
            _owner.RuntimeGame.GetServicePhysicalSnapshot(_customer);
        report.passed = ValidateScenario(physical);
        report.wetness = physical.Wetness;
        report.foamAmount = physical.FoamAmount;
        report.shampooState = physical.ShampooState.ToString();
        report.towelWrapped = physical.IsTowelWrapped;
        report.towelContamination = physical.TowelContamination.ToString();
        report.physicalRevision = physical.PhysicalStateRevision;
        report.actionHistoryCount = _owner.RuntimeGame.GetServiceActionHistory(_customer).Count;
        report.station = _customer.Station;
        report.customerState = _customer.State.ToString();
        yield return RecordFor(report.passed ? "验收通过" : "验收失败", 1f);
        string scenarioDirectory = Path.Combine(_options.OutputDirectory, _options.Scenario);
        File.WriteAllText(Path.Combine(scenarioDirectory, "report.json"), JsonUtility.ToJson(report, true));
        yield return new WaitForSecondsRealtime(.3f);
        Application.Quit(report.passed ? 0 : 2);
    }

    private IEnumerator NormalWash()
    {
        yield return RunHold(WashAction.Shower, _owner.RuntimeGame.ServiceConfig.RinseDuration, "A 花洒打湿");
        yield return RunHold(WashAction.Shampoo, _owner.RuntimeGame.ServiceConfig.ShampooDuration, "A 洗发水起泡");
        yield return RunHold(WashAction.Shower, _owner.RuntimeGame.ServiceConfig.RinseDuration, "A 完全冲洗");
        Quick(ActiveServiceAction.WrapTowel, "A 包干净毛巾");
        yield return RecordFor(_status, .8f);
    }

    private IEnumerator DryShampooRecovery()
    {
        yield return RunHold(WashAction.Shampoo, _owner.RuntimeGame.ServiceConfig.ShampooDuration, "B 干发洗发水结块");
        yield return RunHold(WashAction.Shower, _owner.RuntimeGame.ServiceConfig.RinseDuration, "B 花洒补救打湿");
        yield return RunHold(WashAction.Shampoo, _owner.RuntimeGame.ServiceConfig.ShampooDuration, "B 重新揉洗起泡");
    }

    private IEnumerator FoamyTowelRecovery()
    {
        yield return RunHold(WashAction.Shower, _owner.RuntimeGame.ServiceConfig.RinseDuration, "C 花洒打湿");
        yield return RunHold(WashAction.Shampoo, _owner.RuntimeGame.ServiceConfig.ShampooDuration, "C 洗发水起泡");
        Quick(ActiveServiceAction.WrapTowel, "C 泡沫包毛巾");
        yield return RecordFor(_status, .8f);
        Quick(ActiveServiceAction.RemoveTowel, "C 拆污染毛巾");
        yield return RecordFor(_status, .8f);
        yield return RunHold(WashAction.Shower, _owner.RuntimeGame.ServiceConfig.RinseDuration, "C 冲洗补救");
    }

    private IEnumerator FoamyTransfer()
    {
        yield return RunHold(WashAction.Shower, _owner.RuntimeGame.ServiceConfig.RinseDuration, "D 花洒打湿");
        yield return RunHold(WashAction.Shampoo, _owner.RuntimeGame.ServiceConfig.ShampooDuration, "D 洗发水起泡");
        _status = "D 泡沫状态移动到剪发区";
        if (!_owner.RuntimeGame.Assign(_customer, 1))
            throw new InvalidOperationException("Haircut station transfer failed.");
        yield return RecordFor(_status, SalonGameModel.MovingToStationSeconds + .8f);
    }

    private IEnumerator RunHold(WashAction action, float duration, string status)
    {
        _status = status;
        if (!_owner.RuntimeGame.BeginWashAction(_customer, action))
            throw new InvalidOperationException("Begin action failed: " + action);
        float elapsed = 0f;
        while (_customer.ActiveServiceAction != ActiveServiceAction.None && elapsed < duration + .5f)
        {
            float step = Mathf.Min(.1f, duration - elapsed);
            if (step <= 0f) step = .1f;
            _owner.RuntimeGame.TickActiveServiceAction(_customer, step);
            elapsed += step;
            CaptureFrame();
            yield return new WaitForSecondsRealtime(.1f);
        }
        if (_customer.ActiveServiceAction != ActiveServiceAction.None)
            _owner.RuntimeGame.TickActiveServiceAction(_customer, duration + .1f);
        yield return RecordFor(status + " 完成", .6f);
    }

    private void Quick(ActiveServiceAction action, string status)
    {
        _status = status;
        if (_owner.RuntimeGame.PerformQuickAction(_customer, action)
            != ServiceActionResult.QuickActionCompleted)
            throw new InvalidOperationException("Quick action failed: " + action);
    }

    private IEnumerator RecordFor(string status, float seconds)
    {
        _status = status;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            CaptureFrame();
            yield return new WaitForSecondsRealtime(.1f);
            elapsed += .1f;
        }
    }

    private void CaptureFrame()
    {
        string path = Path.Combine(_frameDirectory, $"frame_{_frame++:D5}.png");
        Camera camera = Camera.main;
        if (camera == null) return;
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

    private bool ValidateScenario(CustomerPhysicalStateSnapshot physical)
    {
        switch (_options.Scenario)
        {
            case "A":
                return physical.IsTowelWrapped && physical.ShampooState == ShampooState.None
                    && physical.FoamAmount <= .001f && _customer.Step == 1;
            case "B":
                return physical.ShampooState == ShampooState.Normal && physical.FoamAmount > 0f
                    && _owner.RuntimeGame.GetServiceActionHistory(_customer).Count == 3;
            case "C":
                return !physical.IsTowelWrapped && physical.ShampooState == ShampooState.None
                    && physical.FoamAmount <= .001f;
            case "D":
                return _customer.Station == 1 && physical.ShampooState == ShampooState.Normal
                    && physical.FoamAmount > 0f;
            default:
                return false;
        }
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(_status)) return;
        GUI.Box(new Rect(25f, 25f, 520f, 62f), "阶段3录屏测试\n" + _status);
    }
}
