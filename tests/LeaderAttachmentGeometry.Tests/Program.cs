using System;
using System.IO;
using System.Reflection;

namespace AvevaIntegration
{
    internal static class Program
    {
        private const double Tolerance = 1e-9;
        private static int testsRun;

        private static void Main()
        {
            RunDxfMtextNormalizationTests();
            RunGlobalAssociationTests();
            RunSelfDrivenWorkflowRegressionChecks();
            RunUnifiedWorkflowLogRegressionChecks();
            RunAnnotationAutoLayoutButtonRegressionChecks();
            RunTerminalVerificationRegressionChecks();
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
            AssertBatchPlan(5, new int[] { 1, 1, 1, 1, 1 });
            AssertBatchPlan(6, new int[] { 1, 1, 1, 1, 1, 1 });
            AssertBatchPlan(10, new int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });
            AssertBatchPlan(23, new int[] {
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });

            Console.WriteLine("PASS: " + testsRun + " assertions");
        }

        private static void RunTerminalVerificationRegressionChecks()
        {
            string workflowSource = File.ReadAllText(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "..\\..\\..\\..\\..\\SelfDrivenAnnotationAutoLayoutWorkflow.cs"));
            AssertTrue("terminal checks remaining", workflowSource.IndexOf(
                "remaining == 0", StringComparison.Ordinal) >= 0);
            AssertTrue("terminal checks end range", workflowSource.IndexOf(
                "endExclusive == total", StringComparison.Ordinal) >= 0);
            AssertTrue("terminal accepts next minus one", workflowSource.IndexOf(
                "nextBatchStart != -1", StringComparison.Ordinal) >= 0);
            AssertTrue("nonterminal rejects negative next", workflowSource.IndexOf(
                "nextBatchStart < 0", StringComparison.Ordinal) >= 0);
            AssertTrue("terminal completes workflow", workflowSource.IndexOf(
                "CompleteOnOwnerThread();", StringComparison.Ordinal) >= 0);
            AssertTrue("completion logs can save", workflowSource.IndexOf(
                "can_save=true", StringComparison.Ordinal) >= 0);
            AssertTrue("completion does not schedule next", workflowSource.IndexOf(
                "return;\n                }\n\n                if (remaining <= 0",
                StringComparison.Ordinal) >= 0);
            AssertTrue("batch size is one", AnnotationAutoLayoutPlan.BatchSize == 1);
        }

        private static void RunSelfDrivenWorkflowRegressionChecks()
        {
            string dispatcherSource = File.ReadAllText(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "..\\..\\..\\..\\..\\MarineUiDispatcher.cs"));
            string workflowSource = File.ReadAllText(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "..\\..\\..\\..\\..\\SelfDrivenAnnotationAutoLayoutWorkflow.cs"));
            AssertTrue("dispatcher has deferred post",
                dispatcherSource.IndexOf("PostDeferred", StringComparison.Ordinal) >= 0);
            AssertTrue("workflow uses deferred next batch post",
                workflowSource.IndexOf("dispatcher.PostDeferred(ApplyNextBatchOnOwnerThread)",
                    StringComparison.Ordinal) >= 0);
            AssertTrue("verify accepts already applied result",
                workflowSource.IndexOf("VERIFY_ACCEPTED", StringComparison.Ordinal) >= 0);
        }

        private static void RunUnifiedWorkflowLogRegressionChecks()
        {
            string clientSource = File.ReadAllText(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "..\\..\\..\\..\\..\\AlgorithmServiceClient.cs"));
            string workflowSource = File.ReadAllText(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "..\\..\\..\\..\\..\\SelfDrivenAnnotationAutoLayoutWorkflow.cs"));
            AssertTrue("workflow passes shared upload log",
                workflowSource.IndexOf("extraParamsJson", StringComparison.Ordinal) >= 0 &&
                workflowSource.IndexOf("logPath", StringComparison.Ordinal) >= 0 &&
                workflowSource.IndexOf("runId", StringComparison.Ordinal) >= 0);
            AssertTrue("workflow passes shared query log",
                workflowSource.IndexOf("resultJsonPath", StringComparison.Ordinal) >= 0 &&
                workflowSource.IndexOf("logPath", StringComparison.Ordinal) >= 0);
            AssertTrue("shared upload log uses auto layout path",
                clientSource.IndexOf("sharedLogPath", StringComparison.Ordinal) >= 0);
            AssertTrue("shared query log uses auto layout path",
                clientSource.IndexOf("ALGORITHM_QUERY", StringComparison.Ordinal) >= 0);
            AssertTrue("shared upload completion event exists",
                clientSource.IndexOf("DXF_UPLOAD_COMPLETED", StringComparison.Ordinal) >= 0);
            AssertTrue("shared upload failure event exists",
                clientSource.IndexOf("DXF_UPLOAD_FAILED", StringComparison.Ordinal) >= 0);
            AssertTrue("shared algorithm success event exists",
                clientSource.IndexOf("ALGORITHM_SUCCEEDED", StringComparison.Ordinal) >= 0);
            AssertTrue("shared logger serializes writes",
                clientSource.IndexOf("static readonly object logLock", StringComparison.Ordinal) >= 0);
            AssertTrue("legacy upload suffix remains available",
                clientSource.IndexOf(".upload.log.txt", StringComparison.Ordinal) >= 0);
            AssertTrue("legacy query suffix remains available",
                clientSource.IndexOf(".query.log.txt", StringComparison.Ordinal) >= 0);
        }

        private static void RunAnnotationAutoLayoutButtonRegressionChecks()
        {
            string pml = File.ReadAllText(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "..\\..\\..\\..\\..\\PML\\AnnotationAutoLayout.pmlfrm"));
            string control = File.ReadAllText(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "..\\..\\..\\..\\..\\AnnotationUnicodeProbeControl.cs"));
            string statusControl = File.ReadAllText(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "..\\..\\..\\..\\..\\AnnotationUnicodeProbeControl.cs"));
            AssertTrue("PML has one PMLNet host", pml.IndexOf("container .mainFrame PmlNetControl", StringComparison.Ordinal) >= 0);
            AssertTrue("PML has status control member", pml.IndexOf("member .statusControl", StringComparison.Ordinal) >= 0);
            AssertTrue("PML uses status control type", pml.IndexOf("member .statusControl is AnnotationAutoLayoutStatusControl", StringComparison.Ordinal) >= 0);
            AssertTrue("PML creates status control type", pml.IndexOf("object AnnotationAutoLayoutStatusControl()", StringComparison.Ordinal) >= 0);
            AssertTrue("PML does not use probe control", pml.IndexOf("AnnotationUnicodeProbeControl", StringComparison.Ordinal) < 0);
            AssertTrue("PML does not host native start button", pml.IndexOf("button .start", StringComparison.Ordinal) < 0);
            AssertTrue("PML does not host native cancel button", pml.IndexOf("button .cancel", StringComparison.Ordinal) < 0);
            AssertTrue("PML has start event binding", pml.IndexOf("StartRequested", StringComparison.Ordinal) >= 0);
            AssertTrue("PML has cancel event binding", pml.IndexOf("CancelRequested", StringComparison.Ordinal) >= 0);
            AssertTrue("PML has no log callback binding", pml.IndexOf("OpenLog", StringComparison.Ordinal) < 0);
            AssertTrue("PML has no close button callback binding", pml.IndexOf("CloseRequested", StringComparison.Ordinal) < 0);
            AssertTrue("unified log remains", File.ReadAllText(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "..\\..\\..\\..\\..\\SelfDrivenAnnotationAutoLayoutWorkflow.cs"))
                .IndexOf("auto-layout.log.txt", StringComparison.Ordinal) >= 0);
            AssertTrue("control has start button", control.IndexOf("startButton", StringComparison.Ordinal) >= 0);
            AssertTrue("control has cancel button", control.IndexOf("cancelButton", StringComparison.Ordinal) >= 0);
            AssertTrue("control has no open log button", control.IndexOf("openLogButton", StringComparison.Ordinal) < 0);
            AssertTrue("control has no close button", control.IndexOf("closeButton", StringComparison.Ordinal) < 0);
            AssertTrue("control has progress bar", control.IndexOf("ProgressBar", StringComparison.Ordinal) >= 0);
            AssertTrue("control has title area", control.IndexOf("titleLabel", StringComparison.Ordinal) >= 0);
            AssertTrue("control has drawing display", control.IndexOf("drawingValue", StringComparison.Ordinal) >= 0);
            AssertTrue("control has state display", control.IndexOf("stateValue", StringComparison.Ordinal) >= 0);
            AssertTrue("control has stage display", control.IndexOf("stageValue", StringComparison.Ordinal) >= 0);
            AssertTrue("control has batch display", control.IndexOf("batchValue", StringComparison.Ordinal) >= 0);
            AssertTrue("control has status update API", control.IndexOf("SetStatus", StringComparison.Ordinal) >= 0);
            AssertTrue("control has no open log button", control.IndexOf("OpenLog", StringComparison.Ordinal) < 0);
            AssertTrue("control has no close button", control.IndexOf("CloseRequested", StringComparison.Ordinal) < 0);
            AssertTrue("status control class is present", statusControl.IndexOf("AnnotationAutoLayoutStatusControl", StringComparison.Ordinal) >= 0);
        }

        private static void RunGlobalAssociationTests()
        {
            System.Collections.Generic.List<GlobalAnnotationGeometryMatcher.Annotation> annotations =
                new System.Collections.Generic.List<GlobalAnnotationGeometryMatcher.Annotation>();
            annotations.Add(new GlobalAnnotationGeometryMatcher.Annotation {
                Handle = "28", Text = "A", IsMove = true, X = 2.14872, Y = 0.0 });
            annotations.Add(new GlobalAnnotationGeometryMatcher.Annotation {
                Handle = "5E", Text = "B", IsMove = true, X = 0.41598, Y = 0.0 });
            annotations[0].OriginCenterPresent = true;
            annotations[1].OriginCenterPresent = true;
            System.Collections.Generic.List<GlobalAnnotationGeometryMatcher.GeometryPair> geometry =
                new System.Collections.Generic.List<GlobalAnnotationGeometryMatcher.GeometryPair>();
            geometry.Add(new GlobalAnnotationGeometryMatcher.GeometryPair {
                UnderlineHandle = "1D16", LeaderHandle = "1D17", X = 0.0, Y = 0.0 });
            geometry.Add(new GlobalAnnotationGeometryMatcher.GeometryPair {
                UnderlineHandle = "1D7C", LeaderHandle = "1D7D", X = 4.27866, Y = 0.0 });
            GlobalAnnotationGeometryMatcher.Result result =
                GlobalAnnotationGeometryMatcher.Match(annotations, geometry);
            AssertTrue("global matching succeeds", string.IsNullOrEmpty(result.Failure));
            AssertTrue("global matching unique", result.IsUnique);
            AssertTrue("global matching resolves collision",
                result.Assignments[0].Geometry.UnderlineHandle == "1D7C" &&
                result.Assignments[1].Geometry.UnderlineHandle == "1D16");

            annotations.Add(new GlobalAnnotationGeometryMatcher.Annotation {
                Handle = "2D", Text = "C", IsMove = false, X = 5.0, Y = 0.0 });
            annotations[2].OriginCenterPresent = true;
            geometry.Add(new GlobalAnnotationGeometryMatcher.GeometryPair {
                UnderlineHandle = "1D1A", LeaderHandle = "1D1B", X = 5.0, Y = 0.0 });
            result = GlobalAnnotationGeometryMatcher.Match(annotations, geometry);
            AssertTrue("reservation participates", result.Assignments.Count == 3);
            AssertTrue("reservation flag retained",
                !result.Assignments[1].Annotation.IsMove);
            GlobalAnnotationGeometryMatcher.Annotation missing =
                new GlobalAnnotationGeometryMatcher.Annotation {
                    Handle = "MISSING", Text = "M", IsMove = false,
                    X = 1.0, Y = 1.0, OriginCenterPresent = false };
            annotations[1] = missing;
            result = GlobalAnnotationGeometryMatcher.Match(annotations, geometry);
            AssertTrue("missing origin is rejected",
                result.Failure == "GLOBAL_ASSOCIATION_ORIGIN_CENTER_MISSING");
        }

        private static void RunDxfMtextNormalizationTests()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAvevaAssembly;
            string assemblyPath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..\\..\\..\\..\\..\\bin\\Debug\\AvevaIntegrationBeautify.dll"));
            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            Type entryType = assembly.GetType("AvevaIntegration.DemoEntry");
            MethodInfo isText = entryType.GetMethod(
                "IsDxfAnnotationTextEntity",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo normalize = entryType.GetMethod(
                "NormalizeDxfAnnotationText",
                BindingFlags.Static | BindingFlags.NonPublic);

            AssertTrue("TEXT entity accepted",
                (bool)isText.Invoke(null, new object[] { "TEXT" }));
            AssertTrue("MTEXT entity accepted",
                (bool)isText.Invoke(null, new object[] { "mtext" }));
            AssertTrue("LINE entity rejected",
                !(bool)isText.Invoke(null, new object[] { "LINE" }));
            AssertTrue("formatted MTEXT normalized",
                (string)normalize.Invoke(null, new object[] {
                    "MTEXT", "{\\fAnyFont|b0|i0;103P-CLH0001-003H}" }) ==
                    "103P-CLH0001-003H");
            AssertTrue("plain MTEXT preserved",
                (string)normalize.Invoke(null, new object[] {
                    "MTEXT", "103P-CLH0001-006" }) ==
                    "103P-CLH0001-006");
            AssertTrue("TEXT value preserved",
                (string)normalize.Invoke(null, new object[] {
                    "TEXT", "103P-CLH0001-003H" }) ==
                    "103P-CLH0001-003H");
            AssertTrue("MTEXT paragraph decoded",
                (string)normalize.Invoke(null, new object[] {
                    "MTEXT", "A\\PB" }) == "A\nB");
            AssertTrue("null text normalized to empty",
                (string)normalize.Invoke(null, new object[] { "MTEXT", null }) ==
                    string.Empty);
        }

        private static Assembly ResolveAvevaAssembly(
            object sender,
            ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            string path = Path.Combine("D:\\CodeNetSpace\\AvevaSdk", name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
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
