using System;
using System.Collections.Generic;
using UnityEngine;

namespace WrappingRopeLibrary.Model
{
    [Serializable]
    public class MeshConfigurator : object
    {
        [Range(0, 4)]
        public int BendCrossectionsNumber = 0;
        [Obsolete("This field is legacy")]
        public bool FlipNormals = false;
        [SerializeField]
        public List<Vector3> Profile = new List<Vector3>(3);

    }
}
