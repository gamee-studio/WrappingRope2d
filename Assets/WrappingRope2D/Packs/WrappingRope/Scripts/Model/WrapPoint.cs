using System;
using UnityEngine;

namespace WrappingRopeLibrary.Model
{
    [Serializable]
    public class WrapPoint : object
    {
        public Vector3 Origin;

        public Vector3 LocalShift;

        public Vector3 PositionInWorldSpace;

        public GameObject Parent;

        public void SetPointInWorldSpace(Transform transform, float wrapDistance)
        {
            wrapDistance = wrapDistance < 0.001 ? 0.001f : wrapDistance;
            var shift = (transform.rotation * LocalShift).normalized * wrapDistance;
            PositionInWorldSpace = transform.TransformPoint(Origin) + shift;
        }


        public void SetPointInWorldSpace(float wrapDistance)
        {
            if (Parent == null)
                return;
            SetPointInWorldSpace(Parent.transform, wrapDistance);
        }
    }
}
