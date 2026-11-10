using System;

namespace Ibit.Core.Oximetry
{
    /// <summary>
    /// Demonstration only: RMS AC/DC ratio of ratios followed by the
    /// Maxim/SparkFun example polynomial. NOT a PITACO-specific calibration;
    /// output must never drive clinical warnings or DeepDDA decisions.
    /// </summary>
    public static class Spo2Estimator
    {
        public static bool TryEstimateDemo(PpgProcessedWindow input,
            out double spo2DemoPercent, out double ratio)
        {
            spo2DemoPercent = 0.0;
            ratio = 0.0;
            if (input == null || input.RedDc <= 0.0 || input.IrDc <= 0.0 ||
                input.RedAcRms <= 1e-9 || input.IrAcRms <= 1e-9)
                return false;

            ratio = (input.RedAcRms / input.RedDc) /
                    (input.IrAcRms / input.IrDc);
            if (double.IsNaN(ratio) || double.IsInfinity(ratio) ||
                ratio < 0.2 || ratio > 1.4)
                return false;

            // Example curve also used in pitaco_bridge/experimental.py.
            double estimate = -45.060 * ratio * ratio + 30.354 * ratio + 94.845;
            if (double.IsNaN(estimate) || double.IsInfinity(estimate) ||
                estimate < 70.0 || estimate > 100.0)
                return false;

            spo2DemoPercent = Math.Round(estimate, 1);
            return true;
        }
    }
}
