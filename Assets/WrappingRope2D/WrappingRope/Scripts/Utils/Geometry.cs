using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WrappingRopeLibrary.Model;
using WrappingRopeLibrary.Enums;

namespace WrappingRopeLibrary.Utils
{
    public class Geometry
    {
        public static bool Raycast(Ray ray, out HitInfo hitInfo, float maxDistance, LayerMask layerMask)
        {
            hitInfo = new HitInfo();
            var raycastHit = new RaycastHit();
            if (Physics.Raycast(ray.origin, ray.direction, out raycastHit, maxDistance, layerMask))
            {
                return GetHitInfo(ray, raycastHit, maxDistance, out hitInfo);
            }
            return false;
        }


        public static bool RaycastClosed(Ray ray, out HitInfo hitInfo, float maxDistance, LayerMask layerMask, IEnumerable<GameObject> exclude)
        {
            hitInfo = new HitInfo();
            var hits = Physics.RaycastAll(ray.origin, ray.direction, maxDistance, layerMask);
            RaycastHit? raycastHit = null;
            var min = float.MaxValue;
            foreach(var hit in hits)
            {
                if (exclude.Any(x => hit.transform.gameObject == x))
                    continue;
                var dist = (hit.point - ray.origin).sqrMagnitude;
                if (dist < min)
                {
                    min = dist;
                    raycastHit = hit;
                }
            }
            if (raycastHit != null)
            {
                return GetHitInfo(ray, raycastHit.Value, maxDistance, out hitInfo);
            }
            return false;
        }

        private static bool GetHitInfo(Ray ray, RaycastHit raycastHit, float maxDistance, out HitInfo hitInfo)
        {
                hitInfo = new HitInfo();
                var meshCollider = raycastHit.collider as MeshCollider;
                if (meshCollider != null && !meshCollider.convex)
                {
                    if (!IsAllowedCollision(raycastHit.transform.gameObject))
                        return false;

                    hitInfo = new HitInfo(raycastHit);
                    return true;
                }
                // todo: if raycast has no hit, iterate other objects
                return TryRaycast(ray, raycastHit.collider.gameObject, maxDistance, out hitInfo);
        }

        public static bool TryRaycast(Ray ray, GameObject gameObject, float maxDistance, out HitInfo hitInfo)
        {
            hitInfo = new HitInfo();
            if (gameObject == null)
                return false;
            if (!IsAllowedCollision(gameObject))
                return false;
            hitInfo.Rigidbody = gameObject.GetComponent<Rigidbody>();
            hitInfo.GameObject = gameObject;
            // If object has non convex mesh collider, use native Raycast 
            var meshCollider = gameObject.GetComponent<MeshCollider>();
            if (meshCollider != null && !meshCollider.convex)
            {
                var raycastHit = new RaycastHit();
                if (meshCollider.Raycast(ray, out raycastHit, maxDistance))
                {
                    hitInfo = new HitInfo(raycastHit);
                    return true;
                }
                return false;
            }

            // Process collision with mesh
            var mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
            if (mesh == null)
            {
                return false;
            }
            var localOrigin = gameObject.transform.InverseTransformPoint(ray.origin);
            var localDest = gameObject.transform.InverseTransformPoint(ray.origin + ray.direction.normalized * maxDistance);
            var localDirection = localDest - localOrigin;
            var localDistance = localDirection.magnitude;
            var localRay = new Ray(localOrigin, localDirection);
            var plane1 = new Plane(localRay.origin, localRay.origin + localRay.direction, localRay.origin + GetThirdPoint(localRay.direction));
            var plane2 = new Plane(localRay.origin, localRay.origin + localRay.direction, localRay.origin + plane1.normal);
            float minLocalDistance = localDistance;
            var triangles = mesh.triangles;
            var vertices = mesh.vertices;

            for (int i = 0; i < triangles.Length; i += 3)
            {

                var v1 = vertices[triangles[i]];
                var v2 = vertices[triangles[i + 1]];
                var v3 = vertices[triangles[i + 2]];

                if ((plane1.SameSide(v1, v2) && plane1.SameSide(v2, v3))
                    || (plane2.SameSide(v1, v2) && plane2.SameSide(v2, v3)))
                    continue;

                var plane = GetPlane(v1, v2, v3);

                if (IsVisible(plane.normal, localDirection))
                {
                    float distance;
                    if (plane.Raycast(localRay, out distance))
                    {
                        var point = localRay.GetPoint(distance);


                        if (IsPointInTriangle(point, v1, v2, v3))
                        {
                            if (distance > minLocalDistance)
                                continue;

                            minLocalDistance = distance;
                            hitInfo.Normal = plane.normal;
                            hitInfo.Point = point;
                            hitInfo.TriangleIndex = i / 3;
                        }
                    }
                }
            }
            ApplyTransform(ref hitInfo, gameObject.transform);
            return minLocalDistance != localDistance;
        }


        private static bool IsAllowedCollision(GameObject gameObject)
        {
            // If object is invisible do not process collision
            var renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer == null)
                return false;
            // If object is part of static batch, do not process collistion, otherwise can get "Not allowed to access vertices on mesh" exception
            if (renderer.isPartOfStaticBatch)
                return false;
            return true;
        }


        private static void ApplyTransform(ref HitInfo hitInfo, Transform transform)
        {
            hitInfo.Normal = transform.TransformPoint(hitInfo.Normal);
            hitInfo.Point = transform.TransformPoint(hitInfo.Point);
        }

