
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class VisionConeVisual : MonoBehaviour
{
    [SerializeField] private float viewDistance = 8f;
    [SerializeField] private float viewAngle = 70f;
    [SerializeField] private int segments = 40;

    private Mesh mesh;

    void Awake()
    {
        mesh = new Mesh();
        mesh.name = "Vision Cone Mesh";

        GetComponent<MeshFilter>().mesh = mesh;

        GenerateCone();
    }

    void GenerateCone()
    {
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float angle = -viewAngle / 2f +
                (viewAngle * i / segments);

            float radians = angle * Mathf.Deg2Rad;

            vertices[i + 1] = new Vector3(
                Mathf.Sin(radians) * viewDistance,
                0f,
                Mathf.Cos(radians) * viewDistance
            );

            if (i < segments)
            {
                int t = i * 3;

                triangles[t] = 0;
                triangles[t + 1] = i + 1;
                triangles[t + 2] = i + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
}
