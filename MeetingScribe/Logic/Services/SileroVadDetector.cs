using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MeetingScribe.Logic.Services;

public class SileroVadDetector : IDisposable
{
    private readonly InferenceSession _session;
    private readonly float[] _state = new float[2 * 1 * 128]; // The internal state of an RNN (h и c)
    private readonly int _sampleRate = 16000;

    public SileroVadDetector(string modelPath)
    {
        _session = new InferenceSession(modelPath);
    }

    public void ResetState()
    {
        Array.Clear(_state, 0, _state.Length);
    }

    /// <summary>
    /// Checks a 30-millisecond audio sample. Returns a speech probability between 0.0 and 1.0
    /// </summary>
    public float IsSpeechProbability(float[] samples)
    {
        if (samples.Length != 480)
            throw new ArgumentException("Размер чанка должен быть строго 480 сэмплов (30 мс).");

        // Creating Tensors for ONNX
        var inputTensor = new DenseTensor<float>(samples, new[] { 1, samples.Length });
        var srTensor = new DenseTensor<long>(new long[] { _sampleRate }, new[] { 1 });

        // We  pass the current state to the tensor
        var stateTensor = new DenseTensor<float>(_state, new[] { 2, 1, 128 });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor),
            NamedOnnxValue.CreateFromTensor("sr", srTensor),
            NamedOnnxValue.CreateFromTensor("state", stateTensor)
        };

        using var results = _session.Run(inputs);

        // 1. We obtain the speech probability (index 0)
        var outputTensor = results.ElementAt(0).AsTensor<float>();
        float probability = outputTensor.First();

        // 2. We recive the updated network status (index 1)
        var newStateTensor = results.ElementAt(1).AsTensor<float>();

        // We use Onnx Runtime's native array copying
        float[] updatedState = newStateTensor.ToArray();
        Buffer.BlockCopy(updatedState, 0, _state, 0, _state.Length * sizeof(float));

        return probability;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}