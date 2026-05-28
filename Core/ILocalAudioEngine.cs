using System;
using System.Collections.Generic;
using NDIIntercom.Models;

namespace NDIIntercom.Core
{
    /// <summary>Local capture/playback used by <see cref="IntercomEngine"/> (WASAPI on Windows, ALSA/Pulse/PipeWire later on Linux).</summary>
    public interface ILocalAudioEngine : IDisposable
    {
        event EventHandler<float> MicrophoneLevelUpdated;
        event EventHandler<byte[]> AudioCaptured;

        List<AudioDeviceInfo> GetInputDevices();
        List<AudioDeviceInfo> GetOutputDevices();
        void SelectMicrophone(string deviceId);
        void SelectSpeaker(string deviceId);
        void StartCapture();
        void StopCapture();
        void StartPlayback();
        void StopPlayback();
        void AddToOutputBuffer(byte[] audioData);
        void ConfigureNoiseGate(bool enabled, float thresholdDb, float attackMs, float releaseMs, float holdMs);
    }
}
