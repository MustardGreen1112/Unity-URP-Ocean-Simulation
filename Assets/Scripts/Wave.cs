using System;
using System.Drawing;
using UnityEditor;
using UnityEngine;

public class Wave
{
    readonly FFT fft;
    readonly int size;
    readonly float lengthScale;
    /// <summary>
    /// From ScriptableObject WaveSettings.
    /// </summary>
    readonly WaveSettings settings; 
    readonly ComputeBuffer spectrumParamsList;
    readonly RenderTexture waveData;
    readonly Texture2D gaussianNoise;
    /// <summary>
    /// hat(H)(k, 0).
    /// </summary>
    readonly RenderTexture initialSpectrum;
    /// <summary>
    /// x - amplitude at t, yxw - gradient at t. 
    /// </summary>
    public RenderTexture Amplitude_h => amplitudes_h;
    public RenderTexture Amplitude_Dx => amplitudes_Dx;
    public RenderTexture Amplitude_Dz => amplitudes_Dz;

    // Debug: 
    public RenderTexture WaveData => waveData;
    public RenderTexture InitialSpectrum => initialSpectrum;

    readonly RenderTexture amplitudes_h;
    readonly RenderTexture amplitudes_Dx;
    readonly RenderTexture amplitudes_Dz;
    readonly RenderTexture buffer; // Buffer for compute H0K and conjugate of H0K (for test -) and IFFT.

    readonly ComputeShader initialSpectrumComputeShader;
    readonly ComputeShader spectrumComputeShader;
    const int numthreadsX = 8, numthreadsY = 8;

    readonly int ID_GAUSSIANRV = Shader.PropertyToID("GaussianRV");
    readonly int ID_INITIALSPECTRUM = Shader.PropertyToID("InitialSpectrum");
    readonly int ID_WAVEDATA = Shader.PropertyToID("WaveData");
    readonly int ID_SPECTRUMPARAMS = Shader.PropertyToID("Spectrums");
    readonly int ID_H0K = Shader.PropertyToID("H0k");
    readonly int ID_G = Shader.PropertyToID("G");
    readonly int ID_DEPTH = Shader.PropertyToID("Depth");
    readonly int ID_SIZE = Shader.PropertyToID("Size");
    readonly int ID_LENGTHSCALE = Shader.PropertyToID("LengthScale");

    readonly int ID_AMPLITUDES_H = Shader.PropertyToID("Amplitudes_h");
    readonly int ID_AMPLITUDES_DX = Shader.PropertyToID("Amplitudes_Dx");
    readonly int ID_AMPLITUDES_DZ = Shader.PropertyToID("Amplitudes_Dz");
    readonly int ID_TIME = Shader.PropertyToID("Time");

    readonly int ID_KERNEL_COMPUTEINITIALSPECTRUM;
    readonly int ID_KERNEL_COMPUTECONJUGATE;
    readonly int ID_KERNEL_COMPUTE_AMPLITUDES_AT_T;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="size">The length of sample points along one axis. (N) </param>
    /// <param name="settings"></param>
    /// <param name="initialSpectrumComputeShader"></param>
    /// <param name="fft"></param>
    /// <param name="gaussianNoise"></param>
    /// <param name="lengthScale">The actual length of the ocean in world space. </param>
    public Wave(int size, WaveSettings settings, 
        ComputeShader initialSpectrumComputeShader, ComputeShader spectrumComputeShader,
        FFT fft, Texture2D gaussianNoise, float lengthScale) {
        this.fft = fft;
        this.size = size;
        this.lengthScale = lengthScale;
        this.settings = settings;
        this.initialSpectrumComputeShader = initialSpectrumComputeShader;
        this.spectrumComputeShader = spectrumComputeShader;
        this.gaussianNoise = gaussianNoise;
        spectrumParamsList = new ComputeBuffer(1, 4 * sizeof(float));
        spectrumParamsList.SetData(new SpectrumSettings[] {settings.spectrumSettings});
        waveData = CreateRenderTexture(size, RenderTextureFormat.ARGBFloat, true);
        initialSpectrum = CreateRenderTexture(size, RenderTextureFormat.ARGBFloat, true);

        amplitudes_h = CreateRenderTexture(size);
        amplitudes_Dx = CreateRenderTexture(size);
        amplitudes_Dz = CreateRenderTexture(size);
        buffer = CreateRenderTexture(size);

        ID_KERNEL_COMPUTEINITIALSPECTRUM = initialSpectrumComputeShader.FindKernel("ComputeInitialSpectrum");
        ID_KERNEL_COMPUTECONJUGATE = initialSpectrumComputeShader.FindKernel("ComputeConjugate");
        ID_KERNEL_COMPUTE_AMPLITUDES_AT_T = spectrumComputeShader.FindKernel("ComputeAmplitudesAtT");
    }

