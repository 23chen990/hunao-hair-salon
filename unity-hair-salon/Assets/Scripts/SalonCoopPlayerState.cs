using HairSalon;
using HairSalon.Character;
using UnityEngine;

/// <summary>
/// Runtime-only state for the second local salon player. Shared customers,
/// stations, day flow, and economy stay in SalonGameModel; this object holds
/// only P2's avatar, input surface, focus, and in-progress interaction.
/// </summary>
public sealed class SalonCoopPlayerState
{
    public readonly int PlayerId;
    public Transform Transform;
    public HairdresserCharacter Character;
    public SalonMobileControls Controls;
    public CustomerModel GuidedCustomer;
    public SalonCustomerView WorkingView;
    public ServiceType WorkingService;
    public SalonTool WorkingTool;
    public float WorkElapsed;
    public float WorkDuration;
    public bool HaircutSuspended;
    public string ActionLabel = "靠近顾客";
    public bool ActionAvailable;

    public SalonCoopPlayerState(int playerId)
    {
        PlayerId = playerId;
    }

    public bool IsWorking => WorkingView != null;

    public void ResetInteraction()
    {
        GuidedCustomer = null;
        WorkingView = null;
        WorkElapsed = 0f;
        WorkDuration = 0f;
        HaircutSuspended = false;
        ActionLabel = "靠近顾客";
        ActionAvailable = false;
    }
}
