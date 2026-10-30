using System.Drawing;

namespace Core.Input;

/// <summary>
/// Generates human-looking cursor paths using the WindMouse algorithm: a point pulled
/// toward the target by "gravity" while a decaying random "wind" deflects it, producing
/// curved, variable-speed motion instead of a straight constant-velocity line.
/// </summary>
/// <remarks>
/// Pure and deterministic for a given <see cref="Random"/> seed, so the movement model
/// can be tested without touching the desktop.
/// </remarks>
public static class WindMousePath
{
    private static readonly double SqrtThree = Math.Sqrt(3);
    private static readonly double SqrtFive = Math.Sqrt(5);

    /// <summary>
    /// Builds the sequence of points from <paramref name="start"/> to <paramref name="destination"/>.
    /// The destination is always the final point, so a path never stops short of its target.
    /// </summary>
    public static IReadOnlyList<Point> Generate(
        Point start,
        Point destination,
        WindMouseOptions options,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(random);
        options.Validate();

        List<Point> path = [];

        double currentX = start.X;
        double currentY = start.Y;
        double velocityX = 0;
        double velocityY = 0;
        double windX = 0;
        double windY = 0;
        double maxStep = options.MaxStep;

        double distance = Distance(currentX, currentY, destination);

        while (distance >= 1 && path.Count < options.MaxSteps)
        {
            double windMagnitude = Math.Min(options.Wind, distance);

            if (distance >= options.TargetArea)
            {
                windX = (windX / SqrtThree) + (((2 * random.NextDouble()) - 1) * windMagnitude / SqrtFive);
                windY = (windY / SqrtThree) + (((2 * random.NextDouble()) - 1) * windMagnitude / SqrtFive);
            }
            else
            {
                // Inside the target area the wind decays and the steps shrink, so the
                // cursor eases onto the target rather than snapping to it.
                windX /= SqrtThree;
                windY /= SqrtThree;

                maxStep = maxStep < 3
                    ? (3 * random.NextDouble()) + 3
                    : maxStep / SqrtFive;
            }

            velocityX += windX + (options.Gravity * (destination.X - currentX) / distance);
            velocityY += windY + (options.Gravity * (destination.Y - currentY) / distance);

            double velocityMagnitude = Math.Sqrt((velocityX * velocityX) + (velocityY * velocityY));

            if (velocityMagnitude > maxStep)
            {
                double clip = (maxStep / 2) + (random.NextDouble() * maxStep / 2);
                velocityX = velocityX / velocityMagnitude * clip;
                velocityY = velocityY / velocityMagnitude * clip;
            }

            currentX += velocityX;
            currentY += velocityY;

            var next = new Point((int)Math.Round(currentX), (int)Math.Round(currentY));

            if (path.Count == 0 || path[^1] != next)
            {
                path.Add(next);
            }

            distance = Distance(currentX, currentY, destination);
        }

        if (path.Count == 0 || path[^1] != destination)
        {
            path.Add(destination);
        }

        return path;
    }

    private static double Distance(double x, double y, Point target)
    {
        double dx = target.X - x;
        double dy = target.Y - y;

        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
