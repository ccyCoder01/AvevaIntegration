using System;

namespace AvevaIntegration
{
    internal enum TextRectangleEdge
    {
        Left = 0,
        Right = 1,
        Bottom = 2,
        Top = 3
    }

    internal struct GeometryPoint
    {
        public readonly double X;
        public readonly double Y;

        public GeometryPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    internal struct OrientedTextRectangle
    {
        public readonly GeometryPoint BottomLeft;
        public readonly GeometryPoint BottomRight;
        public readonly GeometryPoint TopRight;
        public readonly GeometryPoint TopLeft;

        public OrientedTextRectangle(
            GeometryPoint bottomLeft,
            GeometryPoint bottomRight,
            GeometryPoint topRight,
            GeometryPoint topLeft)
        {
            BottomLeft = bottomLeft;
            BottomRight = bottomRight;
            TopRight = topRight;
            TopLeft = topLeft;
        }
    }

    internal struct LeaderAttachmentResult
    {
        public readonly GeometryPoint Point;
        public readonly TextRectangleEdge Edge;
        public readonly double DistanceSquared;

        public LeaderAttachmentResult(
            GeometryPoint point,
            TextRectangleEdge edge,
            double distanceSquared)
        {
            Point = point;
            Edge = edge;
            DistanceSquared = distanceSquared;
        }
    }

    internal static class LeaderAttachmentGeometry
    {
        internal const double Epsilon = 1e-9;

        internal static GeometryPoint ClosestPointOnSegment(
            GeometryPoint point,
            GeometryPoint segmentStart,
            GeometryPoint segmentEnd)
        {
            double dx = segmentEnd.X - segmentStart.X;
            double dy = segmentEnd.Y - segmentStart.Y;
            double lengthSquared = dx * dx + dy * dy;
            if (lengthSquared <= Epsilon * Epsilon)
            {
                return segmentStart;
            }

            double t = ((point.X - segmentStart.X) * dx +
                (point.Y - segmentStart.Y) * dy) / lengthSquared;
            if (t < 0.0)
            {
                t = 0.0;
            }
            else if (t > 1.0)
            {
                t = 1.0;
            }
            return new GeometryPoint(
                segmentStart.X + t * dx,
                segmentStart.Y + t * dy);
        }

        internal static LeaderAttachmentResult
            CalculateNearestTextRectangleAttachment(
                GeometryPoint leaderStart,
                OrientedTextRectangle rectangle)
        {
            GeometryPoint[] starts = new GeometryPoint[]
            {
                rectangle.BottomLeft,
                rectangle.BottomRight,
                rectangle.BottomLeft,
                rectangle.TopLeft
            };
            GeometryPoint[] ends = new GeometryPoint[]
            {
                rectangle.TopLeft,
                rectangle.TopRight,
                rectangle.BottomRight,
                rectangle.TopRight
            };
            TextRectangleEdge[] edges = new TextRectangleEdge[]
            {
                TextRectangleEdge.Left,
                TextRectangleEdge.Right,
                TextRectangleEdge.Bottom,
                TextRectangleEdge.Top
            };

            GeometryPoint center = new GeometryPoint(
                (rectangle.BottomLeft.X + rectangle.BottomRight.X +
                    rectangle.TopRight.X + rectangle.TopLeft.X) / 4.0,
                (rectangle.BottomLeft.Y + rectangle.BottomRight.Y +
                    rectangle.TopRight.Y + rectangle.TopLeft.Y) / 4.0);
            double directionX = leaderStart.X - center.X;
            double directionY = leaderStart.Y - center.Y;

            LeaderAttachmentResult best = new LeaderAttachmentResult();
            double bestAlignment = double.NegativeInfinity;
            bool hasBest = false;
            int index = 0;
            while (index < edges.Length)
            {
                GeometryPoint candidate = ClosestPointOnSegment(
                    leaderStart,
                    starts[index],
                    ends[index]);
                double candidateX = candidate.X - leaderStart.X;
                double candidateY = candidate.Y - leaderStart.Y;
                double distanceSquared = candidateX * candidateX +
                    candidateY * candidateY;
                double midpointX = (starts[index].X + ends[index].X) / 2.0;
                double midpointY = (starts[index].Y + ends[index].Y) / 2.0;
                double alignment = (midpointX - center.X) * directionX +
                    (midpointY - center.Y) * directionY;

                if (!hasBest ||
                    distanceSquared < best.DistanceSquared - Epsilon ||
                    (Math.Abs(distanceSquared - best.DistanceSquared) <= Epsilon &&
                        alignment > bestAlignment + Epsilon))
                {
                    best = new LeaderAttachmentResult(
                        candidate,
                        edges[index],
                        distanceSquared);
                    bestAlignment = alignment;
                    hasBest = true;
                }
                index++;
            }
            return best;
        }

        internal static bool TryBuildFinalTextRectangleFromUnderline(
            GeometryPoint underlineStart,
            GeometryPoint underlineEnd,
            GeometryPoint originalTextCenter,
            double deltaX,
            double deltaY,
            out OrientedTextRectangle rectangle)
        {
            rectangle = new OrientedTextRectangle();
            GeometryPoint first = underlineStart;
            GeometryPoint second = underlineEnd;
            if (first.X > second.X + Epsilon ||
                (Math.Abs(first.X - second.X) <= Epsilon &&
                    first.Y > second.Y))
            {
                first = underlineEnd;
                second = underlineStart;
            }

            double baselineX = second.X - first.X;
            double baselineY = second.Y - first.Y;
            double length = Math.Sqrt(
                baselineX * baselineX + baselineY * baselineY);
            if (length <= Epsilon)
            {
                return false;
            }

            double normalX = -baselineY / length;
            double normalY = baselineX / length;
            double midpointX = (first.X + second.X) / 2.0;
            double midpointY = (first.Y + second.Y) / 2.0;
            double signedCenterDistance =
                (originalTextCenter.X - midpointX) * normalX +
                (originalTextCenter.Y - midpointY) * normalY;
            if (Math.Abs(signedCenterDistance) <= Epsilon)
            {
                return false;
            }
            if (signedCenterDistance < 0.0)
            {
                normalX = -normalX;
                normalY = -normalY;
                signedCenterDistance = -signedCenterDistance;
            }

            double height = signedCenterDistance * 2.0;
            GeometryPoint bottomLeft = Translate(first, deltaX, deltaY);
            GeometryPoint bottomRight = Translate(second, deltaX, deltaY);
            GeometryPoint topLeft = new GeometryPoint(
                bottomLeft.X + normalX * height,
                bottomLeft.Y + normalY * height);
            GeometryPoint topRight = new GeometryPoint(
                bottomRight.X + normalX * height,
                bottomRight.Y + normalY * height);
            rectangle = new OrientedTextRectangle(
                bottomLeft,
                bottomRight,
                topRight,
                topLeft);
            return true;
        }

        private static GeometryPoint Translate(
            GeometryPoint point,
            double deltaX,
            double deltaY)
        {
            return new GeometryPoint(point.X + deltaX, point.Y + deltaY);
        }
    }
}
