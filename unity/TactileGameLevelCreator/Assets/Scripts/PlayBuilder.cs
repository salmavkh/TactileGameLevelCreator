using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class PlayBuilder : MonoBehaviour
{
    [Header("Scene Objects")]
    public string platformObjectName = "Platform";
    public string playerObjectName = "Player";

    [Header("Scaling")]
    public float pixelsPerUnit = 100f;

    [Header("Top Edge Platform Settings")]
    [Tooltip("Layer name for one-way platforms.")]
    public string oneWayLayerName = "OneWay";

    [Tooltip("How many samples across X to build the top silhouette. Higher = more accurate, but heavier.")]
    [Range(16, 256)]
    public int topEdgeSamples = 96;

    [Tooltip("Simplify the sampled top edge (in pixels). Increase if edge is noisy/jittery.")]
    [Range(0f, 10f)]
    public float simplifyEpsilonPixels = 2f;

    [Tooltip("Minimum vertical thickness of the platform collider (world units). Helps reduce falling through.")]
    public float edgeThicknessWorld = 0.05f;

    [Header("Filtering (adaptive)")]
    public bool enableAreaFilter = true;

    [Tooltip("Minimum polygon area as a fraction of the whole image area. Example: 0.005 = 0.5%")]
    [Range(0f, 0.5f)]
    public float minAreaFrac = 0.005f;

    [Header("Debug")]
    public bool logPaths = true;
    public bool listRunDirFiles = true;
    public bool logEachPolygon = true;

    [Tooltip("If true, prints why a polygon was skipped (area too small, silhouette failed, etc.).")]
    public bool logSkipReasons = true;

    [Tooltip("If true, prints a short summary of skip counts at the end.")]
    public bool logSummary = true;

    void Start()
    {
        GameObject platform = GameObject.Find(platformObjectName);
        GameObject player = GameObject.Find(playerObjectName);

        if (platform == null) { Debug.LogError($"Missing '{platformObjectName}' in scene."); return; }
        if (player == null) { Debug.LogError($"Missing '{playerObjectName}' in scene."); return; }

        if (string.IsNullOrWhiteSpace(SessionManager.RunDir))
        {
            Debug.LogError("SessionManager.RunDir is empty. Run Capture -> Continue first.");
            return;
        }

        string runDir = SessionManager.RunDir;

        string pngPath = Path.Combine(runDir, "objects_only_rgba.png");
        string jsonPath = Path.Combine(runDir, "objects_contour.json");

        if (logPaths)
        {
            Debug.Log("PlayBuilder runDir: " + runDir);
            Debug.Log("PNG:  " + pngPath);
            Debug.Log("JSON: " + jsonPath);
        }

        if (listRunDirFiles && Directory.Exists(runDir))
        {
            Debug.Log("Files in runDir:");
            foreach (var f in Directory.GetFiles(runDir))
                Debug.Log(" - " + Path.GetFileName(f));
        }

        if (!File.Exists(pngPath) || !File.Exists(jsonPath))
        {
            Debug.LogError("Missing outputs in runDir. Need objects_only_rgba.png and objects_contour.json");
            return;
        }

        // ---- Platform visual ----
        var sr = platform.GetComponent<SpriteRenderer>();
        if (sr == null) { Debug.LogError("Platform missing SpriteRenderer."); return; }
        sr.sprite = LoadSpriteFromPng(pngPath, pixelsPerUnit);

        // disable any collider sitting on Platform itself
        var polyOnPlatform = platform.GetComponent<PolygonCollider2D>();
        if (polyOnPlatform != null) polyOnPlatform.enabled = false;
        var boxOnPlatform = platform.GetComponent<BoxCollider2D>();
        if (boxOnPlatform != null) boxOnPlatform.enabled = false;
        var edgeOnPlatform = platform.GetComponent<EdgeCollider2D>();
        if (edgeOnPlatform != null) edgeOnPlatform.enabled = false;

        // ---- GroundPieces parent ----
        Transform piecesParent = platform.transform.Find("GroundPieces");
        if (piecesParent == null)
        {
            var go = new GameObject("GroundPieces");
            go.transform.SetParent(platform.transform, false);
            piecesParent = go.transform;
        }

        // clear previous
        for (int i = piecesParent.childCount - 1; i >= 0; i--)
            Destroy(piecesParent.GetChild(i).gameObject);

        // ---- parse JSON ----
        string json = File.ReadAllText(jsonPath);

        if (!TryParseImageWH(json, out int imgW, out int imgH))
        {
            Debug.LogError("Failed to parse image_w/image_h from JSON.");
            return;
        }

        if (!TryParseAllOuters(json, out List<List<Vector2>> outersPx))
        {
            Debug.LogError("Failed to parse polygons[].outer from JSON.");
            return;
        }

        Debug.Log($"Parsed polygons: {outersPx.Count} | image: {imgW}x{imgH}");

        float imageArea = imgW * imgH;
        float minAreaPx = imageArea * Mathf.Max(0f, minAreaFrac);
        Debug.Log($"Area filter: enable={enableAreaFilter} minAreaFrac={minAreaFrac} => minAreaPx={minAreaPx:0}");

        int oneWayLayer = LayerMask.NameToLayer(oneWayLayerName);
        if (oneWayLayer == -1)
            Debug.LogWarning($"No layer named '{oneWayLayerName}'. Create it (Layer dropdown) for one-way platforms.");

        int spawned = 0;

        // Debug counters
        int skippedTooFewPoints = 0;
        int skippedArea = 0;
        int skippedSilhouetteNullOrShort = 0;
        int skippedAfterCleanup = 0;

        for (int idx = 0; idx < outersPx.Count; idx++)
        {
            var outer = outersPx[idx];

            if (outer == null || outer.Count < 3)
            {
                skippedTooFewPoints++;
                if (logSkipReasons) Debug.Log($"SKIP Polygon {idx}: outer is null or <3 points.");
                continue;
            }

            float area = Mathf.Abs(PolygonAreaPx(outer));
            if (logEachPolygon)
                Debug.Log($"Polygon {idx}: points={outer.Count}, area(px^2)≈{area:0}");

            if (enableAreaFilter && area < minAreaPx)
            {
                skippedArea++;
                if (logSkipReasons)
                    Debug.Log($"SKIP Polygon {idx}: area too small ({area:0} < {minAreaPx:0}). Lower minAreaFrac or disableAreaFilter.");
                continue;
            }

            // Build TOP silhouette in pixel space
            List<Vector2> topPx = BuildTopSilhouettePx(outer, topEdgeSamples);
            if (topPx == null || topPx.Count < 2)
            {
                skippedSilhouetteNullOrShort++;
                if (logSkipReasons)
                    Debug.Log($"SKIP Polygon {idx}: BuildTopSilhouettePx returned {(topPx == null ? "null" : $"only {topPx.Count} pts")}. Try increasing topEdgeSamples or check polygon shape.");
                continue;
            }

            int beforeCleanup = topPx.Count;

            // Simplify to reduce jitter
            topPx = RemoveNearDuplicates(topPx, 0.5f);

            if (simplifyEpsilonPixels > 0f)
                topPx = DouglasPeuckerOpen(topPx, simplifyEpsilonPixels);

            if (topPx == null || topPx.Count < 2)
            {
                skippedAfterCleanup++;
                if (logSkipReasons)
                    Debug.Log($"SKIP Polygon {idx}: after cleanup/simplify, top edge <2 points. (before={beforeCleanup}) Try reduce simplifyEpsilonPixels.");
                continue;
            }

            if (logSkipReasons)
                Debug.Log($"PASS Polygon {idx}: topEdge pts before={beforeCleanup} after={topPx.Count}");

            // Convert to local points
            var localPts = new Vector2[topPx.Count];
            for (int i = 0; i < topPx.Count; i++)
                localPts[i] = PxToLocal(topPx[i].x, topPx[i].y, imgW, imgH, pixelsPerUnit);

            // Spawn piece at origin (points are platform-local space)
            GameObject piece = new GameObject($"Piece_{spawned}");
            piece.transform.SetParent(piecesParent, false);
            piece.transform.localPosition = Vector3.zero;
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = Vector3.one;

            if (oneWayLayer != -1) piece.layer = oneWayLayer;

            // One-way setup: EdgeCollider2D + PlatformEffector2D
            var edge = piece.AddComponent<EdgeCollider2D>();
            edge.isTrigger = false;
            edge.edgeRadius = Mathf.Max(0f, edgeThicknessWorld * 0.5f);
            edge.points = localPts;
            edge.usedByEffector = true;

            var eff = piece.AddComponent<PlatformEffector2D>();
            eff.useOneWay = true;
            eff.useOneWayGrouping = false;
            eff.surfaceArc = 180f;
            eff.sideArc = 0f;

            // Static RB (effectors behave best with a rigidbody present)
            var rb = piece.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            rb.simulated = true;

            spawned++;
        }

        Debug.Log($"Spawned one-way top-edge pieces: {spawned}");
        if (spawned == 0)
            Debug.LogWarning("Spawned 0 pieces. Try: enableAreaFilter=false OR reduce minAreaFrac.");

        if (logSummary)
        {
            Debug.Log(
                "---- PlayBuilder Summary ----\n" +
                $"Total polygons parsed: {outersPx.Count}\n" +
                $"Spawned: {spawned}\n" +
                $"Skipped (<3 pts): {skippedTooFewPoints}\n" +
                $"Skipped (area filter): {skippedArea}\n" +
                $"Skipped (silhouette fail): {skippedSilhouetteNullOrShort}\n" +
                $"Skipped (after cleanup): {skippedAfterCleanup}\n" +
                "----------------------------"
            );
        }
    }

    // ---------------- TOP SILHOUETTE (pixel space) ----------------
    static List<Vector2> BuildTopSilhouettePx(List<Vector2> poly, int samples)
    {
        if (poly == null || poly.Count < 3) return null;

        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        for (int i = 0; i < poly.Count; i++)
        {
            if (poly[i].x < minX) minX = poly[i].x;
            if (poly[i].x > maxX) maxX = poly[i].x;
        }

        if (maxX - minX < 1f) return null;

        var outPts = new List<Vector2>(samples);

        for (int s = 0; s < samples; s++)
        {
            float t = (samples == 1) ? 0.5f : (float)s / (samples - 1);
            float x = Mathf.Lerp(minX, maxX, t);

            bool found = false;
            float bestY = float.PositiveInfinity;

            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Count];

                float dx = b.x - a.x;
                if (Mathf.Abs(dx) < 1e-6f) continue;

                float lo = Mathf.Min(a.x, b.x);
                float hi = Mathf.Max(a.x, b.x);

                if (x < lo - 1e-3f || x > hi + 1e-3f) continue;

                float u = (x - a.x) / dx;
                if (u < 0f || u > 1f) continue;

                float y = a.y + u * (b.y - a.y);

                if (y < bestY)
                {
                    bestY = y;
                    found = true;
                }
            }

            if (found)
                outPts.Add(new Vector2(x, bestY));
        }

        if (outPts.Count < 2) return null;
        return outPts;
    }

    // ---------------- Pixel -> Unity local ----------------
    static Vector2 PxToLocal(float xPx, float yPx, int imgW, int imgH, float ppu)
    {
        float localX = (xPx - imgW * 0.5f) / ppu;
        float localY = -(yPx - imgH * 0.5f) / ppu;
        return new Vector2(localX, localY);
    }

    // ---------------- Sprite load ----------------
    Sprite LoadSpriteFromPng(string path, float ppu)
    {
        byte[] data = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(data);
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
    }

    // ---------------- Geometry helpers ----------------
    static float PolygonAreaPx(List<Vector2> pts)
    {
        double sum = 0.0;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[(i + 1) % pts.Count];
            sum += (double)a.x * b.y - (double)b.x * a.y;
        }
        return (float)(0.5 * sum);
    }

    static List<Vector2> RemoveNearDuplicates(List<Vector2> pts, float minDistPx)
    {
        if (pts.Count <= 2) return pts;

        var outPts = new List<Vector2>(pts.Count);
        float minDist2 = minDistPx * minDistPx;

        Vector2 prev = pts[0];
        outPts.Add(prev);

        for (int i = 1; i < pts.Count; i++)
        {
            Vector2 cur = pts[i];
            if ((cur - prev).sqrMagnitude >= minDist2)
            {
                outPts.Add(cur);
                prev = cur;
            }
        }

        if (outPts.Count >= 2 &&
            (outPts[outPts.Count - 1] - outPts[outPts.Count - 2]).sqrMagnitude < minDist2)
            outPts.RemoveAt(outPts.Count - 1);

        return outPts;
    }

    static List<Vector2> DouglasPeuckerOpen(List<Vector2> pts, float epsilon)
    {
        if (pts == null || pts.Count < 3) return pts;

        int n = pts.Count;
        var marked = new bool[n];
        marked[0] = true;
        marked[n - 1] = true;

        DPMarkOpen(pts, 0, n - 1, epsilon, marked);

        var result = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
            if (marked[i]) result.Add(pts[i]);

        return result.Count >= 2 ? result : pts;
    }

    static void DPMarkOpen(List<Vector2> pts, int i0, int i1, float eps, bool[] marked)
    {
        if (i1 <= i0 + 1) return;

        Vector2 a = pts[i0];
        Vector2 b = pts[i1];

        float maxDist = -1f;
        int index = -1;

        for (int i = i0 + 1; i < i1; i++)
        {
            float d = PerpDistance(pts[i], a, b);
            if (d > maxDist)
            {
                maxDist = d;
                index = i;
            }
        }

        if (maxDist > eps && index != -1)
        {
            marked[index] = true;
            DPMarkOpen(pts, i0, index, eps, marked);
            DPMarkOpen(pts, index, i1, eps, marked);
        }
    }

    static float PerpDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float ab2 = ab.sqrMagnitude;
        if (ab2 < 1e-8f) return Vector2.Distance(p, a);

        float t = Vector2.Dot(p - a, ab) / ab2;
        t = Mathf.Clamp01(t);
        Vector2 proj = a + t * ab;
        return Vector2.Distance(p, proj);
    }

    // ---------------- JSON parsing (simple scanning, no external libs) ----------------
    static bool TryParseImageWH(string s, out int w, out int h)
    {
        w = h = 0;
        return TryParseIntAfterKey(s, "\"image_w\"", out w) && TryParseIntAfterKey(s, "\"image_h\"", out h);
    }

    static bool TryParseIntAfterKey(string s, string key, out int value)
    {
        value = 0;
        int i = s.IndexOf(key);
        if (i < 0) return false;

        i = s.IndexOf(':', i);
        if (i < 0) return false;
        i++;

        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;

        int sign = 1;
        if (i < s.Length && s[i] == '-') { sign = -1; i++; }

        long v = 0;
        bool any = false;
        while (i < s.Length && char.IsDigit(s[i]))
        {
            any = true;
            v = v * 10 + (s[i] - '0');
            i++;
        }

        if (!any) return false;
        value = (int)(sign * v);
        return true;
    }

    static bool TryParseAllOuters(string s, out List<List<Vector2>> all)
    {
        all = new List<List<Vector2>>(16);
        int idx = 0;

        while (true)
        {
            int k = s.IndexOf("\"outer\"", idx);
            if (k < 0) break;

            if (!TryParseOuterAt(s, k, out List<Vector2> pts))
                return false;

            all.Add(pts);
            idx = k + 7;
        }

        return all.Count > 0;
    }

    static bool TryParseOuterAt(string s, int outerKeyIndex, out List<Vector2> pts)
    {
        pts = new List<Vector2>(256);

        int i = s.IndexOf(':', outerKeyIndex);
        if (i < 0) return false;
        i++;

        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        if (i >= s.Length || s[i] != '[') return false;

        int depth = 0;
        int x = 0, y = 0;
        bool haveX = false;

        for (; i < s.Length; i++)
        {
            char c = s[i];

            if (c == '[') { depth++; continue; }
            if (c == ']')
            {
                depth--;
                if (depth == 0) break;
                continue;
            }

            if (depth != 2) continue;
            if (c == ',' || char.IsWhiteSpace(c)) continue;

            if (c == '-' || char.IsDigit(c))
            {
                int sign = 1;
                if (c == '-') { sign = -1; i++; }

                int val = 0;
                bool any = false;
                while (i < s.Length && char.IsDigit(s[i]))
                {
                    any = true;
                    val = val * 10 + (s[i] - '0');
                    i++;
                }
                if (!any) return false;

                val *= sign;
                i--;

                if (!haveX) { x = val; haveX = true; }
                else
                {
                    y = val;
                    pts.Add(new Vector2(x, y));
                    haveX = false;
                }
            }
        }

        return pts.Count >= 3;
    }
}
