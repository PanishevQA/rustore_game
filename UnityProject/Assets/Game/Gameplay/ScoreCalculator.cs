using System;
using System.Collections.Generic;
using DontGetSidetracked.Core;

namespace DontGetSidetracked.Gameplay
{
    public readonly struct ScoreBreakdown
    {
        public readonly double Score;
        public readonly double MeanDistance;
        public readonly double Completion;
        public readonly double EndAccuracy;
        public readonly double GrossErrorRatio;

        public ScoreBreakdown(double score, double meanDistance, double completion, double endAccuracy, double grossErrorRatio)
        {
            Score = score;
            MeanDistance = meanDistance;
            Completion = completion;
            EndAccuracy = endAccuracy;
            GrossErrorRatio = grossErrorRatio;
        }
    }

    public sealed class ScoreCalculator
    {
        public ScoreBreakdown Calculate(RouteDefinition route, IReadOnlyList<RecordedPoint> userPoints)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (userPoints == null || userPoints.Count < 2) return new ScoreBreakdown(0, FixedPoint2.Scale, 0, 0, 1);

            double totalDistance = 0;
            int gross = 0;
            double maxProgress = 0;
            double grossThreshold = route.PathWidth * 3.0;

            for (int i = 0; i < userPoints.Count; i++)
            {
                var nearest = FindNearest(route.ReferencePoints, userPoints[i].Position);
                totalDistance += nearest.distance;
                if (nearest.distance > grossThreshold) gross++;
                if (nearest.progress > maxProgress) maxProgress = nearest.progress;
            }

            double mean = totalDistance / userPoints.Count;
            double completion = Clamp01(maxProgress / (route.ReferencePoints.Count - 1.0));
            var end = route.ReferencePoints[route.ReferencePoints.Count - 1];
            var last = userPoints[userPoints.Count - 1].Position;
            double endDistance = Math.Sqrt(last.DistanceSquared(end));
            double endAccuracy = Clamp01(1.0 - endDistance / (route.PathWidth * 2.0));
            double distanceScore = Clamp01(1.0 - mean / (route.PathWidth * 2.5));
            double grossRatio = gross / (double)userPoints.Count;
            double grossScore = 1.0 - grossRatio;

            double score = 100.0 * (0.70 * distanceScore + 0.18 * completion + 0.08 * endAccuracy + 0.04 * grossScore);
            if (completion < 0.5) score *= completion / 0.5;
            score = Math.Round(Clamp(score, 0, 100), 1, MidpointRounding.ToEven);
            return new ScoreBreakdown(score, mean, completion, endAccuracy, grossRatio);
        }

        private static (double distance, double progress) FindNearest(IReadOnlyList<FixedPoint2> reference, FixedPoint2 point)
        {
            double bestSquared = double.MaxValue;
            double bestProgress = 0;
            for (int i = 0; i < reference.Count - 1; i++)
            {
                var a = reference[i];
                var b = reference[i + 1];
                double abx = b.X - a.X;
                double aby = b.Y - a.Y;
                double apx = point.X - a.X;
                double apy = point.Y - a.Y;
                double lenSq = abx * abx + aby * aby;
                double t = lenSq <= 0 ? 0 : Clamp01((apx * abx + apy * aby) / lenSq);
                double qx = a.X + abx * t;
                double qy = a.Y + aby * t;
                double dx = point.X - qx;
                double dy = point.Y - qy;
                double distSq = dx * dx + dy * dy;
                if (distSq < bestSquared)
                {
                    bestSquared = distSq;
                    bestProgress = i + t;
                }
            }
            return (Math.Sqrt(bestSquared), bestProgress);
        }

        private static double Clamp01(double value) => Clamp(value, 0, 1);
        private static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
    }
}
