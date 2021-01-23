using System.Diagnostics;

namespace MVCore.Engine
{
    public class MStopwatch : Stopwatch
    {
        private double _frequencyScaler;

        public MStopwatch()
        {
            _frequencyScaler = 1.0 / Frequency;
        }

        public double ElapsedSeconds
        {
            get
            {
                return ElapsedTicks * _frequencyScaler;
            }
        }

    }
}
