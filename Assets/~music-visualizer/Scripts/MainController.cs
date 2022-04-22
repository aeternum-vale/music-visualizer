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
    [SerializeField] private int _totalPeakInfosCount = 1000;
    [SerializeField] private float _minPeakHeight = 5;
    [ReadOnly] [SerializeField] private float _maxPeakHeight;

    [SerializeField] private string _filePath;

    [Space] [SerializeField] private RectTransform _peaksContainer;
    [SerializeField] private TMP_Text _timeText;


    private MaxMinPeak[] _peakInstances;
    private float[] _readBuffer;

    private bool _timerIsRunning;
    private float _currentTime;
    private TimeSpan _audioDuration;
    private List<PeakInfo> _peakInfos;
    private PeakInfo _defaultPeakInfo;

    private void Awake()
    {
        _peakInstances = _peaksContainer.GetComponentsInChildren<MaxMinPeak>();
        _maxPeakHeight = _peaksContainer.rect.height / 2;
        _defaultPeakInfo = new PeakInfo(-_minPeakHeight / _maxPeakHeight, _minPeakHeight / _maxPeakHeight);
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

        for (int i = 0; i < instancesLength; i++)
        {
            var peakInfoIndex = Mathf.RoundToInt(centerPeakInfoIndex - (float) instancesLength / 2) + i;
            PeakInfo peakInfo = peakInfoIndex >= 0 && peakInfoIndex < peakInfosLength
                ? _peakInfos[peakInfoIndex]
                : _defaultPeakInfo;

            var peakInstance = _peakInstances[i];

            var sizeDelta = peakInstance.MaxPeak.rectTransform.sizeDelta;
            peakInstance.MaxPeak.rectTransform.sizeDelta = new Vector2(sizeDelta.x, peakInfo.Max * _maxPeakHeight);

            sizeDelta = peakInstance.MinPeak.rectTransform.sizeDelta;
            peakInstance.MinPeak.rectTransform.sizeDelta = new Vector2(sizeDelta.x, -peakInfo.Min * _maxPeakHeight);
        }
    }

    private void DisplayTime(float timeToDisplay)
    {
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);
        _timeText.text = $"{minutes:00}:{seconds:00}";
    }
}