    public void ComputeInitialSpectrum()
    {
        // Read
        Texture2D gv = GetNoiseTexture(size);
        // gv = gaussianNoise;
        initialSpectrumComputeShader.SetTexture(ID_KERNEL_COMPUTEINITIALSPECTRUM, ID_GAUSSIANRV, gv);
        initialSpectrumComputeShader.SetBuffer(ID_KERNEL_COMPUTEINITIALSPECTRUM, ID_SPECTRUMPARAMS, spectrumParamsList);
        initialSpectrumComputeShader.SetFloat(ID_G, settings.g);
        initialSpectrumComputeShader.SetFloat(ID_DEPTH, settings.depth);
        initialSpectrumComputeShader.SetInt(ID_SIZE, size);
        initialSpectrumComputeShader.SetFloat(ID_LENGTHSCALE, lengthScale);
        // Write
        initialSpectrumComputeShader.SetTexture(ID_KERNEL_COMPUTEINITIALSPECTRUM, ID_WAVEDATA, waveData);
        initialSpectrumComputeShader.SetTexture(ID_KERNEL_COMPUTEINITIALSPECTRUM, ID_H0K, buffer);
        // Dispatch
        initialSpectrumComputeShader.Dispatch(ID_KERNEL_COMPUTEINITIALSPECTRUM, size / numthreadsX, size / numthreadsY, 1);
        // Compute Conjugate
        initialSpectrumComputeShader.SetTexture(ID_KERNEL_COMPUTECONJUGATE, ID_INITIALSPECTRUM, initialSpectrum);
        initialSpectrumComputeShader.SetTexture(ID_KERNEL_COMPUTECONJUGATE, ID_H0K, buffer);
        initialSpectrumComputeShader.Dispatch(ID_KERNEL_COMPUTECONJUGATE, size / numthreadsX, size / numthreadsY, 1);
    }



    public void ComputeHeightField(float time)
    {
        spectrumComputeShader.SetTexture(ID_KERNEL_COMPUTE_AMPLITUDES_AT_T, ID_INITIALSPECTRUM, initialSpectrum);
        spectrumComputeShader.SetTexture(ID_KERNEL_COMPUTE_AMPLITUDES_AT_T, ID_WAVEDATA, waveData);
        spectrumComputeShader.SetTexture(ID_KERNEL_COMPUTE_AMPLITUDES_AT_T, ID_AMPLITUDES_H, amplitudes_h);
        spectrumComputeShader.SetTexture(ID_KERNEL_COMPUTE_AMPLITUDES_AT_T, ID_AMPLITUDES_DX, amplitudes_Dx);
        spectrumComputeShader.SetTexture(ID_KERNEL_COMPUTE_AMPLITUDES_AT_T, ID_AMPLITUDES_DZ, amplitudes_Dz);
        spectrumComputeShader.SetFloat(ID_TIME, time);
        spectrumComputeShader.Dispatch(ID_KERNEL_COMPUTE_AMPLITUDES_AT_T, size / numthreadsX, size / numthreadsY, 1);
        fft.IFFT2D(amplitudes_h, buffer, true);
        fft.IFFT2D(amplitudes_Dx, buffer, true);
        fft.IFFT2D(amplitudes_Dz, buffer, true);
    }

    Texture2D GetNoiseTexture(int size)
    {
        string filename = "GaussianNoiseTexture" + size.ToString() + "x" + size.ToString();
        Texture2D noise = Resources.Load<Texture2D>("GaussianNoiseTextures/" + filename);
        return noise ? noise : GenerateNoiseTexture(size, true);
    }

    Texture2D GenerateNoiseTexture(int size, bool saveIntoAssetFile)
    {
        Texture2D noise = new Texture2D(size, size, TextureFormat.RGFloat, false, true);
        noise.filterMode = FilterMode.Point;
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                noise.SetPixel(i, j, new Vector4(NormalRandom().x, NormalRandom().y));
            }
        }
        noise.Apply();

#if UNITY_EDITOR
        if (saveIntoAssetFile)
        {
            string filename = "GaussianNoiseTexture" + size.ToString() + "x" + size.ToString();
            string path = "Assets/Resources/GaussianNoiseTextures/";
            AssetDatabase.CreateAsset(noise, path + filename + ".asset");
            Debug.Log("Texture \"" + filename + "\" was created at path \"" + path + "\".");
        }
#endif
        return noise;
    }

    Vector2 NormalRandom()
    {
        float u1 = UnityEngine.Random.value;
        float u2 = UnityEngine.Random.value;

        float radius = Mathf.Sqrt(-2.0f * Mathf.Log(u1));
        float theta = 2.0f * Mathf.PI * u2;

        return new Vector2(
            radius * Mathf.Cos(theta),
            radius * Mathf.Sin(theta)
        );
    }

    public void Dispose()
    {
        spectrumParamsList?.Release();
    }

    RenderTexture CreateRenderTexture(int size, RenderTextureFormat format = RenderTextureFormat.RGFloat, bool useMips = false)
    {
        RenderTexture rt = new RenderTexture(size, size, 0,
            format, RenderTextureReadWrite.Linear);
        rt.useMipMap = useMips;
        rt.autoGenerateMips = false;
        rt.anisoLevel = 6;
        rt.filterMode = FilterMode.Trilinear;
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }


}
