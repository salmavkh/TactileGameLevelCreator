using System.Collections.Generic;
using UnityEngine;

public static class SessionManager
{
    public static string RunDir;

    public static int BackgroundIndex = 0;
    public static int CharacterIndex = 0;
    public static int ItemIndex = 0;

    public static int NumItems = 10;
    public static int NumObstacles = 3;
    public static float TimerSeconds = 60f;
    public static int MaxHearts = 3;

    public static int CurrentHearts;
    public static int RandomSeed = 0;

    public static int TargetCollectCount = 10;

    // NEW: Planned placements from Customize preview -> used by Play
    public static List<Vector3> PlannedItemPositions = new List<Vector3>();
    public static List<Vector3> PlannedObstaclePositions = new List<Vector3>();
}
