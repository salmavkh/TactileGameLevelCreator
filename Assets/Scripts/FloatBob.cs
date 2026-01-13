using UnityEngine;

public class FloatBob2D : MonoBehaviour
{
    public float amplitude = 0.15f;   // how high it floats
    public float speed = 1.5f;        // how fast

    Vector3 startPos;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        float y = Mathf.Sin(Time.time * speed) * amplitude;
        transform.localPosition = startPos + new Vector3(0f, y, 0f);
    }
}
