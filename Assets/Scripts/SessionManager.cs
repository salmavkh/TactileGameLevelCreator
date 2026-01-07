public static class SessionManager
{
    // From Capture -> used by Play to build the level
    public static string RunDir;

    // From Customize -> used by Play to apply cosmetics + item type
    public static int BackgroundIndex = 0;  // 0..2
    public static int CharacterIndex = 0;   // 0..2
    public static int ItemIndex = 0;        // 0..2

    // Gameplay goal
    public static int TargetCollectCount = 10;
}
