using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    [Header("Scene refs")]
    public Transform platformRoot;
    public string groundPiecesName = "GroundPieces";
    public Transform obstacleRoot;

    [Header("Obstacle prefab (assign in Inspector)")]
    public GameObject obstaclePrefab;

    [Header("Placement")]
    public float yOffset = 0.22f;
    public float minEdgeWidthWorld = 0.5f;

    [Tooltip("Obstacles only on platforms whose top Y is within this of the maximum top Y.")]
    public float topPlatformEpsilon = 0.25f;

    [Tooltip("Small jitter along platform (0 = none).")]
    public float alongEdgeJitter = 0.06f;

    [Header("Optional: keep obstacles apart")]
    public float minSeparation = 0.55f;

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

        if (obstaclePrefab == null)
        {
            Debug.LogWarning("ObstacleSpawner: obstaclePrefab is null (assign one).");
            yield break;
        }

        SpawnFromPlan();
    }

    void SpawnFromPlan()
    {
        if (obstaclePrefab == null) return;

        var plan = SessionManager.PlannedObstaclePositions;
        if (plan == null || plan.Count == 0)
        {
            Debug.LogWarning("ObstacleSpawner: No planned obstacle positions found.");
            return;
        }

        int spawned = 0;
        foreach (var p in plan)
        {
            Vector3 world = platformRoot.TransformPoint(p); // p is platform-local
            world.y += yOffset;
            // Vector3 world = platformRoot.TransformPoint(p);
            // world.y = platformRoot.position.y + p.y + yOffset;
            var go = Instantiate(obstaclePrefab, world, Quaternion.identity, obstacleRoot);
            spawned++;

            var sr = go.GetComponentInChildren<SpriteRenderer>();
            sr.gameObject.tag = "Obstacle";
            if (sr != null)
            {
                sr.sortingLayerName = "Obstacles";
                sr.sortingOrder = -1;
            }

        }

        Debug.Log($"ObstacleSpawner: Spawned {spawned} obstacles from Customize plan.");
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

        // Sort by X (left->right)
        Array.Sort(wpts, (p1, p2) => p1.x.CompareTo(p2.x));

        float minX = wpts[0].x;
        float maxX = wpts[wpts.Length - 1].x;

        // Spread by slot
        float baseT = 0.5f;
        if (slot == 1) baseT = 0.33f;
        else if (slot == 2) baseT = 0.66f;
        else if (slot >= 3) baseT = (float)rng.NextDouble();

        float jitter = (alongEdgeJitter > 0f)
            ? (float)(rng.NextDouble() * (2f * alongEdgeJitter) - alongEdgeJitter)
            : 0f;

        float t = Mathf.Clamp01(baseT + jitter);
        float x = Mathf.Lerp(minX, maxX, t);

        // Interpolate y at x
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

        return (wpts[0] + wpts[wpts.Length - 1]) * 0.5f;
    }

    static List<EdgeCollider2D> GetTopEdges(List<EdgeCollider2D> edges, float eps)
    {
        float maxY = float.NegativeInfinity;
        var yByEdge = new Dictionary<EdgeCollider2D, float>(edges.Count);

        foreach (var e in edges)
        {
            float y = MaxYOnEdgeWorld(e);
            yByEdge[e] = y;
            if (y > maxY) maxY = y;
        }

        var top = new List<EdgeCollider2D>();
        foreach (var kv in yByEdge)
        {
            if (kv.Value >= maxY - eps)
                top.Add(kv.Key);
        }
        return top;
    }

    static float MaxYOnEdgeWorld(EdgeCollider2D edge)
    {
        float maxY = float.NegativeInfinity;
        foreach (var lp in edge.points)
        {
            var w = edge.transform.TransformPoint(lp);
            if (w.y > maxY) maxY = w.y;
        }
        return maxY;
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

    static bool IsTooClose(Vector3 p, List<Vector3> occupied, float minSep)
    {
        float minSep2 = minSep * minSep;
        for (int i = 0; i < occupied.Count; i++)
        {
            if ((occupied[i] - p).sqrMagnitude < minSep2)
                return true;
        }
        return false;
    }
}
