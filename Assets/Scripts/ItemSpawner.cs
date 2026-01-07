using System.Collections;
using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    public Transform platformRoot;
    public string groundPiecesName = "GroundPieces";
    public Transform itemRoot;

    public float yOffset = 0.15f;
    public float minEdgeWidthWorld = 0.5f;

    IEnumerator Start()
    {
        // wait until PlayBuilder created GroundPieces + pieces
        Transform piecesParent = null;
        while (piecesParent == null || piecesParent.childCount == 0)
        {
            if (platformRoot != null)
                piecesParent = platformRoot.Find(groundPiecesName);

            yield return null; // wait 1 frame
        }

        // wait until customization selected prefab is available
        while (PlayCustomizationApplier.SelectedItemPrefab == null)
            yield return null;

        SpawnOnePerPlatform(piecesParent);
    }

    void SpawnOnePerPlatform(Transform piecesParent)
    {
        int spawned = 0;
        var prefab = PlayCustomizationApplier.SelectedItemPrefab;

        for (int i = 0; i < piecesParent.childCount; i++)
        {
            var piece = piecesParent.GetChild(i);
            var edge = piece.GetComponent<EdgeCollider2D>();
            if (edge == null || edge.points.Length < 2) continue;

            // skip tiny slivers
            float width = GetEdgeWidthWorld(edge);
            if (width < minEdgeWidthWorld) continue;

            Vector3 p = RandomPointOnEdgeWorld(edge);
            p.y += yOffset;

            Instantiate(prefab, p, Quaternion.identity, itemRoot);
            spawned++;
        }

        SessionManager.TargetCollectCount = spawned;
        Debug.Log($"ItemSpawner: Spawned {spawned} items. TargetCollectCount={spawned}");
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

    Vector3 RandomPointOnEdgeWorld(EdgeCollider2D edge)
    {
        var pts = edge.points;
        int seg = Random.Range(0, pts.Length - 1);
        Vector3 a = edge.transform.TransformPoint(pts[seg]);
        Vector3 b = edge.transform.TransformPoint(pts[seg + 1]);
        return Vector3.Lerp(a, b, Random.value);
    }
}
