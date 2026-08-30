using HairSalon.ServiceArchitecture;
using HairSalon;
using UnityEngine;

/// <summary>
/// Routes action timing feedback into the current requirement icon. The reference UI has one
/// demand bubble; it must never create a second world-space disc behind the customer.
/// </summary>
public sealed class ActionProgressView : MonoBehaviour
{
    private OrderDemandBubbleView _orderBubble;
    private HaircutDemandBubbleView _haircutBubble;
    private bool _isInitialized;

    public void Initialize(Camera sceneCamera)
    {
        if (_isInitialized) return;
        _orderBubble = GetComponentInParent<OrderDemandBubbleView>(true);
        _haircutBubble = GetComponentInParent<HaircutDemandBubbleView>(true);
        ClearProgress();
        _isInitialized = true;
    }

    public bool SetProgressForService(
        ServiceType actionService, string symbol, float progress, Color color)
    {
        float value = Mathf.Clamp01(progress);
        bool boundToRequirement = _orderBubble != null &&
            _orderBubble.SetServiceProgress(actionService, value, color);
        if (_haircutBubble != null)
        {
            if (actionService == ServiceType.Cut)
            {
                _haircutBubble.SetHoldColor(color);
                _haircutBubble.SetHoldProgress(value);
                boundToRequirement = true;
            }
            else
            {
                _haircutBubble.ClearHoldProgress();
            }
        }
        return boundToRequirement;
    }

    public void ShowReady()
    {
        ArmTool();
    }

    public void ArmTool() => ClearProgress();

    public void ClearProgress()
    {
        if (_orderBubble != null) _orderBubble.ClearServiceProgress();
        if (_haircutBubble != null) _haircutBubble.ClearHoldProgress();
    }

    public void SetBackgroundWaitProgress(string label, float progress, Color color)
    {
        if (_orderBubble != null) _orderBubble.SetProcessingWaitProgress(label, progress, color);
    }

    public void ClearBackgroundWaitProgress()
    {
        if (_orderBubble != null) _orderBubble.ClearBackgroundWaitProgress();
    }

    public void SetServiceExecution(ServiceExecution execution)
    {
        if (execution == null)
        {
            ClearProgress();
            return;
        }
        ServiceType service = execution.Type == ServiceExecutionType.Wash
            ? ServiceType.Wash
            : execution.Type == ServiceExecutionType.BlowDry
                ? ServiceType.Dry
                : ServiceType.Cut;
        SetProgressForService(service, string.Empty, execution.Progress, SalonPalette.Warning);
    }
}
