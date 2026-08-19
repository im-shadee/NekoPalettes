namespace NekoPalettes.Internal
{
    internal static class NekoPaletteDebug
    {
        public static void Log(string message)
        {
            if (PackageConfig.ENABLE_LOGS)
                UnityEngine.Debug.Log($"[NekoPalette] {message}");
        }

        public static void LogWarning(string message)
        {
            if (PackageConfig.ENABLE_LOGS)
                UnityEngine.Debug.LogWarning($"[NekoPalette] {message}");
        }

        // Shade: Keep errors visible everywhere so critical issues are never hidden
        public static void LogError(string message)
        {
            UnityEngine.Debug.LogError($"[NekoPalette] {message}");
        }
    }
}
