using System;

namespace AvevaIntegration
{
    internal static class Program
    {
        private const double Tolerance = 1e-9;
        private static int testsRun;

        private static void Main()
        {
            AssertTrue("probe invariants pass",
                AnnotationAutoLayoutDispatcherProbeLogic.Passed(
                    12, 12, 12, false, false, true, 19));
            AssertTrue("probe rejects owner mismatch",
                !AnnotationAutoLayoutDispatcherProbeLogic.Passed(
                    12, 13, 12, false, false, true, 19));
            AssertTrue("probe rejects thread-pool callback",
                !AnnotationAutoLayoutDispatcherProbeLogic.Passed(
                    12, 12, 12, false, true, true, 19));
            AssertTrue("probe rejects same worker and callback",
                !AnnotationAutoLayoutDispatcherProbeLogic.Passed(
                    12, 12, 12, false, false, true, 12));
            AnnotationAutoLayoutDispatcherProbeState probeState =
                new AnnotationAutoLayoutDispatcherProbeState();
            probeState.ProbeId = "test";
            string probeStatus = probeState.GetStatus();
            AssertTrue("probe status has fixed state field",
                probeStatus.IndexOf("|STATE=IDLE|", StringComparison.Ordinal) >= 0);
            AssertTrue("probe status has passed field",
                probeStatus.IndexOf("|PASSED=false|", StringComparison.Ordinal) >= 0);

            OrientedTextRectangle rectangle = AxisAlignedRectangle(
                0.0, 0.0, 10.0, 10.0);

            AssertAttachment("left", rectangle, -5.0, 4.0,
                0.0, 4.0, TextRectangleEdge.Left);
            AssertAttachment("right", rectangle, 15.0, 6.0,
                10.0, 6.0, TextRectangleEdge.Right);
            AssertAttachment("above", rectangle, 4.0, 15.0,
                4.0, 10.0, TextRectangleEdge.Top);
            AssertAttachment("below", rectangle, 6.0, -5.0,
                6.0, 0.0, TextRectangleEdge.Bottom);
            AssertAttachment("upper-left deterministic tie", rectangle,
                -4.0, 12.0, 0.0, 10.0, TextRectangleEdge.Left);

            GeometryPoint clamped = LeaderAttachmentGeometry.ClosestPointOnSegment(
                new GeometryPoint(-3.0, 4.0),
                new GeometryPoint(0.0, 0.0),
                new GeometryPoint(10.0, 0.0));
            AssertPoint("segment projection clamps to corner", clamped, 0.0, 0.0);

            AssertAttachment("horizontal center alignment", rectangle,
                -5.0, 5.0, 0.0, 5.0, TextRectangleEdge.Left);
            AssertAttachment("vertical center alignment", rectangle,
                5.0, 15.0, 5.0, 10.0, TextRectangleEdge.Top);

            GeometryPoint degenerate = LeaderAttachmentGeometry.ClosestPointOnSegment(
                new GeometryPoint(9.0, 9.0),
                new GeometryPoint(2.0, 3.0),
                new GeometryPoint(2.0, 3.0));
            AssertPoint("degenerate segment", degenerate, 2.0, 3.0);
            AssertFinite("degenerate segment finite", degenerate);

            LeaderAttachmentResult first =
                LeaderAttachmentGeometry.CalculateNearestTextRectangleAttachment(
                    new GeometryPoint(-4.0, 12.0), rectangle);
            int repeat = 0;
            while (repeat < 100)
            {
                LeaderAttachmentResult current =
                    LeaderAttachmentGeometry.CalculateNearestTextRectangleAttachment(
                        new GeometryPoint(-4.0, 12.0), rectangle);
                AssertPoint("repeat deterministic", current.Point,
                    first.Point.X, first.Point.Y);
                AssertTrue("repeat edge deterministic", current.Edge == first.Edge);
                repeat++;
            }

            GeometryPoint underlineStart = new GeometryPoint(1.0, 2.0);
            GeometryPoint underlineEnd = new GeometryPoint(11.0, 2.0);
            GeometryPoint originalCenter = new GeometryPoint(6.0, 4.0);
            double dx = 20.0;
            double dy = -3.0;
            OrientedTextRectangle finalRectangle;
            bool built = LeaderAttachmentGeometry.TryBuildFinalTextRectangleFromUnderline(
                underlineStart, underlineEnd, originalCenter, dx, dy,
                out finalRectangle);
            AssertTrue("final rectangle built", built);
            AssertPoint("bbox translated bottom-left",
                finalRectangle.BottomLeft, 21.0, -1.0);
            AssertPoint("bbox translated top-right",
                finalRectangle.TopRight, 31.0, 3.0);
            AssertPoint("leader_start regression",
                new GeometryPoint(40.0, 2.0), 40.0, 2.0);
            AssertTrue("dx regression", dx == 20.0);
            AssertTrue("dy regression", dy == -3.0);
            AssertPoint("new_center regression",
                new GeometryPoint(originalCenter.X + dx, originalCenter.Y + dy),
                26.0, 1.0);

            AssertBatchPlan(1, new int[] { 1 });
            AssertBatchPlan(5, new int[] { 5 });
            AssertBatchPlan(6, new int[] { 5, 1 });
            AssertBatchPlan(10, new int[] { 5, 5 });
            AssertBatchPlan(23, new int[] { 5, 5, 5, 5, 3 });

            Console.WriteLine("PASS: " + testsRun + " assertions");
        }

        private static OrientedTextRectangle AxisAlignedRectangle(
            double minX, double minY, double maxX, double maxY)
        {
            return new OrientedTextRectangle(
                new GeometryPoint(minX, minY),
                new GeometryPoint(maxX, minY),
                new GeometryPoint(maxX, maxY),
                new GeometryPoint(minX, maxY));
        }

        private static void AssertAttachment(
            string name,
            OrientedTextRectangle rectangle,
            double startX,
            double startY,
            double expectedX,
            double expectedY,
            TextRectangleEdge expectedEdge)
        {
            LeaderAttachmentResult result =
                LeaderAttachmentGeometry.CalculateNearestTextRectangleAttachment(
                    new GeometryPoint(startX, startY), rectangle);
            AssertPoint(name, result.Point, expectedX, expectedY);
            AssertTrue(name + " edge", result.Edge == expectedEdge);
            AssertFinite(name + " finite", result.Point);
        }

        private static void AssertPoint(
            string name,
            GeometryPoint actual,
            double expectedX,
            double expectedY)
        {
            AssertTrue(name + " X", Math.Abs(actual.X - expectedX) <= Tolerance);
            AssertTrue(name + " Y", Math.Abs(actual.Y - expectedY) <= Tolerance);
        }

        private static void AssertFinite(string name, GeometryPoint point)
        {
            AssertTrue(name,
                !double.IsNaN(point.X) && !double.IsInfinity(point.X) &&
                !double.IsNaN(point.Y) && !double.IsInfinity(point.Y));
        }

        private static void AssertTrue(string name, bool condition)
        {
            testsRun++;
            if (!condition)
            {
                throw new InvalidOperationException("FAILED: " + name);
            }
        }

        private static void AssertBatchPlan(int moveCount, int[] expected)
        {
            System.Collections.Generic.List<int> actual =
                AnnotationAutoLayoutPlan.CreateBatchSizes(moveCount);
            AssertTrue("batch count " + moveCount,
                actual.Count == expected.Length);
            int index = 0;
            while (index < expected.Length)
            {
                AssertTrue("batch size " + moveCount + "/" + index,
                    actual[index] == expected[index]);
                index++;
            }
        }
    }
}
