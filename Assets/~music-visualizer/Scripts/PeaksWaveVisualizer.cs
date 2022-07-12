using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;
using NAudioWaveFormRenderer;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PeaksWaveVisualizer : MonoBehaviour
{
    [FormerlySerializedAs("_filePath")] [SerializeField]
    private string _audioFilePath;

    [SerializeField] private string _coverFilePath;
    [SerializeField] private string _songArtist;
    [SerializeField] private string _songTitle;

    [Space] [SerializeField] private EPeakProviderType _peakProviderType = EPeakProviderType.Max;
    [SerializeField] private int _totalPeakInfosCount = 1000;
    [SerializeField] private float _peakMinHeight = 5;
    [SerializeField] private float _minPeakHeightMultiplier = 1f;
    [SerializeField] private float _peakInterpolation = 0.1f;
    [ReadOnly] [SerializeField] private float _maxPeakHeight;

    [Space] [SerializeField] private TMP_Text _songArtistText;
    [SerializeField] private TMP_Text _songTitleText;
    [SerializeField] private Image _coverImage;
    [SerializeField] private SpriteRenderer _blurredCoverImage;
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private RectTransform _peaksContainer;

    [Space] [SerializeField] private float _waveMaxBloom = 0.2f;
    [SerializeField] private float _waveBloomInterpolation = 1f;
    [SerializeField] private float _waveBloomPower = 1f;
    [SerializeField] private EPeakProviderType _waveBloomPeakProvider = EPeakProviderType.Max;
    [SerializeField] private PostProcessProfile _ppProfile;

    [Space] [SerializeField] private bool _testMode = false;


    private MaxMinPeak[] _peakInstances;
    private float[] _readBuffer;

    private bool _isPlaying;
    private float _currentTime;
    private TimeSpan _audioDuration;
    private List<PeakInfo> _peakInfos;
    private List<PeakInfo> _waveBgPeakInfos;
    private PeakInfo[] _targetPeakHeights;
    private PeakInfo _defaultPeakInfo;
    private int _lastCenterPeakInfoIndex;
    private Bloom _ppBloom;

    private const float TimeDeltaFor220Fps = 1f / 220f;

    private float CorrectedTimeDelta => Time.deltaTime / TimeDeltaFor220Fps;

    //public static PeaksWaveVisualizer Instance { get; private set; }

    public float _flyerStartSec;
    public float _flyerDurationSec;
    public event EventHandler OnFullDurationReach;

    public static PeaksWaveVisualizer Instance { get; private set; }


    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        _peakInstances = _peaksContainer.GetComponentsInChildren<MaxMinPeak>();
        _targetPeakHeights = new PeakInfo[_peakInstances.Length];
        _maxPeakHeight = _peaksContainer.rect.height / 2;
        _defaultPeakInfo = new PeakInfo(-_peakMinHeight / _maxPeakHeight, _peakMinHeight / _maxPeakHeight);

        _ppBloom = _ppProfile.GetSetting<Bloom>();
    }

    [Button]
    private void PlayDefault() => Play(0f, 15f);
    
    
    public void Play(float flyerStartSec, float flyerDurationSec)
    {
        _flyerStartSec = flyerStartSec;
        _flyerDurationSec = flyerDurationSec;

        Play();
    }


    private void Play()
    {
        RetrieveAudioFileTags(out Sprite sprite, out string artist, out string title);

        _coverImage.sprite = sprite;
        _blurredCoverImage.sprite = sprite;

        _songArtistText.text = artist;
        _songTitleText.text = title;

        RetrieveAudioPeaksAndDuration(GetPeakProvider(_peakProviderType), out _peakInfos, out _audioDuration);
        if (_peakProviderType != _waveBloomPeakProvider)
            RetrieveAudioPeaksAndDuration(GetPeakProvider(_waveBloomPeakProvider), out _waveBgPeakInfos, out _);
        else
            _waveBgPeakInfos = _peakInfos;

        _audioSource.clip = CreateAudioClip();
        _audioSource.clip.LoadAudioData();

        _currentTime = 0;
        _isPlaying = true;
        _lastCenterPeakInfoIndex = -1;

        _audioSource.Play();
        _audioSource.time = _flyerStartSec;
    }

    public void Stop()
    {
        _isPlaying = false;
        _audioSource.Stop();
    }

    private AudioClip CreateAudioClip()
    {
        AudioClip clip;
        using (var waveStream = new AudioFileReader(_audioFilePath))
        {
            var audioData = new float[waveStream.Length];
            waveStream.Read(audioData, 0, (int) waveStream.Length);

            clip = AudioClip.Create("title", (int) waveStream.Length, waveStream.WaveFormat.Channels,
                waveStream.WaveFormat.SampleRate, false);
            clip.SetData(audioData, 0);
        }

        return clip;
    }


    private void RetrieveAudioPeaksAndDuration(IPeakProvider peakProvider, out List<PeakInfo> peakInfos,
        out TimeSpan duration)
    {
        if (_audioFilePath == null) throw new Exception("file path is null");

        peakInfos = new List<PeakInfo>();

        using (var waveStream = new AudioFileReader(_audioFilePath))
        {
            int bytesPerSample = waveStream.WaveFormat.BitsPerSample / 8;
            var samples = waveStream.Length / bytesPerSample;
            var samplesPerPixel = (int) (samples / _totalPeakInfosCount);
            peakProvider.Init(waveStream.ToSampleProvider(), samplesPerPixel);

            for (int i = 0; i < _totalPeakInfosCount; i++)
                peakInfos.Add(peakProvider.GetNextPeak());

            duration = waveStream.TotalTime;
        }
    }

    private void RetrieveAudioFileTags(out Sprite cover, out string artist, out string title)
    {
        var audioFile = TagLib.File.Create(_audioFilePath);
        var byteArray = audioFile.Tag.Pictures[0].Data.ToArray();
        cover = CreateSpriteOutOf(byteArray);
        artist = audioFile.Tag.FirstAlbumArtist;
        title = audioFile.Tag.Title;
    }

    private Sprite ReadCoverImageFile()
    {
        byte[] byteArray = File.ReadAllBytes(_coverFilePath);
        return CreateSpriteOutOf(byteArray);
    }

    private Sprite CreateSpriteOutOf(byte[] byteArray)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.LoadImage(byteArray);
        tex.Apply();
        var rect = _coverImage.rectTransform.rect;
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        return sprite;
    }

    private IPeakProvider GetPeakProvider(EPeakProviderType type)
    {
        switch (type)
        {
            case EPeakProviderType.Average:
                return new AveragePeakProvider(4);
            case EPeakProviderType.Decibel:
                return new DecibelPeakProvider(new MaxPeakProvider(), 48);
            case EPeakProviderType.Max:
                return new MaxPeakProvider();
            case EPeakProviderType.Rms:
                return new RmsPeakProvider(4);
            case EPeakProviderType.Sampling:
                return new SamplingPeakProvider(4);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }


    private void Update()
    {
        if (!_isPlaying) return;

        _currentTime += Time.deltaTime;
        DisplayTime(_audioSource.time);

        UpdatePeaks();
    }

    private void UpdatePeaks()
    {
        if (_audioSource.time > _audioSource.clip.length) return;

        if (_audioSource.time >= _flyerStartSec + _flyerDurationSec)
        {
            Debug.Log($"OnDurationReach");
            OnFullDurationReach?.Invoke(this, EventArgs.Empty);
        }


        int instancesLength = _peakInstances.Length;
        int peakInfosLength = _peakInfos.Count;
        float audioDurationSec = (float) _audioDuration.TotalSeconds;

        int centerPeakInfoIndex =
            Mathf.RoundToInt((float) _audioSource.time / (float) audioDurationSec * _totalPeakInfosCount);

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

                if (_testMode)
                    sinClosenessToTheCenter = 1f;

                _targetPeakHeights[i] = new PeakInfo(
                    Mathf.Max(-peakInfo.Min * _maxPeakHeight * sinClosenessToTheCenter * _minPeakHeightMultiplier,
                        _peakMinHeight),
                    Mathf.Max(peakInfo.Max * _maxPeakHeight * sinClosenessToTheCenter, _peakMinHeight)
                );
            }
        }

        for (int i = 0; i < instancesLength; i++)
        {
            var peakInstance = _peakInstances[i];
            PeakInfo targetHeights = _targetPeakHeights[i];

            var finalPeakInterpolation = _testMode ? 1f : _peakInterpolation;

            var sizeDelta = peakInstance.MaxPeak.rectTransform.sizeDelta;
            peakInstance.MaxPeak.rectTransform.sizeDelta =
                new Vector2(sizeDelta.x,
                    Mathf.Lerp(sizeDelta.y, targetHeights.Max, finalPeakInterpolation * CorrectedTimeDelta));

            sizeDelta = peakInstance.MinPeak.rectTransform.sizeDelta;
            peakInstance.MinPeak.rectTransform.sizeDelta =
                new Vector2(sizeDelta.x,
                    Mathf.Lerp(sizeDelta.y, targetHeights.Min, finalPeakInterpolation * CorrectedTimeDelta));
        }

        float newWaveBgAlpha =
            Mathf.Lerp(
                _ppBloom.intensity.value,
                Mathf.Pow(
                    Mathf.Clamp(_waveBgPeakInfos[centerPeakInfoIndex].Max, 0f, 1f),
                    _waveBloomPower) *
                _waveMaxBloom,
                _waveBloomInterpolation * CorrectedTimeDelta);

        _ppBloom.intensity.value = newWaveBgAlpha;
    }

    private void DisplayTime(float timeToDisplay)
    {
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);
        _timeText.text = $"{minutes:00}:{seconds:00}";
    }
}