using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class WaveManager : MonoBehaviour
{
    Wave wave;
    [SerializeField]
    int size = 512;
    [SerializeField]
    WaveSettings waveSettings;
    [SerializeField]
    float lengthScale = 10.0f;
    [SerializeField]
    ComputeShader initialSpectrumComputeShader;
    [SerializeField]
    ComputeShader fftComputeShader;
    [SerializeField]
    ComputeShader spectrumComputeShader;
    [SerializeField]
    Texture2D gaussianNoise;
    [SerializeField]
    Material waveMaterial;
    RenderTexture amplitude_h, amplitude_Dx, amplietude_Dz;
    FFT fft;

    private void Awake()
    {
        fft = new FFT(fftComputeShader, size);
        wave = new Wave(size, waveSettings, initialSpectrumComputeShader, spectrumComputeShader,
            fft, gaussianNoise, lengthScale);
        amplitude_h = wave.Amplitude_h;
        amplitude_Dx = wave.Amplitude_Dx;
        amplietude_Dz = wave.Amplitude_Dz;
    }

    private void Start()
    {
        // Set the wave material properties
        waveMaterial.SetTexture("_Amplitude_h", amplitude_h);
        waveMaterial.SetTexture("_Amplitude_Dx", amplitude_Dx);
        waveMaterial.SetTexture("_Amplitude_Dz", amplietude_Dz);
        waveMaterial.SetFloat("_LengthScale", lengthScale);
        wave.ComputeInitialSpectrum();

    }
    void Update()
    {
        //wave = new Wave(size, waveSettings, initialSpectrumComputeShader, spectrumComputeShader,
        //    fft, gaussianNoise, lengthScale);
        //wave.ComputeInitialSpectrum();
        wave.ComputeHeightField(Time.time);
    }

    private void OnDestroy()
    {
        wave?.Dispose();
    }
}
