namespace ValheimCompanion
{
    internal enum CompanionMode
    {
        Stopped,
        Fight,
        Gather
    }

    internal static class CompanionModeState
    {
        public static CompanionMode Current { get; private set; } = CompanionMode.Stopped;

        public static void Set(CompanionMode mode)
        {
            Current = mode;
        }
    }
}
