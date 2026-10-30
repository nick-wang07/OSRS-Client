using Core.Input;
using System.Drawing;

namespace Core.Tests;

public class WindMousePathTests
{
    private static readonly WindMouseOptions Options = WindMouseOptions.Default;

    private static Random Seeded(int seed = 1234) => new(seed);

    [Fact]
    public void Generate_RejectsNullOptions() =>
        Assert.Throws<ArgumentNullException>(
            () => WindMousePath.Generate(Point.Empty, new Point(10, 10), null!, Seeded()));

    [Fact]
    public void Generate_RejectsNullRandom() =>
        Assert.Throws<ArgumentNullException>(
            () => WindMousePath.Generate(Point.Empty, new Point(10, 10), Options, null!));

    [Fact]
    public void Generate_AlwaysFinishesExactlyOnTheDestination()
    {
        var destination = new Point(640, 480);

        IReadOnlyList<Point> path = WindMousePath.Generate(Point.Empty, destination, Options, Seeded());

        Assert.Equal(destination, path[^1]);
    }

    [Fact]
    public void Generate_ProducesDestinationOnlyWhenAlreadyThere()
    {
        var destination = new Point(50, 50);

        IReadOnlyList<Point> path = WindMousePath.Generate(destination, destination, Options, Seeded());

        Assert.Single(path);
        Assert.Equal(destination, path[0]);
    }

    [Fact]
    public void Generate_ProducesAnIntermediatePathForDistantTargets()
    {
        IReadOnlyList<Point> path = WindMousePath.Generate(
            Point.Empty, new Point(800, 600), Options, Seeded());

        // A straight jump would be a single point; a human-like path has many.
        Assert.True(path.Count > 10, $"Expected a multi-step path, got {path.Count} points.");
    }

    [Fact]
    public void Generate_NeverRepeatsConsecutivePoints()
    {
        IReadOnlyList<Point> path = WindMousePath.Generate(
            Point.Empty, new Point(400, 300), Options, Seeded());

        for (int i = 1; i < path.Count; i++)
        {
            Assert.NotEqual(path[i - 1], path[i]);
        }
    }

    [Fact]
    public void Generate_KeepsStepsWithinMaxStepUntilTheFinalSnap()
    {
        WindMouseOptions options = Options with { MaxStep = 15 };

        IReadOnlyList<Point> path = WindMousePath.Generate(
            Point.Empty, new Point(500, 500), options, Seeded());

        // The last point is appended verbatim to land exactly on target, so it is exempt.
        for (int i = 1; i < path.Count - 1; i++)
        {
            double step = Distance(path[i - 1], path[i]);

            Assert.True(step <= options.MaxStep + 1, $"Step {i} traveled {step:F2}px.");
        }
    }

