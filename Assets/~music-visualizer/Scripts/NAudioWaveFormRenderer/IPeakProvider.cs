using NAudio.Wave;

namespace NAudioWaveFormRenderer
{
    public interface IPeakProvider
    {
        void Init(ISampleProvider reader, int samplesPerPixel);
        PeakInfo GetNextPeak();
    }
}