        private static bool IsVisible(Vector3 normal, Vector3 viewDir)
        {
            return (normal.x * viewDir.x + normal.y * viewDir.y + normal.z * viewDir.z) < 0;
        }


        private static Vector3 GetThirdPoint(Vector3 direction)
        {
            direction = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
            var components = new List<float>() { direction.x, direction.y, direction.z };

            var minComp = components.Min();

            var point = new Vector3();
            if (minComp == direction.y)
                return Vector3.up;
            if (minComp == direction.x)
                return Vector3.right;
            if (minComp == direction.z)
                return Vector3.forward;
            return point;
        }


        private static bool IsPointInTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            // Compute vectors        
            var v0 = c - a;
            var v1 = b - a;
            var v2 = point - a;

            // Compute dot products
            var dot00 = Vector3.Dot(v0, v0);
            var dot01 = Vector3.Dot(v0, v1);
            var dot02 = Vector3.Dot(v0, v2);
            var dot11 = Vector3.Dot(v1, v1);
            var dot12 = Vector3.Dot(v1, v2);

            // Compute barycentric coordinates
            var invDenom = 1 / (dot00 * dot11 - dot01 * dot01);
            var u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            var v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            // Check if point is in triangle
            return (u >= 0) && (v >= 0) && (u + v < 1);
        }

        public static List<Vector3> CreatePolygon(int vertexCount, Axis normal, float radius, float initAngle)
        {
            var res = new List<Vector3>();
            for (var i = 0; i < vertexCount; i++)
            {
                var x = radius * Mathf.Cos((float)Math.PI * 360 * (1f - (float)i / (vertexCount)) / 180);
                var y = radius * Mathf.Sin((float)Math.PI * 360 * (1f - (float)i / (vertexCount)) / 180);
                switch (normal)
                {
                    case Axis.X:
                        res.Add(new Vector3(0, x, y));
                        break;
                    case Axis.Y:
                        res.Add(new Vector3(x, 0, y));
                        break;
                    case Axis.Z:
                        res.Add(new Vector3(x, y, 0));
                        break;

                }
            }
            return res;
        }


        public static List<Vector3> RotatePoly(List<Vector3> vertices, float angle, Vector3 axis)
        {
            var rotateResult = new List<Vector3>();
            for (var i = 0; i < vertices.Count; i++)
            {
                rotateResult.Add(Quaternion.AngleAxis(angle, axis) * vertices[i]);
            }
            return rotateResult;
        }


        public static List<Vector3> RotatePoly(List<Vector3> vertices, Quaternion rotation)
        {
            var rotateResult = new List<Vector3>();
            for (var i = 0; i < vertices.Count; i++)
            {
                rotateResult.Add(rotation * vertices[i]);
            }
            return rotateResult;
        }


        public static void RotatePoly(Vector3[] target, Vector3[] source, Quaternion rotation)
        {
            for (var i = 0; i < source.Length; i++)
            {
                target[i] = rotation * source[i];
            }
        }


        public static void RotatePoly(Vector3[] target, Vector3[] source, float angle, Vector3 axis)
        {
            for (var i = 0; i < source.Length; i++)
            {
                target[i] = Quaternion.AngleAxis(angle, axis) * source[i];
            }
        }


        public static List<Vector3> TranslatePoly(List<Vector3> vertices, Vector3 direction)
        {
            var translateResult = new List<Vector3>();
            for (var i = 0; i < vertices.Count; i++)
            {
                translateResult.Add(vertices[i] + direction);
            }
            return translateResult;
        }


        public static List<Vector3> ScalePoly(List<Vector3> vertices, float scale)
        {
            var result = new List<Vector3>();
            for (var i = 0; i < vertices.Count; i++)
            {
                result.Add(vertices[i] * scale);
            }
            return result;
        }


        public static void ScalePoly(Vector3[] target, Vector3[] source, float scale)
        {
            for (var i = 0; i < source.Length; i++)
            {
                target[i] = source[i] * scale;
            }
        }

        public static float Angle(Vector3 vec1, Vector3 vec2, Vector3 n)
        {
            // angle in [0,180]
            float angle = Vector3.Angle(vec1, vec2);
            float sign = Mathf.Sign(Vector3.Dot(n, Vector3.Cross(vec1, vec2)));

            // angle in [-179,180]

            float angle360;
            if (sign < 0)
                angle360 = 360 - angle;
            else
                angle360 = angle;
            return angle360;

        }

        /// <summary>
        /// Compare points of two polygons
        /// </summary>
        /// <param name="p1">Polygon1</param>
        /// <param name="p2">Polygon2</param>
        /// <returns>Is points the same</returns>
        public static bool IsPolyTheSame(List<Vector3> p1, List<Vector3> p2)
        {
            if (p1.Count != p2.Count)
                return false;
            for (var i = 0; i < p1.Count; i++)
            {
                if (p1[i] != p2[i])
                    return false;
            }
            return true;

        }

        public static Plane GetPlane(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            Plane plane;
            if ((v1 - v2).sqrMagnitude < 0.01 || (v2 - v3).sqrMagnitude < 0.01 || (v3 - v1).sqrMagnitude < 0.01)
            {
                plane = new Plane(v1 * 100f, v2 * 100f, v3 * 100f);
                plane.SetNormalAndPosition(plane.normal, v1);
            }
            else
            {
                plane = new Plane(v1, v2, v3);
            }
            return plane;
        }
    }
}
