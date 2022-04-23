using System;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.WaveFormRenderer;
using NaughtyAttributes;
using UnityEngine;
using System.Linq;
using TMPro;
using UnityEngine.UI;

public class MainController : MonoBehaviour
{
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private string _filePath;

    [Space] [SerializeField] private int _totalPeakInfosCount = 1000;
    [SerializeField] private float _peakMinHeight = 5;
    [SerializeField] private float _minPeakHeightMultiplier = 1f;
    [SerializeField] private float _peakInterpolation = 0.1f;
    [ReadOnly] [SerializeField] private float _maxPeakHeight;


    [Space] [SerializeField] private RectTransform _peaksContainer;
    [SerializeField] private TMP_Text _timeText;


    private MaxMinPeak[] _peakInstances;
    private float[] _readBuffer;

    private bool _timerIsRunning;
    private float _currentTime;
    private TimeSpan _audioDuration;
    private List<PeakInfo> _peakInfos;
    private PeakInfo[] _targetPeakHeights;
    private PeakInfo _defaultPeakInfo;
    private int _lastCenterPeakInfoIndex;

    private void Awake()
    {
        _peakInstances = _peaksContainer.GetComponentsInChildren<MaxMinPeak>();
        _targetPeakHeights = new PeakInfo[_peakInstances.Length];
        _maxPeakHeight = _peaksContainer.rect.height / 2;
        _defaultPeakInfo = new PeakInfo(-_peakMinHeight / _maxPeakHeight, _peakMinHeight / _maxPeakHeight);
    }

    private void RetrieveAudioInfo(out List<PeakInfo> peakInfos, out TimeSpan duration)
    {
        if (_filePath == null) throw new Exception("file path is null");

        peakInfos = new List<PeakInfo>();

        using (var waveStream = new AudioFileReader(_filePath))
        {
            int bytesPerSample = waveStream.WaveFormat.BitsPerSample / 8;
            var samples = waveStream.Length / bytesPerSample;
            var samplesPerPixel = (int) (samples / _totalPeakInfosCount);
            _readBuffer = new float[samplesPerPixel];

            var sp = waveStream.ToSampleProvider();

            for (int i = 0; i < _totalPeakInfosCount; i++)
            {
                var samplesRead = sp.Read(_readBuffer, 0, _readBuffer.Length);

                var max = (samplesRead == 0) ? 0 : _readBuffer.Take(samplesRead).Max();
                var min = (samplesRead == 0) ? 0 : _readBuffer.Take(samplesRead).Min();
                peakInfos.Add(new PeakInfo(min, max));
            }

            duration = waveStream.TotalTime;
        }
    }

    [Button]
    private void Play()
    {
        RetrieveAudioInfo(out _peakInfos, out _audioDuration);

        _currentTime = 0;
        _timerIsRunning = true;
        _lastCenterPeakInfoIndex = -1;

        _audioSource.Play();
    }

    private void Update()
    {
        if (!_timerIsRunning) return;

        _currentTime += Time.deltaTime;
        DisplayTime(_currentTime);

        UpdatePeaks();
    }

    private void UpdatePeaks()
    {
        int instancesLength = _peakInstances.Length;
        int peakInfosLength = _peakInfos.Count;
        float audioDurationSec = (float) _audioDuration.TotalSeconds;

        int centerPeakInfoIndex =
            Mathf.RoundToInt((float) _currentTime / (float) audioDurationSec * _totalPeakInfosCount);

        if (centerPeakInfoIndex != _lastCenterPeakInfoIndex)
        {
            _lastCenterPeakInfoIndex = centerPeakInfoIndex;

            for (int i = 0; i < instancesLength; i++)
            {
                var peakInfoIndex = Mathf.RoundToInt(centerPeakInfoIndex - (float) instancesLength / 2) + i;
                PeakInfo peakInfo = peakInfoIndex >= 0 && peakInfoIndex < peakInfosLength
                    ? _peakInfos[peakInfoIndex]
                    : _defaultPeakInfo;

                float extendedI = i * instancesLength / (instancesLength - 1f);
                float halfInstancesLength = instancesLength / 2f;
                float closenessToTheCenter = 1f - Math.Abs(extendedI / halfInstancesLength - 1f);

                float sinClosenessToTheCenter = Mathf.Pow(Mathf.Sin(closenessToTheCenter * Mathf.PI / 2f), 5);

                _targetPeakHeights[i] = new PeakInfo(
                    Mathf.Max(-peakInfo.Min * _maxPeakHeight * sinClosenessToTheCenter * _minPeakHeightMultiplier, _peakMinHeight),
                    Mathf.Max(peakInfo.Max * _maxPeakHeight * sinClosenessToTheCenter, _peakMinHeight)
                );
            }
        }

        for (int i = 0; i < instancesLength; i++)
        {
            var peakInstance = _peakInstances[i];
            PeakInfo targetHeights = _targetPeakHeights[i];

            var sizeDelta = peakInstance.MaxPeak.rectTransform.sizeDelta;
            peakInstance.MaxPeak.rectTransform.sizeDelta =
                new Vector2(sizeDelta.x, Mathf.Lerp(sizeDelta.y, targetHeights.Max, _peakInterpolation));

            sizeDelta = peakInstance.MinPeak.rectTransform.sizeDelta;
            peakInstance.MinPeak.rectTransform.sizeDelta =
                new Vector2(sizeDelta.x, Mathf.Lerp(sizeDelta.y, targetHeights.Min, _peakInterpolation));
        }
    }

    private void DisplayTime(float timeToDisplay)
    {
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);
        _timeText.text = $"{minutes:00}:{seconds:00}";
    }
}