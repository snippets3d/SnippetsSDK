using System;

namespace Snippets.Sdk
{

    /// <summary>
    /// Implements a progress reporter that reports a progress in a custom range, instead of the default [0, 1].
    /// This class converts a reported progress in the range [0, 1] to a progress in the range [min, max]
    /// </summary>
    public class ProgressWithCustomFloatRange : Progress<float>
    {
        /// <summary>
        /// Minimum range value
        /// </summary>
        public float Min { get; set; }

        /// <summary>
        /// Maximum range value
        /// </summary>
        public float Max { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="min">Lower limit of the custom range</param>
        /// <param name="max">Higher limit of the custom range</param>
        public ProgressWithCustomFloatRange(float min, float max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>
        /// Reports a progress in the range [0, 1]. The class will convert it to the appropriate range
        /// </summary>
        /// <param name="value">Progress value in the range [0, 1]</param>
        protected override void OnReport(float value)
        {
            value = (Min + (Max - Min) * value);
            base.OnReport(value);
        }

    }
}
