public enum CustomerMoodStage
{
    Calm,
    Impatient,
    Angry,
    Leaving
}

public static class CustomerMoodModel
{
    public static CustomerMoodStage FromPatience(float fraction)
    {
        if (fraction < .12f) return CustomerMoodStage.Leaving;
        if (fraction < .30f) return CustomerMoodStage.Angry;
        if (fraction < .60f) return CustomerMoodStage.Impatient;
        return CustomerMoodStage.Calm;
    }

    public static float StormOutSpeedMultiplier(CustomerMoodStage stage)
    {
        return stage == CustomerMoodStage.Leaving ? 1.65f : 1f;
    }
}
