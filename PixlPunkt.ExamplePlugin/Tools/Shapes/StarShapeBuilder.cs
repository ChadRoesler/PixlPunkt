using PixlPunkt.PluginSdk.Shapes;

namespace PixlPunkt.ExamplePlugin.Tools.Shapes
{
    /// <summary>
    /// A shape builder that creates star shapes with configurable points.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is an example implementation of <see cref="ShapeBuilderBase"/> that demonstrates:
    /// </para>
    /// <list type="bullet">
    /// <item>Creating custom geometric shapes</item>
    /// <item>Using the base class for automatic coordinate normalization</item>
    /// <item>Generating both outline and filled point sets</item>
    /// </list>
    /// </remarks>
    public sealed class StarShapeBuilder : ShapeBuilderBase
    {
        private readonly StarShapeSettings _settings;

        /// <summary>
        /// Creates a new star shape builder with the specified settings.
        /// </summary>
        /// <param name="settings">The star shape settings.</param>
        public StarShapeBuilder(StarShapeSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <inheritdoc/>
        public override string DisplayName => "Star";

        /// <inheritdoc/>
        protected override HashSet<(int x, int y)> BuildOutlinePointsCore(int x0, int y0, int width, int height)
        {
            var points = new HashSet<(int x, int y)>();

            if (width < 2 || height < 2) return points;

            // Calculate star vertices
            var vertices = CalculateStarVertices(x0, y0, width, height);

            // Draw lines between consecutive vertices
            for (int i = 0; i < vertices.Count; i++)
            {
                var p1 = vertices[i];
                var p2 = vertices[(i + 1) % vertices.Count];
                DrawLine(points, (int)p1.x, (int)p1.y, (int)p2.x, (int)p2.y);
            }

            return points;
        }

        /// <inheritdoc/>
        protected override HashSet<(int x, int y)> BuildFilledPointsCore(int x0, int y0, int width, int height)
        {
            var points = new HashSet<(int x, int y)>();

            if (width < 2 || height < 2) return points;

            // Calculate star vertices
            var vertices = CalculateStarVertices(x0, y0, width, height);

            // Fill the star using scanline algorithm
            FillPolygon(points, vertices, y0, y0 + height);

            return points;
        }

        /// <summary>
        /// Calculates the vertices of a star shape.
        /// </summary>
        private List<(double x, double y)> CalculateStarVertices(int x0, int y0, int width, int height)
        {
            var vertices = new List<(double x, double y)>();

            double centerX = x0 + width / 2.0;
            double centerY = y0 + height / 2.0;
            double outerRadius = Math.Min(width, height) / 2.0;
            double innerRadius = outerRadius * _settings.InnerRadiusRatio;

            int numPoints = _settings.PointCount;
            double angleStep = Math.PI / numPoints;
            double startAngle = -Math.PI / 2; // Start at top

            for (int i = 0; i < numPoints * 2; i++)
            {
                double angle = startAngle + i * angleStep;
                double radius = (i % 2 == 0) ? outerRadius : innerRadius;

                double x = centerX + Math.Cos(angle) * radius;
                double y = centerY + Math.Sin(angle) * radius;

                vertices.Add((x, y));
            }

            return vertices;
        }
    }
}
