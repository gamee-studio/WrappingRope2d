using System;
using UnityEngine;
namespace WrappingRopeLibrary.Model
{
    [Serializable]
    public struct Node
    {
        public Vector3 _vertex;

        public Vector3 Vertex { get { return _vertex; } set { _vertex = value; } }

        public Vector2 _uv;

        public Vector2 Uv { get { return _uv; } set { _uv = value; } }

        public int _normalCount;

        public Vector3[] Normals;

        public int VertexIndex { get; set;  }

        public int UvLeamVertexIndex {get; set;}
        public Node(int normalsCount) : this()
        {
            Normals = new Vector3[normalsCount];
        }

        public Vector3 _averageNormal;


        public void AddNormal(Vector3 normal)
        {
            Normals[_normalCount] = normal;
            _normalCount++;
        }

        public Vector3 GetAverageNormal()
        {
            if (_averageNormal == Vector3.zero)
            {
                var res = Normals[0];
                //Debug.DrawRay(Vertex, Normals[0]);
                for (var i = 1; i < _normalCount; i++)
                {
                    //Debug.DrawRay(Vertex, Normals[i]);
                    var normal = Normals[i];
                    res+= normal;
                }
                _averageNormal = res.normalized;
            }
            return _averageNormal;
        }

        public void ResetNormals()
        {
            _normalCount = 0;
            _averageNormal = Vector3.zero;
        }

    }
}
