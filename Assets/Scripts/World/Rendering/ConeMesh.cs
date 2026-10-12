using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ConeMesh : MonoBehaviour
{
    public float radius = 2f;
    public float length = 5f;
    public int segments = 32;

    void Start()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Light Cone";

        // 顶点：
        // 0 = 锥尖
        // 1 ~ segments = 底部圆周
        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uv = new Vector2[segments + 1];

        // 锥尖
        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0);

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;

            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;

            // 光锥沿 Z 轴延伸
            vertices[i + 1] = new Vector3(x, y, length);

            // 这里最重要：
            // V 从锥尖的 0 → 底部的 1
            uv[i + 1] = new Vector2((float)i / segments, 1);
        }

        int[] triangles = new int[segments * 3];

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = next + 1;
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }
}