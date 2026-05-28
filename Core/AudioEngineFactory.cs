namespace NDIIntercom.Core
{
    internal static class AudioEngineFactory
    {
        /// <summary>Windows and Linux each compile their own <see cref="AudioEngine"/> type from a different source file.</summary>
        public static ILocalAudioEngine CreateDefault() => new AudioEngine();
    }
}