    [Fact]
    public void Generate_IsDeterministicForAGivenSeed()
    {
        IReadOnlyList<Point> first = WindMousePath.Generate(
            Point.Empty, new Point(300, 200), Options, Seeded(99));
        IReadOnlyList<Point> second = WindMousePath.Generate(
            Point.Empty, new Point(300, 200), Options, Seeded(99));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Generate_ProducesDifferentPathsForDifferentSeeds()
    {
        IReadOnlyList<Point> first = WindMousePath.Generate(
            Point.Empty, new Point(300, 200), Options, Seeded(1));
        IReadOnlyList<Point> second = WindMousePath.Generate(
            Point.Empty, new Point(300, 200), Options, Seeded(2));

        // Randomized wind is the whole point: identical paths would be a bot fingerprint.
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Generate_IsNotAStraightLine()
    {
        Point start = Point.Empty;
        var destination = new Point(500, 0);

        IReadOnlyList<Point> path = WindMousePath.Generate(start, destination, Options, Seeded(7));

        // A perfectly horizontal path would mean no wind was applied at all.
        Assert.Contains(path, point => point.Y != 0);
    }

    [Fact]
    public void Generate_HonorsTheStepCeiling()
    {
        // Zero gravity removes the pull toward the target, so the walk cannot converge
        // and must be stopped by the safety valve rather than looping forever.
        WindMouseOptions options = Options with { Gravity = 0, Wind = 10, MaxSteps = 50 };

        IReadOnlyList<Point> path = WindMousePath.Generate(
            Point.Empty, new Point(5000, 5000), options, Seeded());

        Assert.True(path.Count <= options.MaxSteps + 1, $"Path grew to {path.Count} points.");
        Assert.Equal(new Point(5000, 5000), path[^1]);
    }

    [Fact]
    public void Generate_HandlesNegativeCoordinates()
    {
        var destination = new Point(-400, -300);

        IReadOnlyList<Point> path = WindMousePath.Generate(
            new Point(100, 100), destination, Options, Seeded());

        Assert.Equal(destination, path[^1]);
    }

    [Theory]
    [InlineData(-1, 3, 30, 12)]
    [InlineData(9, -1, 30, 12)]
    [InlineData(9, 3, 0, 12)]
    [InlineData(9, 3, 30, -1)]
    public void Generate_RejectsInvalidOptions(double gravity, double wind, double maxStep, double targetArea)
    {
        WindMouseOptions options = Options with
        {
            Gravity = gravity,
            Wind = wind,
            MaxStep = maxStep,
            TargetArea = targetArea,
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => WindMousePath.Generate(Point.Empty, new Point(10, 10), options, Seeded()));
    }

    [Fact]
    public void Generate_RejectsNonPositiveMaxSteps()
    {
        WindMouseOptions options = Options with { MaxSteps = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => WindMousePath.Generate(Point.Empty, new Point(10, 10), options, Seeded()));
    }

    /// <summary>
    /// Pins the exact trajectory for a fixed seed.
    /// </summary>
    /// <remarks>
    /// WindMouse is a fixed, published algorithm, so its output for a given seed is a
    /// stable contract. Property assertions alone cannot catch a flipped sign or a
    /// swapped operand — many such changes still yield a path that merely "ends on
    /// target and curves". Changing the algorithm deliberately means updating this list.
    /// </remarks>
    [Fact]
    public void Generate_ProducesTheExpectedTrajectoryForAKnownSeed()
    {
        Point[] expected =
        [
            new(18, 15), new(34, 25), new(57, 41), new(72, 50), new(93, 66),
            new(114, 81), new(132, 94), new(148, 104), new(172, 117), new(196, 129),
            new(222, 138), new(239, 143), new(247, 145), new(245, 144), new(235, 141),
            new(216, 134), new(204, 128), new(200, 126), new(202, 126), new(204, 127),
            new(205, 128), new(207, 129), new(208, 129), new(213, 131), new(210, 130),
        ];

        IReadOnlyList<Point> path = WindMousePath.Generate(
            new Point(10, 10), new Point(210, 130), Options, new Random(20260718));

        Assert.Equal(expected, path);
    }

    [Fact]
    public void Generate_OvershootsAndSettlesRatherThanStoppingDead()
    {
        // Gravity carries the cursor past the target before the target-area damping
        // pulls it back, which is what makes the motion look human.
        IReadOnlyList<Point> path = WindMousePath.Generate(
            new Point(10, 10), new Point(210, 130), Options, new Random(20260718));

        Assert.Contains(path, point => point.X > 210);
    }

    [Fact]
    public void Generate_StepCountScalesWithDistance()
    {
        IReadOnlyList<Point> near = WindMousePath.Generate(
            Point.Empty, new Point(60, 60), Options, Seeded(5));
        IReadOnlyList<Point> far = WindMousePath.Generate(
            Point.Empty, new Point(1200, 900), Options, Seeded(5));

        Assert.True(far.Count > near.Count, $"far={far.Count}, near={near.Count}");
    }

    [Fact]
    public void Generate_StrongerGravityTracksMoreDirectly()
    {
        var destination = new Point(600, 0);

        IReadOnlyList<Point> loose = WindMousePath.Generate(
            Point.Empty, destination, Options with { Gravity = 2, Wind = 6 }, Seeded(11));
        IReadOnlyList<Point> tight = WindMousePath.Generate(
            Point.Empty, destination, Options with { Gravity = 40, Wind = 6 }, Seeded(11));

        Assert.True(
            MaxDeviation(tight) < MaxDeviation(loose),
            $"tight={MaxDeviation(tight):F1}, loose={MaxDeviation(loose):F1}");
    }

    [Fact]
    public void Generate_MoreWindDeviatesFurther()
    {
        var destination = new Point(600, 0);

        IReadOnlyList<Point> calm = WindMousePath.Generate(
            Point.Empty, destination, Options with { Wind = 1 }, Seeded(21));
        IReadOnlyList<Point> gusty = WindMousePath.Generate(
            Point.Empty, destination, Options with { Wind = 15 }, Seeded(21));

        Assert.True(
            MaxDeviation(gusty) > MaxDeviation(calm),
            $"gusty={MaxDeviation(gusty):F1}, calm={MaxDeviation(calm):F1}");
    }

    [Fact]
    public void Generate_MaxStepBoundsIndividualStepLength()
    {
        // Step count is deliberately not compared: a larger MaxStep overshoots the
        // target area and oscillates before settling, so the relationship between
        // MaxStep and step count is not monotonic. What MaxStep does guarantee is a
        // ceiling on any single step.
        WindMouseOptions options = Options with { MaxStep = 8 };

        IReadOnlyList<Point> path = WindMousePath.Generate(
            Point.Empty, new Point(500, 400), options, Seeded(33));

        for (int i = 1; i < path.Count - 1; i++)
        {
            Assert.True(Distance(path[i - 1], path[i]) <= options.MaxStep + 1);
        }
    }

    /// <summary>Largest perpendicular departure from the straight line along y = 0.</summary>
    private static double MaxDeviation(IReadOnlyList<Point> path) =>
        path.Max(point => Math.Abs((double)point.Y));

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;

        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
