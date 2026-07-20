using System;
using System.Collections.Generic;

namespace AvevaIntegration
{
    internal sealed class GlobalAnnotationGeometryMatcher
    {
        internal sealed class Annotation
        {
            internal string Handle;
            internal string Text;
            internal bool IsMove;
            internal bool OriginCenterPresent;
            internal double X;
            internal double Y;
        }

        internal sealed class GeometryPair
        {
            internal string UnderlineHandle;
            internal string LeaderHandle;
            internal double X;
            internal double Y;
        }

        internal sealed class Assignment
        {
            internal Annotation Annotation;
            internal GeometryPair Geometry;
            internal double Distance;
        }

        internal sealed class Result
        {
            internal readonly List<Assignment> Assignments =
                new List<Assignment>();
            internal double TotalCost;
            internal bool IsUnique;
            internal string Failure;
        }

        internal static Result Match(
            IList<Annotation> annotations,
            IList<GeometryPair> geometries)
        {
            Result result = new Result();
            if (annotations == null || geometries == null ||
                annotations.Count != geometries.Count ||
                annotations.Count == 0)
            {
                result.Failure = "global annotation/geometry count mismatch";
                return result;
            }

            List<Annotation> left = new List<Annotation>(annotations);
            List<GeometryPair> right = new List<GeometryPair>(geometries);
            left.Sort(CompareAnnotation);
            right.Sort(CompareGeometry);
            int n = left.Count;
            double[,] costs = new double[n, n];
            int row = 0;
            while (row < n)
            {
                if (!left[row].OriginCenterPresent)
                {
                    result.Failure = "GLOBAL_ASSOCIATION_ORIGIN_CENTER_MISSING";
                    return result;
                }
                int col = 0;
                while (col < n)
                {
                    double dx = left[row].X - right[col].X;
                    double dy = left[row].Y - right[col].Y;
                    double cost = Math.Sqrt(dx * dx + dy * dy);
                    if (double.IsNaN(cost) || double.IsInfinity(cost) || cost < 0.0)
                    {
                        result.Failure = "invalid global association cost";
                        return result;
                    }
                    costs[row, col] = cost;
                    col++;
                }
                row++;
            }

            int[] assignment;
            result.TotalCost = Hungarian(costs, out assignment);
            if (double.IsNaN(result.TotalCost) || double.IsInfinity(result.TotalCost))
            {
                result.Failure = "global association total cost is invalid";
                return result;
            }
            HashSet<string> underlines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> leaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            row = 0;
            while (row < n)
            {
                GeometryPair pair = right[assignment[row]];
                if (!underlines.Add(pair.UnderlineHandle) ||
                    !leaders.Add(pair.LeaderHandle))
                {
                    result.Failure = "duplicate global geometry assignment";
                    result.Assignments.Clear();
                    return result;
                }
                result.Assignments.Add(new Assignment {
                    Annotation = left[row],
                    Geometry = pair,
                    Distance = costs[row, assignment[row]]
                });
                row++;
            }
            result.IsUnique = true;
            return result;
        }

        private static double Hungarian(double[,] input, out int[] assignment)
        {
            int n = input.GetLength(0);
            double[] u = new double[n + 1];
            double[] v = new double[n + 1];
            int[] p = new int[n + 1];
            int[] way = new int[n + 1];
            int i = 1;
            while (i <= n)
            {
                p[0] = i;
                int j0 = 0;
                double[] minv = new double[n + 1];
                bool[] used = new bool[n + 1];
                int j = 1;
                while (j <= n) { minv[j] = double.MaxValue; j++; }
                do
                {
                    used[j0] = true;
                    int i0 = p[j0];
                    double delta = double.MaxValue;
                    int j1 = 0;
                    j = 1;
                    while (j <= n)
                    {
                        if (!used[j])
                        {
                            double cur = input[i0 - 1, j - 1] - u[i0] - v[j];
                            if (cur < minv[j]) { minv[j] = cur; way[j] = j0; }
                            if (minv[j] < delta) { delta = minv[j]; j1 = j; }
                        }
                        j++;
                    }
                    j = 0;
                    while (j <= n)
                    {
                        if (used[j]) { u[p[j]] += delta; v[j] -= delta; }
                        else { minv[j] -= delta; }
                        j++;
                    }
                    j0 = j1;
                } while (p[j0] != 0);
                do
                {
                    int j1 = way[j0];
                    p[j0] = p[j1];
                    j0 = j1;
                } while (j0 != 0);
                i++;
            }
            assignment = new int[n];
            int col = 1;
            while (col <= n) { assignment[p[col] - 1] = col - 1; col++; }
            return -v[0];
        }

        private static int CompareAnnotation(Annotation a, Annotation b)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(a.Handle, b.Handle);
        }

        private static int CompareGeometry(GeometryPair a, GeometryPair b)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(
                a.UnderlineHandle, b.UnderlineHandle);
        }
    }
}
