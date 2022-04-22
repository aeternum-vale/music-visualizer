using UnityEngine;
using UnityEngine.UI;

public class MaxMinPeak : MonoBehaviour
{
    [SerializeField] private Image _maxPeak;
    [SerializeField] private Image _minPeak;

    public Image MaxPeak => _maxPeak;
    public Image MinPeak => _minPeak;
}