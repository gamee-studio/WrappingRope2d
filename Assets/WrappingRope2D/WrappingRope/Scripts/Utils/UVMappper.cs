using UnityEngine;
using WrappingRopeLibrary.Enums;

namespace WrappingRopeLibrary.Utils
{
    internal class UVMappper
    {
        internal static Vector2 GetUv(UVLocation uvLocation, float cross, float extend)
        {
            switch (uvLocation)
            {
                case UVLocation.AlongU:
                case UVLocation.ContraU:
                    return new Vector2(extend, cross);
                case UVLocation.AlongV:
                case UVLocation.ContraV:
                    return new Vector2(cross, extend);
                default:
                    return new Vector2(extend, cross);
            }
        }
    }
}
