using System;
using System.Collections.Generic;
using UnityEngine;

public class CustomizePreviewSpawner : MonoBehaviour
{
    // ============================================================
    // TUNING KNOBS (edit in Inspector)
    // ============================================================

    [Header("Offsets (world units)")]
    [Tooltip("How far above the platform edge to place spawned objects.")]
    public float yOffset = 0.22f;

    [Header("Platform filtering")]
    [Tooltip("Skip very tiny platforms (slivers). Objects won't be placed on edges narrower than this.")]
    public float minEdgeWidthWorld = 0.5f;

    [Header("Separation / overlap avoidance")]
    [Tooltip("Minimum distance between an item and an obstacle (bigger = fewer overlaps).")]
    public float minSeparation = 0.55f;

    [Tooltip("How far left/right to try placing an item beside an obstacle.")]
    public float sideShift = 0.75f;

    [Tooltip("If item can't fit beside an obstacle, place it this much higher above the obstacle.")]
    public float floatAboveOffset = 0.70f;

    [Header("Obstacle placement rules")]
    [Tooltip("Obstacles are only spawned on platforms whose top Y is within this epsilon of the max top Y.")]
    public float topPlatformEpsilon = 0.25f;

    [Tooltip("Don't spawn obstacles too close to the player spawn point.")]
    public float avoidPlayerSpawnRadius = 0.8f;

    [Header("Optional spread jitter")]
    [Tooltip("Small random jitter on placement along the edge (0 = no jitter).")]
    public float alongEdgeJitter = 0.06f;

    // ============================================================
    // SCENE REFERENCES
    // ============================================================

    [Header("Platform Pieces Source")]
    [Tooltip("Drag your Platform transform here (the one that contains GroundPieces).")]
    public Transform platformRoot;

    [Tooltip("Child name under Platform where PlayBuilder creates platform pieces.")]
    public string groundPiecesName = "GroundPieces";

    [Header("Preview Parents (empty transforms)")]
    public Transform previewItemRoot;
    public Transform previewObstacleRoot;

    [Tooltip("Optional: a small marker transform to show player start position.")]
    public Transform previewPlayerMarker;

    [Header("Prefabs (project assets, not scene objects)")]
    [Tooltip("3 item prefabs (matches ItemIndex 0..2).")]
    public GameObject[] itemPrefabs; // size 3

    [Tooltip("Single obstacle prefab for now.")]
    public GameObject obstaclePrefab;

    // ============================================================
    // Public entry point
    // ============================================================

