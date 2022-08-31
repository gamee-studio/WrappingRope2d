using UnityEngine;

namespace WrappingRopeLibrary.Model
{
    public class HitInfo
    {
        public int TriangleIndex { get; set; }
        public Vector3 Normal { get; set; }
        public Vector3 Point { get; set; }
        public Rigidbody Rigidbody { get; set; }
        public GameObject GameObject { get; set; }


        public HitInfo()
        {

        }

        public HitInfo(RaycastHit raycastHit)
        {
            TriangleIndex = raycastHit.triangleIndex;
            Normal = raycastHit.normal;
            Point = raycastHit.point;
            Rigidbody = raycastHit.rigidbody;
            GameObject = raycastHit.collider.gameObject;
        }
    }
}
