namespace Core.Input;

/// <summary>
/// Tuning for the WindMouse path generator. These are implementation details of one
/// movement model, so they live here rather than on <see cref="IInputProvider"/>.
/// </summary>
public sealed record WindMouseOptions
{
    public static WindMouseOptions Default { get; } = new();

    /// <summary>Pull toward the destination. Higher tracks straighter.</summary>
    public double Gravity { get; init; } = 9;

    /// <summary>Magnitude of random deflection. Higher wanders more.</summary>
    public double Wind { get; init; } = 3;

    /// <summary>Maximum pixels traveled per step.</summary>
    public double MaxStep { get; init; } = 30;

    /// <summary>Distance at which the path stops wandering and settles onto the target.</summary>
    public double TargetArea { get; init; } = 12;

    /// <summary>
    /// Safety valve. The generator is stochastic, so a pathological parameter set could
    /// otherwise loop forever rather than converging.
    /// </summary>
    public int MaxSteps { get; init; } = 10_000;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(Gravity);
        ArgumentOutOfRangeException.ThrowIfNegative(Wind);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxStep);
        ArgumentOutOfRangeException.ThrowIfNegative(TargetArea);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxSteps);
    }
}