    public void RegeneratePreview()
    {
        Transform piecesParent = (platformRoot != null) ? platformRoot.Find(groundPiecesName) : null;
        if (piecesParent == null || piecesParent.childCount == 0)
        {
            Debug.LogWarning("CustomizePreviewSpawner: GroundPieces not ready yet (did CustomizeBuilder run?).");
            return;
        }

        ClearChildren(previewItemRoot);
        ClearChildren(previewObstacleRoot);

        // Collect eligible edges
        var edges = new List<EdgeCollider2D>();
        for (int i = 0; i < piecesParent.childCount; i++)
        {
            var edge = piecesParent.GetChild(i).GetComponent<EdgeCollider2D>();
            if (edge == null || edge.points == null || edge.points.Length < 2) continue;
            if (GetEdgeWidthWorld(edge) < minEdgeWidthWorld) continue;
            edges.Add(edge);
        }

        if (edges.Count == 0)
        {
            Debug.LogWarning("CustomizePreviewSpawner: No eligible platform edges (all too small?).");
            return;
        }

        // Deterministic RNG so Customize preview can match Play if you reuse this same seed there.
        var rng = new System.Random(SessionManager.RandomSeed);

        // Player spawn preview = bottom-left point across all edges
        Vector3 playerSpawn = FindBottomLeftPoint(edges);
        if (previewPlayerMarker != null)
            previewPlayerMarker.position = playerSpawn + new Vector3(0f, yOffset, 0f);

        // Obstacles only on top platforms
        List<EdgeCollider2D> topEdges = GetTopEdges(edges, topPlatformEpsilon);
        if (topEdges.Count == 0) topEdges = edges;

        // Track occupied positions (start with obstacles)
        var occupied = new List<Vector3>();
        var finalObstaclePositions = new List<Vector3>();
        var finalItemPositions = new List<Vector3>();

        // ------------------------
        // 1) Spawn obstacles FIRST
        // ------------------------
        int numObs = Mathf.Max(0, SessionManager.NumObstacles);
        if (numObs > 0 && obstaclePrefab == null)
        {
            Debug.LogWarning("CustomizePreviewSpawner: obstaclePrefab is null (assign one).");
        }
        else if (numObs > 0)
        {
            var obsPlan = ComputeRoundRobinPlan(
                topEdges,
                numObs,
                rng,
                yOffset,
                playerSpawn,
                avoidPlayerSpawn: true
            );

            for (int i = 0; i < obsPlan.Count; i++)
            {
                Vector3 p = obsPlan[i].pos;

                // Optional: keep obstacles from stacking on each other too
                if (IsTooClose(p, occupied, minSeparation))
                {
                    // try slight jitter
                    if (!TryJitterAway(ref p, rng, occupied, minSeparation, tries: 10))
                        continue;
                }

                var go = Instantiate(obstaclePrefab, p, Quaternion.identity, previewObstacleRoot);
                DisablePhysics(go);
                occupied.Add(p);
                finalObstaclePositions.Add(platformRoot.InverseTransformPoint(p));
            }
        }

        // ------------------------
        // 2) Spawn items SECOND (avoid obstacles)
        // ------------------------
        int numItems = Mathf.Max(0, SessionManager.NumItems);
        var itemPrefab = GetSelectedItemPrefab();
        if (numItems > 0 && itemPrefab == null)
        {
            Debug.LogWarning("CustomizePreviewSpawner: Selected item prefab is null (assign itemPrefabs and ItemIndex).");
            return;
        }
        else if (numItems > 0)
        {
            var itemPlan = ComputeRoundRobinPlan(
                edges,
                numItems,
                rng,
                yOffset,
                playerSpawn,
                avoidPlayerSpawn: false
            );

            for (int i = 0; i < itemPlan.Count; i++)
            {
                EdgeCollider2D edge = itemPlan[i].edge;
                Vector3 p = itemPlan[i].pos;

                if (IsTooClose(p, occupied, minSeparation))
                {
                    // platform bounds (so we can decide if "beside" fits)
                    GetEdgeBoundsWorld(edge, out float minX, out float maxX);

                    // nearest obstacle position
                    Vector3 nearest = Nearest(p, occupied);

                    // Try beside (left then right) ONLY if it stays within platform span.
                    Vector3 left = new Vector3(nearest.x - sideShift, p.y, p.z);
                    bool leftFits = left.x >= minX && left.x <= maxX && !IsTooClose(left, occupied, minSeparation);

                    Vector3 right = new Vector3(nearest.x + sideShift, p.y, p.z);
                    bool rightFits = right.x >= minX && right.x <= maxX && !IsTooClose(right, occupied, minSeparation);

                    if (leftFits) p = left;
                    else if (rightFits) p = right;
                    else
                    {
                        // Platform too small (or blocked) => place ABOVE obstacle
                        Vector3 above = new Vector3(nearest.x, nearest.y + floatAboveOffset, p.z);

                        // If still blocked (rare), jitter a bit or skip
                        if (!IsTooClose(above, occupied, minSeparation))
                        {
                            p = above;
                        }
                        else
                        {
                            if (!TryJitterAway(ref above, rng, occupied, minSeparation, tries: 12))
                                continue;
                            p = above;
                        }
                    }
                }

                var go = Instantiate(itemPrefab, p, Quaternion.identity, previewItemRoot);
                DisablePhysics(go);
                finalItemPositions.Add(platformRoot.InverseTransformPoint(p));

                // Optional: if you want items to not overlap each other too, uncomment:
                // occupied.Add(p);
            }
        }

        Debug.Log(
            $"CustomizePreviewSpawner: items={SessionManager.NumItems}, obstacles={SessionManager.NumObstacles}, " +
            $"seed={SessionManager.RandomSeed}, edges={edges.Count}, topEdges={topEdges.Count}"
        );

        SessionManager.PlannedObstaclePositions = finalObstaclePositions;
        SessionManager.PlannedItemPositions = finalItemPositions;
        SessionManager.TargetCollectCount = finalItemPositions.Count;

    }

    // ============================================================
    // Prefab selection
    // ============================================================

