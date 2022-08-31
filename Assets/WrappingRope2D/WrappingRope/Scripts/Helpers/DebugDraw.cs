using System.Collections.Generic;
using UnityEngine;

namespace WrappingRopeLibrary.Helpers
{
    public static class DebugDraw
    {

        public static void DrawPlane(Vector3 position, Vector3 normal, Color color, Color normalColor)
        {
            Vector3 v3;
            if (normal.normalized != Vector3.forward)
                v3 = Vector3.Cross(normal, Vector3.forward).normalized * normal.magnitude;
            else
                v3 = Vector3.Cross(normal, Vector3.up).normalized * normal.magnitude; ;
            var corner0 = position + v3;
            var corner2 = position - v3;

            var q = Quaternion.AngleAxis(90f, normal);
            v3 = q * v3;
            var corner1 = position + v3;
            var corner3 = position - v3;
            Debug.DrawLine(corner0, corner2, color, 3f);
            Debug.DrawLine(corner1, corner3, color, 3f);
            Debug.DrawLine(corner0, corner1, color, 3f);
            Debug.DrawLine(corner1, corner2, color, 3f);
            Debug.DrawLine(corner2, corner3, color, 3f);
            Debug.DrawLine(corner3, corner0, color, 3f);
            Debug.DrawRay(position, normal, normalColor, 5f);
        }


        public static void DrawPoly(List<Vector3> vertices, Color color, float duration)
        {
            for(var i = 0; i < vertices.Count - 1; i++)
            {
                Debug.DrawLine(vertices[i], vertices[i + 1], color, duration);
            }
            Debug.DrawLine(vertices[vertices.Count - 1], vertices[0], color, duration);
        }
    }
}
