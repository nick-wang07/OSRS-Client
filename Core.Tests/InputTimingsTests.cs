using Core.Input;

namespace Core.Tests;

public class InputTimingsTests
{
    [Fact]
    public void PickDelay_RejectsNullRandom() =>
        Assert.Throws<ArgumentNullException>(() => InputTimings.PickDelay((1, 2), null!));

    [Fact]
    public void PickDelay_RejectsNegativeMinimum() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => InputTimings.PickDelay((-1, 5), new Random(1)));

    [Fact]
    public void PickDelay_ReturnsValueWithinRange()
    {
        var random = new Random(7);

        for (int i = 0; i < 200; i++)
        {
            Assert.InRange(InputTimings.PickDelay((10, 20), random), 10, 19);
        }
    }

    [Fact]
    public void PickDelay_HandlesDegenerateRangeWithoutThrowing()
    {
        // Random.Next(5, 5) is legal but always returns 5; Random.Next(5, 4) would throw.
        Assert.Equal(5, InputTimings.PickDelay((5, 5), new Random(1)));
    }

    [Fact]
    public void PickDelay_HandlesInvertedRangeByClampingToMinimum() =>
        Assert.Equal(9, InputTimings.PickDelay((9, 3), new Random(1)));

    [Fact]
    public void PickDelay_ReturnsZeroForZeroRange() =>
        Assert.Equal(0, InputTimings.PickDelay((0, 0), new Random(1)));

    [Fact]
    public void PickDelay_ProducesVariationAcrossAWideRange()
    {
        var random = new Random(3);

        HashSet<int> observed = [];

        for (int i = 0; i < 200; i++)
        {
            observed.Add(InputTimings.PickDelay((0, 100), random));
        }

        // Collapsing to a single value would remove all timing variation.
        Assert.True(observed.Count > 10, $"Only {observed.Count} distinct delays observed.");
    }

    [Fact]
    public void Default_UsesNonZeroHumanScaleDelays()
    {
        InputTimings timings = InputTimings.Default;

        Assert.True(timings.ClickHoldMs.Min > 0);
        Assert.True(timings.ClickHoldMs.Max > timings.ClickHoldMs.Min);
        Assert.True(timings.MoveStepDelayMs > 0);
        Assert.True(timings.MoveStepsPerDelay > 0);
    }

    [Fact]
    public void Instant_RemovesEveryDelay()
    {
        InputTimings timings = InputTimings.Instant;
        var random = new Random(1);

        Assert.Equal(0, InputTimings.PickDelay(timings.ClickHoldMs, random));
        Assert.Equal(0, InputTimings.PickDelay(timings.BeforeClickMs, random));
        Assert.Equal(0, InputTimings.PickDelay(timings.ModifierMs, random));
        Assert.Equal(0, InputTimings.PickDelay(timings.KeyPressMs, random));
        Assert.Equal(0, InputTimings.PickDelay(timings.ScrollNotchMs, random));
        Assert.Equal(0, InputTimings.PickDelay(timings.DragSettleMs, random));
        Assert.Equal(0, InputTimings.PickDelay(timings.KeystrokeMs, random));
        Assert.Equal(0, timings.MoveStepDelayMs);
    }
}

public class WindMouseOptionsTests
{
    [Fact]
    public void Default_UsesSaneTuning()
    {
        WindMouseOptions options = WindMouseOptions.Default;

        Assert.True(options.Gravity > 0);
        Assert.True(options.Wind > 0);
        Assert.True(options.MaxStep > 0);
        Assert.True(options.TargetArea > 0);
        Assert.True(options.MaxSteps > 0);
    }

    [Fact]
    public void Default_IsShared() => Assert.Same(WindMouseOptions.Default, WindMouseOptions.Default);

    [Fact]
    public void WithExpression_OverridesOnlyTheNamedValue()
    {
        WindMouseOptions options = WindMouseOptions.Default with { Gravity = 42 };

        Assert.Equal(42, options.Gravity);
        Assert.Equal(WindMouseOptions.Default.Wind, options.Wind);
    }
}