    GameObject GetSelectedItemPrefab()
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0) return null;
        int idx = Mathf.Clamp(SessionManager.ItemIndex, 0, itemPrefabs.Length - 1);
        return itemPrefabs[idx];
    }

    // ============================================================
    // Round-robin planning (fill platforms first)
    // ============================================================

    struct SpawnPlan
    {
        public EdgeCollider2D edge;
        public Vector3 pos;
        public SpawnPlan(EdgeCollider2D e, Vector3 p) { edge = e; pos = p; }
    }

    List<SpawnPlan> ComputeRoundRobinPlan(
        List<EdgeCollider2D> edges,
        int count,
        System.Random rng,
        float yOffsetWorld,
        Vector3 playerSpawn,
        bool avoidPlayerSpawn
    )
    {
        var results = new List<SpawnPlan>(count);
        if (count <= 0) return results;

        int placed = 0;
        int safety = 0;

        // track slot per edge so multiple objects on same platform spread out
        var slotPerEdge = new Dictionary<EdgeCollider2D, int>(edges.Count);
        foreach (var e in edges) slotPerEdge[e] = 0;

        while (placed < count && safety < count * 60)
        {
            safety++;

            var edge = edges[placed % edges.Count];

            if (avoidPlayerSpawn && edges.Count > 1)
            {
                if (Vector3.Distance(EdgeMidpoint(edge), playerSpawn) < avoidPlayerSpawnRadius)
                    continue;
            }

            int slot = slotPerEdge[edge];
            slotPerEdge[edge] = slot + 1;

            Vector3 p = SamplePointOnEdgeWorld(edge, rng, slot);
            p.y += yOffsetWorld;

            results.Add(new SpawnPlan(edge, p));
            placed++;
        }

        return results;
    }

    // ============================================================
    // Sampling & platform measurements
    // ============================================================

    Vector3 SamplePointOnEdgeWorld(EdgeCollider2D edge, System.Random rng, int slot)
    {
        // Get world points
        var pts = edge.points;
        var wpts = new Vector3[pts.Length];
        for (int i = 0; i < pts.Length; i++)
            wpts[i] = edge.transform.TransformPoint(pts[i]);

        // Sort by X so we can treat it as a left->right polyline
        Array.Sort(wpts, (p1, p2) => p1.x.CompareTo(p2.x));

        float minX = wpts[0].x;
        float maxX = wpts[wpts.Length - 1].x;

        // Choose an X (spread across platform by slot)
        float baseT = 0.5f;
        if (slot == 1) baseT = 0.33f;
        else if (slot == 2) baseT = 0.66f;
        else if (slot >= 3) baseT = (float)rng.NextDouble();

        float jitter = (alongEdgeJitter > 0f)
            ? (float)(rng.NextDouble() * (2f * alongEdgeJitter) - alongEdgeJitter)
            : 0f;

        float t = Mathf.Clamp01(baseT + jitter);
        float x = Mathf.Lerp(minX, maxX, t);

        // Find segment that contains x and interpolate y
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

        // Fallback: midpoint
        return (wpts[0] + wpts[wpts.Length - 1]) * 0.5f;
    }

    static float GetEdgeWidthWorld(EdgeCollider2D edge)
    {
        GetEdgeBoundsWorld(edge, out float minX, out float maxX);
        return maxX - minX;
    }

    static void GetEdgeBoundsWorld(EdgeCollider2D edge, out float minX, out float maxX)
    {
        minX = float.PositiveInfinity;
        maxX = float.NegativeInfinity;

        foreach (var lp in edge.points)
        {
            var w = edge.transform.TransformPoint(lp);
            minX = Mathf.Min(minX, w.x);
            maxX = Mathf.Max(maxX, w.x);
        }
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

    static Vector3 EdgeMidpoint(EdgeCollider2D edge)
    {
        var pts = edge.points;
        Vector3 a = edge.transform.TransformPoint(pts[0]);
        Vector3 b = edge.transform.TransformPoint(pts[pts.Length - 1]);
        return (a + b) * 0.5f;
    }

    static Vector3 FindBottomLeftPoint(List<EdgeCollider2D> edges)
    {
        Vector3 best = Vector3.zero;
        bool has = false;

        foreach (var edge in edges)
        {
            foreach (var lp in edge.points)
            {
                Vector3 w = edge.transform.TransformPoint(lp);
                if (!has) { best = w; has = true; }
                else
                {
                    // bottom-first, then left
                    if (w.y < best.y - 1e-4f || (Mathf.Abs(w.y - best.y) < 1e-4f && w.x < best.x))
                        best = w;
                }
            }
        }
        return best;
    }

    // ============================================================
    // Overlap helpers
    // ============================================================

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

    static Vector3 Nearest(Vector3 p, List<Vector3> occupied)
    {
        if (occupied.Count == 0) return p;

        float best = float.PositiveInfinity;
        Vector3 bestP = occupied[0];

        for (int i = 0; i < occupied.Count; i++)
        {
            float d = (occupied[i] - p).sqrMagnitude;
            if (d < best) { best = d; bestP = occupied[i]; }
        }
        return bestP;
    }

    bool TryJitterAway(ref Vector3 p, System.Random rng, List<Vector3> occupied, float minSep, int tries)
    {
        Vector3 baseP = p;
        for (int i = 0; i < tries; i++)
        {
            float dx = (float)(rng.NextDouble() * 0.8 - 0.4);
            float dy = 0f;
            p = baseP + new Vector3(dx, dy, 0f);
            if (!IsTooClose(p, occupied, minSep))
                return true;
        }
        p = baseP;
        return false;
    }

    // ============================================================
    // Utility
    // ============================================================

    static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    static void DisablePhysics(GameObject go)
    {
        if (go == null) return;

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null) Destroy(rb);

        var cols = go.GetComponentsInChildren<Collider2D>();
        foreach (var c in cols) c.enabled = false;
    }
}

