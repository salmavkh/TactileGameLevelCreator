using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [Header("Scene refs")]
    public Transform platformRoot;
    public string groundPiecesName = "GroundPieces";
    public Transform itemRoot;

    [Header("Placement")]
    public float yOffset = 0.22f;
    public float minEdgeWidthWorld = 0.5f;

    [Tooltip("Small jitter along platform (0 = none).")]
    public float alongEdgeJitter = 0.06f;

    IEnumerator Start()
    {
        // Wait until PlayBuilder created GroundPieces + pieces
        Transform piecesParent = null;
        while (piecesParent == null || piecesParent.childCount == 0)
        {
            if (platformRoot != null)
                piecesParent = platformRoot.Find(groundPiecesName);

            yield return null;
        }

        // Wait until selected item prefab is ready
        while (PlayCustomizationApplier.SelectedItemPrefab == null)
            yield return null;

        SpawnFromPlan();
    }

    void SpawnFromPlan()
    {
        var prefab = PlayCustomizationApplier.SelectedItemPrefab;
        if (prefab == null) return;

        var plan = SessionManager.PlannedItemPositions;
        if (plan == null || plan.Count == 0)
        {
            Debug.LogWarning("ItemSpawner: No planned item positions.");
            return;
        }

        foreach (var p in plan)
        {
            Vector3 world = platformRoot.TransformPoint(p); // p is platform-local
            Instantiate(prefab, world, Quaternion.identity, itemRoot);
        }

        SessionManager.TargetCollectCount = plan.Count;
    }

    List<EdgeCollider2D> CollectEligibleEdges(Transform piecesParent)
    {
        var edges = new List<EdgeCollider2D>();
        for (int i = 0; i < piecesParent.childCount; i++)
        {
            var piece = piecesParent.GetChild(i);
            var edge = piece.GetComponent<EdgeCollider2D>();
            if (edge == null || edge.points == null || edge.points.Length < 2) continue;

            if (GetEdgeWidthWorld(edge) < minEdgeWidthWorld) continue;
            edges.Add(edge);
        }
        return edges;
    }

    // --- Always on top of platform: pick X within [minX,maxX], compute Y on polyline ---
    Vector3 SamplePointOnEdgeWorld(EdgeCollider2D edge, System.Random rng, int slot)
    {
        var pts = edge.points;

        // Convert to world points
        var wpts = new Vector3[pts.Length];
        for (int i = 0; i < pts.Length; i++)
            wpts[i] = edge.transform.TransformPoint(pts[i]);

        // Sort by X so we treat edge as left->right polyline
        Array.Sort(wpts, (p1, p2) => p1.x.CompareTo(p2.x));

        float minX = wpts[0].x;
        float maxX = wpts[wpts.Length - 1].x;

        // Spread multiple items across the same platform by slot
        float baseT = 0.5f;
        if (slot == 1) baseT = 0.33f;
        else if (slot == 2) baseT = 0.66f;
        else if (slot >= 3) baseT = (float)rng.NextDouble();

        float jitter = (alongEdgeJitter > 0f)
            ? (float)(rng.NextDouble() * (2f * alongEdgeJitter) - alongEdgeJitter)
            : 0f;

        float t = Mathf.Clamp01(baseT + jitter);
        float x = Mathf.Lerp(minX, maxX, t);

        // Find segment containing x and interpolate y
        for (int i = 0; i < wpts.Length - 1; i++)
        {
            Vector3 a = wpts[i];
            Vector3 b = wpts[i + 1];

            float lo = Mathf.Min(a.x, b.x);
            float hi = Mathf.Max(a.x, b.x);

            if (x >= lo - 1e-4f && x <= hi + 1e-4f && Mathf.Abs(b.x - a.x) > 1e-6f)
            {
                float u = (x - a.x) / (b.x - a.x);
                float y = Mathf.Lerp(a.y, b.y, u);
                return new Vector3(x, y, 0f);
            }
        }

        // Fallback midpoint
        return (wpts[0] + wpts[wpts.Length - 1]) * 0.5f;
    }

    float GetEdgeWidthWorld(EdgeCollider2D edge)
    {
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        foreach (var lp in edge.points)
        {
            var w = edge.transform.TransformPoint(lp);
            minX = Mathf.Min(minX, w.x);
            maxX = Mathf.Max(maxX, w.x);
        }
        return maxX - minX;
    }
}
