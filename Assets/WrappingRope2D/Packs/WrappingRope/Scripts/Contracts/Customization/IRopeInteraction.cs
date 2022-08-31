using System.Collections.Generic;
using UnityEngine;

namespace WrappingRopeLibrary.Customization
{
    public interface IRopeInteraction
    {
        /// <summary>
        /// The velocity of the rigidbody at the point worldPoint in global space.
        /// </summary>
        /// <param name="worldPoint"></param>
        /// <returns></returns>
        Vector3 GetPointVelocity(Vector3 worldPoint);

        void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode mode);
    }
}
