using System;
using System.Collections.Generic;

namespace AvevaIntegration
{
    internal static class AnnotationAutoLayoutPlan
    {
        internal const int BatchSize = 5;

        internal static List<int> CreateBatchSizes(int moveCount)
        {
            if (moveCount < 0)
            {
                throw new ArgumentOutOfRangeException("moveCount");
            }
            List<int> result = new List<int>();
            int remaining = moveCount;
            while (remaining > 0)
            {
                int size = Math.Min(BatchSize, remaining);
                result.Add(size);
                remaining -= size;
            }
            return result;
        }
    }
}
