using UnityEngine;
using UnityEngine.UIElements;

public class FFT
{
    const int numthreadsX = 8, numthreadsY = 8;
    readonly int size;
    readonly ComputeShader fftshader;
    readonly RenderTexture FFTParameterBuffer;

    // Shader property IDs: 
    readonly int ID_SIZE = Shader.PropertyToID("Size");
    readonly int ID_Step = Shader.PropertyToID("Step");
    readonly int ID_PARAMETERBUFFER = Shader.PropertyToID("FFTParameterBuffer");
    readonly int ID_PARAMETERDATA = Shader.PropertyToID("FFTParameterData");
    readonly int ID_BUFFER0 = Shader.PropertyToID("Buffer0");
    readonly int ID_BUFFER1 = Shader.PropertyToID("Buffer1");
    readonly int ID_PINGPONG = Shader.PropertyToID("PingPong");

    // Kernel IDs: 
    readonly int ID_KERNEL_GENFFTPARAMETERS;
    readonly int ID_KERNEL_X_FFT;
    readonly int ID_KERNEL_Y_FFT;
    readonly int ID_KERNEL_X_IFFT;
    readonly int ID_KERNEL_Y_IFFT;
    readonly int ID_KERNEL_PERMUTE;
    readonly int ID_KERNEL_SCALE;

    public FFT(ComputeShader fftshader, int size)
    {
        this.fftshader = fftshader;
        this.size = size;
        ID_KERNEL_GENFFTPARAMETERS = fftshader.FindKernel("GenFFTParameters");
        ID_KERNEL_X_FFT = fftshader.FindKernel("X_FFT");
        ID_KERNEL_Y_FFT = fftshader.FindKernel("Y_FFT");
        ID_KERNEL_X_IFFT = fftshader.FindKernel("X_IFFT");
        ID_KERNEL_Y_IFFT = fftshader.FindKernel("Y_IFFT");
        FFTParameterBuffer = GenFFTParameters(size);
        ID_KERNEL_PERMUTE = fftshader.FindKernel("Permute");
        ID_KERNEL_SCALE = fftshader.FindKernel("Scale");
    }

    RenderTexture GenFFTParameters(int size)
    {
        int X = (int)Mathf.Log(size, 2);
        int Y = size;
        RenderTexture rt = new RenderTexture(X, Y, 0, RenderTextureFormat.ARGBFloat)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Point
        };
        rt.Create();

        fftshader.SetInt(ID_SIZE, size);
        // In Unity compute shaders, the texture is bound to the kernel, not the compute shader globally. 
        fftshader.SetTexture(ID_KERNEL_GENFFTPARAMETERS, ID_PARAMETERBUFFER, rt);
        fftshader.Dispatch(ID_KERNEL_GENFFTPARAMETERS, X, Y / numthreadsY / 2, 1);
        return rt;
    }

    public void FFT2D(RenderTexture input, RenderTexture buffer, bool outputToInput = false,
        bool permute = true, bool scale = false)
    {
        // First convert each row of the input texture to frequency domain. 
        int logSize = (int)Mathf.Log(size, 2);
        fftshader.SetTexture(ID_KERNEL_X_FFT, ID_PARAMETERDATA, FFTParameterBuffer);
        fftshader.SetTexture(ID_KERNEL_X_FFT, ID_BUFFER0, input);
        fftshader.SetTexture(ID_KERNEL_X_FFT, ID_BUFFER1, buffer);
        bool pingpong = false;
        for (int i = 0; i < logSize; i++)
        {
            pingpong = !pingpong;
            fftshader.SetInt(ID_Step, i);
            fftshader.SetBool(ID_PINGPONG, pingpong);
            fftshader.Dispatch(ID_KERNEL_X_FFT, 
                size / numthreadsX, size / numthreadsY, 1);
        }
        fftshader.SetTexture(ID_KERNEL_Y_FFT, ID_PARAMETERDATA, FFTParameterBuffer);
        fftshader.SetTexture(ID_KERNEL_Y_FFT, ID_BUFFER0, input);
        fftshader.SetTexture(ID_KERNEL_Y_FFT, ID_BUFFER1, buffer);
        for (int i = 0; i < logSize; i++)
        {
            pingpong = !pingpong;
            fftshader.SetInt(ID_Step, i);
            fftshader.SetBool(ID_PINGPONG, pingpong);
            fftshader.Dispatch(ID_KERNEL_Y_FFT, 
                size / numthreadsX, size / numthreadsY, 1);
        }
        if(outputToInput && pingpong)
        {
            Graphics.Blit(buffer, input);
        }
        else if (!outputToInput && !pingpong)
        {
            Graphics.Blit(input, buffer);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="input"></param>
    /// <param name="buffer"></param>
    /// <param name="outputToInput"></param>
    /// <param name="permute"> Convert the DC centered frequency diagram to the DC-at-corner frequency diagram. </param>
    /// <param name="scale"> Divide the result by Size*Size. </param>
    public void IFFT2D(RenderTexture input, RenderTexture buffer, bool outputToInput = true,
        bool permute = true, bool scale = false)
    {
        // First convert each row of the input texture to frequency domain. 
        int logSize = (int)Mathf.Log(size, 2);
        fftshader.SetTexture(ID_KERNEL_X_IFFT, ID_PARAMETERDATA, FFTParameterBuffer);
        fftshader.SetTexture(ID_KERNEL_X_IFFT, ID_BUFFER0, input);
        fftshader.SetTexture(ID_KERNEL_X_IFFT, ID_BUFFER1, buffer);
        bool pingpong = false;
        for (int i = 0; i < logSize; i++)
        {
            pingpong = !pingpong;
            fftshader.SetInt(ID_Step, i);
            fftshader.SetBool(ID_PINGPONG, pingpong);
            fftshader.Dispatch(ID_KERNEL_X_IFFT,
                size / numthreadsX, size / numthreadsY, 1);
        }
        fftshader.SetTexture(ID_KERNEL_Y_IFFT, ID_PARAMETERDATA, FFTParameterBuffer);
        fftshader.SetTexture(ID_KERNEL_Y_IFFT, ID_BUFFER0, input);
        fftshader.SetTexture(ID_KERNEL_Y_IFFT, ID_BUFFER1, buffer);
        for (int i = 0; i < logSize; i++)
        {
            pingpong = !pingpong;
            fftshader.SetInt(ID_Step, i);
            fftshader.SetBool(ID_PINGPONG, pingpong);
            fftshader.Dispatch(ID_KERNEL_Y_IFFT,
                size / numthreadsX, size / numthreadsY, 1);
        }
        if (outputToInput && pingpong)
        {
            Graphics.Blit(buffer, input);
        }
        else if (!outputToInput && !pingpong)
        {
            Graphics.Blit(input, buffer);
        }
        
        if (permute)
        {
            fftshader.SetInt(ID_SIZE, size);
            fftshader.SetTexture(ID_KERNEL_PERMUTE, ID_BUFFER0, outputToInput ? input : buffer);
            fftshader.Dispatch(ID_KERNEL_PERMUTE, size / numthreadsX, size / numthreadsY, 1);
        }

        if (scale)
        {
            fftshader.SetInt(ID_SIZE, size);
            fftshader.SetTexture(ID_KERNEL_SCALE, ID_BUFFER0, outputToInput ? input : buffer);
            fftshader.Dispatch(ID_KERNEL_SCALE, size / numthreadsX, size / numthreadsY, 1);
        }
    }
}
