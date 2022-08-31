using UnityEngine;

namespace WrappingRopeLibrary.Model
{
    public class Edge
    {
        public Vector3 Vertex1 { get; set; }
        public Vector3 Vertex2 { get; set; }

        public int TriangleIndex { get; set; }
    }
}
