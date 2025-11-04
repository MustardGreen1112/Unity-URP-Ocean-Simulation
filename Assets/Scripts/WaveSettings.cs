using UnityEngine;


[System.Serializable]
public struct SpectrumSettings
{
    [Range(0.01f, 10.0f)]
    public float minWavelength;
    /// <summary>
    /// Determines how steep/high the waves are. 
    /// </summary>
    public float scalingFactor;
    public float windSpeed;
    /// <summary>
    /// The theta angle between the wind direction and the world x-axis. 
    /// 0 means the wind is aligned with the world-x while 0.5 means the wind is alinged with the world-minus-x axis. 
    /// Rotating counterclockwise. 
    /// </summary>
    [Range(0, 1)]
    public float windDirection;
}

[CreateAssetMenu(fileName = "WaveSettings", menuName = "Scriptable Objects/WaveSettings")]
public class WaveSettings : ScriptableObject
{
    public float g;
    public float depth;
    public SpectrumSettings spectrumSettings;
}
