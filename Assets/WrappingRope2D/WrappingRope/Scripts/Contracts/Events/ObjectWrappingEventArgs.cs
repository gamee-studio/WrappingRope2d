using System.ComponentModel;
using UnityEngine;
using WrappingRopeLibrary.Scripts;

namespace WrappingRopeLibrary.Events
{
    public class ObjectWrapEventArgs : CancelEventArgs
    {
        private readonly Vector3[] _wrapPoints;
        private readonly GameObject _target;

        /// <summary>
        /// Gets an array of points in space that specifies the wrap path.
        /// </summary>
        public Vector3[] WrapPoints
        {
            get { return _wrapPoints; }
        }

        /// <summary>
        /// Gets a game object that the rope is about to wrap.
        /// </summary>
        public GameObject Target
        {
            get { return _target; }
        }

        public ObjectWrapEventArgs(GameObject target, Vector3[] wrapPoints)
        {
            _target = target;
            _wrapPoints = wrapPoints;
        }
    }

    public delegate void ObjectWrapEventHandler(RopeBase sender, ObjectWrapEventArgs args);
}
