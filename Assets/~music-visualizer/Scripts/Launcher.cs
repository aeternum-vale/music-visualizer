using System;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEngine;
using Utility;


public class Launcher : MonoBehaviour
{
    private static RecorderWindow _recorderWindow;
    
    private const string StartKey = "start";
    private const string DurationKey = "duration";

    private const float FlyerDefaultStartTimeSec = 10f;
    private const float FlyerDefaultDurationSec = 15f;

    [SerializeField] private PeaksWaveVisualizer _peaksWaveVisualizer;


    public static void StartRecord()
    {
        var args = Environment.GetCommandLineArgs();
        Arguments commandLineArguments = new Arguments(args);

        if (commandLineArguments[StartKey] != null)
            PlayerPrefs.SetFloat(StartKey, float.Parse(commandLineArguments[StartKey]));
        else
            PlayerPrefs.SetFloat(StartKey, FlyerDefaultStartTimeSec);
        
        if (commandLineArguments[DurationKey] != null)
            PlayerPrefs.SetFloat(DurationKey, float.Parse(commandLineArguments[DurationKey]));
        else
            PlayerPrefs.SetFloat(DurationKey, FlyerDefaultDurationSec);

        EditorApplication.isPlaying = true;
    }

    private void Start()
    {
        _recorderWindow = GetRecorderWindow();

        _peaksWaveVisualizer.OnFullDurationReach += async (sender, args) =>
        {
            Debug.Log("Launcher Stop Record");

            _peaksWaveVisualizer.Stop();
            _recorderWindow.StopRecording();
            await UniTask.Delay(TimeSpan.FromSeconds(2f), DelayType.Realtime);
            await UniTask.WaitUntil(() => !_recorderWindow.IsRecording());
            EditorApplication.Exit(0);
        };

        _recorderWindow.StartRecording();

        float start = PlayerPrefs.GetFloat(StartKey);
        float duration = PlayerPrefs.GetFloat(DurationKey);
        
        _peaksWaveVisualizer.Play(start, duration);
    }

    private static RecorderWindow GetRecorderWindow()
    {
        return (RecorderWindow) EditorWindow.GetWindow(typeof(RecorderWindow));
    }
}