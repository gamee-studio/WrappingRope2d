using UnityEngine;

namespace WrappingRopeLibrary.Model
{
    public struct Vertex
    {
        public Vector3 Position { get; set; }
        public Vector2 Uv { get; set; }
        public Vector3 Normal { get; set; }
        public int[] NodeIndex { get; set; }


        public Vertex(Vector3 position, int[] nodeIndex, Vector2 uv) : this()
        {
            Position = position;
            NodeIndex = nodeIndex;
            Uv = uv;
        }
        public Vertex(Vector3 position, int[] nodeIndex, Vector2 uv, Vector3 normal) : this(position, nodeIndex, uv)
        {
            Normal = normal;
        }
    }
}
