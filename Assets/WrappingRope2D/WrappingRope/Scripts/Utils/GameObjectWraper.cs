#define DEBUG
#undef DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WrappingRopeLibrary.Customization;
using WrappingRopeLibrary.Helpers;
using WrappingRopeLibrary.Model;
using WrappingRopeLibrary.Scripts;


namespace WrappingRopeLibrary.Utils
{
    internal class GameObjectWraper
    {
        private GameObject _gameObject;
        private Piece _piece;

        private PieceInfo _pieceInfo;
        private HitInfo _hitInfo;

        private int[] _triangles;

        private Vector3[] _vertices;

        private float _frontToHitToBackRelation1;
        private float _frontToHitToBackRelation2;
        private Vector3 _hipPoint1;
        private Vector3 _hipPoint2;
        private float _sqrTreshold;

        internal GameObjectWraper(PieceInfo pieceInfo, HitInfo hitInfo1, HitInfo hitInfo2)
        {
            _gameObject = hitInfo1.GameObject;
            _piece = pieceInfo.Piece;
            _pieceInfo = pieceInfo;
            _hitInfo = hitInfo1;
            _hipPoint1 = hitInfo1.Point;
            _hipPoint2 = hitInfo2.Point;
            var pieceStageDistance = (_pieceInfo.FrontBandPoint - _pieceInfo.BackBandPoint).magnitude;
            var frontToHitDistance = (_pieceInfo.FrontBandPoint - _hitInfo.Point).magnitude;
            _frontToHitToBackRelation1 = frontToHitDistance / pieceStageDistance;
            frontToHitDistance = (_pieceInfo.FrontBandPoint - hitInfo2.Point).magnitude;
            _frontToHitToBackRelation2 = frontToHitDistance / pieceStageDistance;
            _sqrTreshold = _piece.Threshold * _piece.Threshold;
            var mesh = _gameObject.GetComponent<MeshFilter>().sharedMesh;
            _triangles = mesh.triangles;
            _vertices = mesh.vertices;
        }

        internal List<WrapPoint> GetWrapPoints()
        {
            var wrapPoints = new List<WrapPoint>();
            List<WrapPoint> pathPoints = new List<WrapPoint>();
            try
            {
                // Convert piece ends position to local system
                var localPiecePoints = GetLocalPiecePoints(_pieceInfo);
                bool cantDefineWrapSide;
                var piecePlane = GetPiecePlane(out cantDefineWrapSide, localPiecePoints);
                if (cantDefineWrapSide)
                    return wrapPoints;
                var crossPlane = GetCrossPlane(piecePlane, localPiecePoints);
#if DEBUG
                 DrawBasePlanes(_hitInfo.Point, piecePlane.normal, crossPlane.normal);
#endif
                var pathFinder = new WrapPathFinder(_gameObject, _pieceInfo, _hitInfo, localPiecePoints, piecePlane, crossPlane, _triangles, _vertices, _sqrTreshold);
                pathPoints = pathFinder.GetWrapPath();
                if (pathPoints.Count == 0) return wrapPoints;
                if (IsBadPath(pathPoints))
                {
#if DEBUG
                    Debug.Log("Bad path!!!");
#endif
                    return wrapPoints;
                }
                // Newly created points could define pieces that collide with object, so check collision of first and last piece with object and wrap again
                var frontPieceBackBandPoint = GetPointInWorldSpace(crossPlane.normal, pathPoints[0]);
                var backPieceFrontBandPoint = GetPointInWorldSpace(crossPlane.normal, pathPoints[pathPoints.Count - 1]);

                var frontPiece = new PieceInfo() { FrontBandPoint = _piece.FrontBandPoint.PositionInWorldSpace, BackBandPoint = frontPieceBackBandPoint };
                var backPiece = new PieceInfo() { FrontBandPoint = backPieceFrontBandPoint, BackBandPoint = _piece.BackBandPoint.PositionInWorldSpace };

                var frontThirdPoint = _pieceInfo.FrontBandPoint;
                var backThirdPoint = _pieceInfo.BackBandPoint;

                var extFrontPathPoints = ResolveNewPieceCollisionAndGetAdditionPathPoints(frontPiece, frontThirdPoint);
                var extBackPathPoints = ResolveNewPieceCollisionAndGetAdditionPathPoints(backPiece, backThirdPoint);
                pathPoints.InsertRange(0, extFrontPathPoints);
                pathPoints.InsertRange(pathPoints.Count, extBackPathPoints);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            pathPoints.ForEach(point => point.SetPointInWorldSpace(_gameObject.transform, _piece.WrapDistance));
            return pathPoints;
        }

        private bool IsBadPath(List<WrapPoint> pathPoints)
        {
            var prevPiece = _piece.FrontPiece;
            if (prevPiece == null)
                return false;
            if (pathPoints.Count < 2)
                return false;

            var prevPieceDirection = _gameObject.transform.InverseTransformPoint(prevPiece.FrontBandPoint.PositionInWorldSpace)
            - _gameObject.transform.InverseTransformPoint(prevPiece.BackBandPoint.PositionInWorldSpace);
            var pathDirection = pathPoints[1].Origin - pathPoints[0].Origin;
            return Vector3.Angle(pathDirection, prevPieceDirection) < 10;

        }

        private List<WrapPoint> GetWrapPointsInWorldCoordinates(List<WrapPoint> pathPoints, Vector3 localShift)
        {
            var resultPoints = new List<WrapPoint>();
            foreach (var pathPoint in pathPoints)
            {
                WrapPoint resultPoint = new WrapPoint() { PositionInWorldSpace = GetPointInWorldSpace(localShift, pathPoint) };
                resultPoints.Add(resultPoint);
            }
            return resultPoints;
        }

        private Vector3 GetPointInWorldSpace(Vector3 localShift, WrapPoint pathPoint)
        {
            float wrapDistance = _piece.WrapDistance < 0.001 ? 0.001f : _piece.WrapDistance;
            var shift = (_gameObject.transform.rotation * localShift).normalized * wrapDistance;
            var resultPoint = _gameObject.transform.TransformPoint(pathPoint.Origin) + shift;
            return resultPoint;
        }


        private List<WrapPoint> ResolveNewPieceCollisionAndGetAdditionPathPoints(PieceInfo pieceInfo, Vector3 thirdPoint)
        {
            Ray ray = new Ray(pieceInfo.FrontBandPoint, pieceInfo.BackBandPoint - pieceInfo.FrontBandPoint);
            HitInfo hitInfo;
            if (!Geometry.TryRaycast(ray, _gameObject, (pieceInfo.BackBandPoint - pieceInfo.FrontBandPoint).magnitude, out hitInfo))
                return new List<WrapPoint>();

            var localPiecePoints = GetLocalPiecePoints(pieceInfo);
            thirdPoint = _gameObject.transform.InverseTransformPoint(thirdPoint);
            var piecePlane = Geometry.GetPlane(localPiecePoints[1], localPiecePoints[3], thirdPoint);
            var crossPlane = GetCrossPlane(piecePlane, localPiecePoints);
            var pathFinder = new WrapPathFinder(_gameObject, pieceInfo, hitInfo, localPiecePoints, piecePlane, crossPlane, _triangles, _vertices, _sqrTreshold);
            return pathFinder.GetWrapPath();
        }


        private void DrawBasePlanes(Vector3 origin, Vector3 piecePlaneNormal, Vector3 crossPlaneNormal)
        {
            DebugDraw.DrawPlane(origin, piecePlaneNormal, Color.green, Color.red);
            DebugDraw.DrawPlane(origin, crossPlaneNormal, Color.magenta, Color.yellow);
        }


        private Plane GetPiecePlane(out bool cantDefineWrapSide, Vector3[] localPiecePoints)
        {
            Vector3 thirdPoint = new Vector3();
            cantDefineWrapSide = false;
            if (!GetThirdPoint(ref thirdPoint))
            {
                cantDefineWrapSide = true;
                return new Plane();
            }
            thirdPoint = _gameObject.transform.InverseTransformPoint(thirdPoint);

            Plane plane = Geometry.GetPlane(localPiecePoints[1], localPiecePoints[3], thirdPoint);
            return plane;
        }


        private Vector3[] GetLocalPiecePoints(PieceInfo pieceInfo)
        {
            var p1 = _gameObject.transform.InverseTransformPoint(pieceInfo.PrevFrontBandPoint);
            var p2 = _gameObject.transform.InverseTransformPoint(pieceInfo.BackBandPoint);
            var p3 = _gameObject.transform.InverseTransformPoint(pieceInfo.PrevBackBandPoint);
            // Get addition point for some tasks
            var p4 = _gameObject.transform.InverseTransformPoint(pieceInfo.FrontBandPoint);
            return new[] { p1, p2, p3, p4 };
        }

        private Vector3 GetPieceVelocityInHitPoint(float relation)
        {
            var frontBandPointVelocity = _piece.DefineFrontBandPointVelocity();
            var backBandPointVelocity = _piece.DefineBackBandPointVelocity();
            Vector3 mergeVelocity = Vector3.zero;
            if (_piece.LastWrapPointPosition.HasValue)
            {
                mergeVelocity = (_piece.FrontBandPoint.PositionInWorldSpace - _piece.LastWrapPointPosition.Value) / Time.fixedDeltaTime;
            }
            return Vector3.Lerp(frontBandPointVelocity + mergeVelocity, backBandPointVelocity, relation);
        }


        private bool GetThirdPoint(ref Vector3 thirdPoint)
        {
            var velocity1 = Vector3.zero;
            var velocity2 = Vector3.zero;
            // Priority for object implemented interface IRopeInteraction
            var ropeInteraction = _gameObject.GetComponent<IRopeInteraction>();
            if (ropeInteraction != null)
            {
                velocity1 = ropeInteraction.GetPointVelocity(_hipPoint1);
                velocity2 = ropeInteraction.GetPointVelocity(_hipPoint2);
            }
            else if (_hitInfo.Rigidbody != null)
            {
                velocity1 = _hitInfo.Rigidbody.GetPointVelocity(_hipPoint1);
                velocity2 = _hitInfo.Rigidbody.GetPointVelocity(_hipPoint2);
            }
#if DEBUG
                Debug.DrawRay(_hipPoint1, velocity1, Color.red, 5f);
                Debug.DrawRay(_hipPoint2, velocity2, Color.red, 5f);
#endif
            var pieceVelocityInHitPoint1 = GetPieceVelocityInHitPoint(_frontToHitToBackRelation1);
            var pieceVelocityInHitPoint2 = GetPieceVelocityInHitPoint(_frontToHitToBackRelation2);
#if DEBUG

                Debug.DrawRay(_hipPoint1, pieceVelocityInHitPoint1, Color.green, 5f);
                Debug.DrawRay(_hipPoint2, pieceVelocityInHitPoint2, Color.green, 5f);
#endif
            var hitVector = velocity1 - pieceVelocityInHitPoint1 + velocity2 - pieceVelocityInHitPoint2;
            if (hitVector.Equals(Vector3.zero)) return false;
            thirdPoint = _hitInfo.Point + hitVector;
            return true;
        }

        private Plane GetCrossPlane(Plane sourcePlane, Vector3[] localPiecePoints)
        {
            // Find point position above the end of piece, that normal to the plane
            return GetCrossPlane(sourcePlane, localPiecePoints[1], localPiecePoints[3]);
        }


        private Plane GetCrossPlane(Plane sourcePlane, Vector3 p1, Vector3 p2)
        {
            var p3 = sourcePlane.normal + p1;
            return Geometry.GetPlane(p2, p1, p3);
        }


        internal class WrapPathFinder
        {

            private GameObject _gameObject;
            private PieceInfo _pieceInfo;
            private HitInfo _hitInfo;
            private Vector3[] _localPiecePoints = new Vector3[4];
            private Plane _piecePlane;
            private Plane _crossPlane;
            private int[] _triangles;
            private Vector3[] _vertices;
            private float _sqrTreshold;
            private List<Edge[]> _edgeCache = new List<Edge[]>();

            internal WrapPathFinder(
                GameObject gameObject,
                PieceInfo pieceInfo,
                HitInfo hitInfo,
                Vector3[] localPiecePoints,
                Plane piecePlane,
                Plane crossPlane,
                int[] triangles,
                Vector3[] vertices,
                float sqrTreshold
                )
            {
                _gameObject = gameObject;
                _pieceInfo = pieceInfo;
                _hitInfo = hitInfo;
                _localPiecePoints = localPiecePoints;
                _piecePlane = piecePlane;
                _crossPlane = crossPlane;
                _triangles = triangles;
                _vertices = vertices;
                _sqrTreshold = sqrTreshold;
            }

            internal List<WrapPoint> GetWrapPath()
            {
                List<WrapPoint> crossPoints;
                TryFindCrossPointsInMeshCoords(out crossPoints, false);
                var pathPoints = GetWrapPathFromCrossPoints(crossPoints);
                return pathPoints;
            }

            private int FindEdgeGroupIndexByTriangleIndex(int triangleindex)
            {
                var edgeGroup = _edgeCache.Find(eg => eg[0].TriangleIndex == triangleindex);
                if (edgeGroup != null)
                    return _edgeCache.IndexOf(edgeGroup);
                return -1;
            }



            private List<WrapPoint> FindCrossPointsInMeshCoordsForHill(int startEdgeGroupIndex, out bool isCrosspointOutOfTreshold, bool checkTreshold = true)
            {
                var crossPoints = new List<WrapPoint>();
                if (startEdgeGroupIndex + 1 > _edgeCache.Count)
                {
#if DEBUG
                Debug.Log("GameObjectWrapper.FindCrossPointsInMeshCoordsForHill: Start edge group index is out of edge cache range.");
#endif
                    isCrosspointOutOfTreshold = false;
                    return crossPoints;
                }
                var startEdges = _edgeCache[startEdgeGroupIndex];
                var startEdge = startEdges[0];
                if (startEdge != null && !IsCrossPointInWorkSpace(startEdge.Vertex1, startEdge.Vertex2))
                    startEdge = startEdges[1];
                FindCrossPointsForEdgesRecursively(startEdge, ref crossPoints, out isCrosspointOutOfTreshold, checkTreshold);
                return crossPoints;
            }


            private bool CheckRaycastHit(Vector3 startPoint, Vector3 stopPoint, out int triangleIndex, out Vector3 hitPoint)
            {
                var direction = stopPoint - startPoint;
                Ray ray = new Ray() { origin = startPoint + direction.normalized * 0.01f, direction = direction };
                var distance = direction.magnitude;
                HitInfo hitInfo;
                var res = Geometry.TryRaycast(ray, _gameObject, distance, out hitInfo);

                triangleIndex = hitInfo.TriangleIndex;
                hitPoint = hitInfo.Point;
                return res;
            }


            private bool TryFindCrossPointsInMeshCoords(out List<WrapPoint> crossPoints, bool checkTreshold = true)
            {
                int startEdgeGroupIndex = 0;
                crossPoints = new List<WrapPoint>();
                FillEdgesCache(_hitInfo.TriangleIndex, out startEdgeGroupIndex);
                if (_edgeCache.Count == 0)
                {
#if DEBUG
                var name = _gameObject.name;
                Debug.LogWarning(string.Format("Count of wrapped edges of gameObject's ({0}) mesh is zero, but collision of rope and this gameObject was obtained. Most likely wrapping path is wrong because of rounding.", name));
#endif
                }
                bool isCrosspointOutOfTreshold = false;
                var startPoint = _hitInfo.Point;
                do
                {
                    crossPoints.AddRange(FindCrossPointsInMeshCoordsForHill(startEdgeGroupIndex, out isCrosspointOutOfTreshold, checkTreshold));
                    if (isCrosspointOutOfTreshold)
                    {
                        crossPoints.Clear();
#if DEBUG
                    Debug.Log("CrosspointOutOfTreshold");
#endif
                        return false;
                    }
                    int triangleIndex;
                    if (CheckRaycastHit(startPoint, _pieceInfo.BackBandPoint, out triangleIndex, out startPoint))
                    {
                        startEdgeGroupIndex = FindEdgeGroupIndexByTriangleIndex(triangleIndex);
                    }
                    else { break; }
                } while (startEdgeGroupIndex > -1);
                return true;
            }


            private void FindCrossPointsForEdgesRecursively(Edge edge, ref List<WrapPoint> crossPoints, out bool isCrosspointOutOfTreshold, bool checkTreshold = true)
            {
                Edge nextEdge;
                isCrosspointOutOfTreshold = false;
                if (edge == null) return;
                if (!AddEdgeCrossPoint(edge.Vertex1, edge.Vertex2, ref crossPoints, out isCrosspointOutOfTreshold, checkTreshold))
                {
                    return;
                }
                int i = 0;
                nextEdge = FindNextEdgeInNaibourEdgeGroup(edge, out i);
                if (nextEdge == null || checkTreshold && isCrosspointOutOfTreshold) return;
                if (crossPoints.Count > 60)
                {
                    isCrosspointOutOfTreshold = true;
                    crossPoints.Clear();
#if DEBUG
                Debug.LogWarning("Too many wrap points in one wrap (over 60)! Possible stack overflow exception!");
#endif
                    return;
                }
                FindCrossPointsForEdgesRecursively(nextEdge, ref crossPoints, out isCrosspointOutOfTreshold, checkTreshold);
            }


            private void FillEdgesCache(int triangleIndex, out int startEdgeGroupIndex)
            {
                _edgeCache.Clear();
                startEdgeGroupIndex = 0;
                Edge startDupleEdge = null;
                var vertexTriangleIndex = triangleIndex * 3;
                for (int i = 0; i < _triangles.Length; i += 3)
                {
                    var v1 = _vertices[_triangles[i]];
                    var v2 = _vertices[_triangles[i + 1]];
                    var v3 = _vertices[_triangles[i + 2]];

                    if (!_crossPlane.GetSide(v1) && !_crossPlane.GetSide(v2) && !_crossPlane.GetSide(v3))
                        continue;

                    var edges = new Edge[2] { null, null };
                    var pos = 0;
                    if (!_piecePlane.SameSide(v1, v2))
                    {
                        edges[pos] = new Edge() { Vertex1 = v1, Vertex2 = v2, TriangleIndex = i / 3 };
                        pos++;
                    }

                    if (!_piecePlane.SameSide(v2, v3))
                    {
                        edges[pos] = new Edge() { Vertex1 = v2, Vertex2 = v3, TriangleIndex = i / 3 };
                        pos++;
                    }

                    if (!_piecePlane.SameSide(v3, v1))
                    {
                        edges[pos] = new Edge() { Vertex1 = v3, Vertex2 = v1, TriangleIndex = i / 3 };
                        pos++;
                    }

                    bool dupleEdge = v1 == v2 || v1 == v3 || v3 == v2;
                    if (i == vertexTriangleIndex)
                    {
                        startEdgeGroupIndex = _edgeCache.Count;
                        if (dupleEdge && pos > 0)
                        {
                            startDupleEdge = edges[0];
                        }

                    }
                    if (pos > 0 && !dupleEdge)
                    {
                        _edgeCache.Add(edges);
                    }

                }
                if (startDupleEdge != null)
                {
                    int edgeGroupIndex;
                    var naibour = FindNextEdgeInNaibourEdgeGroup(startDupleEdge, out edgeGroupIndex);
                    startEdgeGroupIndex = edgeGroupIndex;
                }
            }


            private Edge FindNextEdgeInNaibourEdgeGroup(Edge edge, out int edgeGroupIndex)
            {
                edgeGroupIndex = 0;
                foreach (var edgeGroup in _edgeCache)
                {
                    // If group contains an edge, it is already processed
                    if (edge.Equals(edgeGroup[0]) || edge.Equals(edgeGroup[1]))
                    {
                        edgeGroupIndex++;
                        continue;
                    }
                    // If coordinates of edge match coordinates of first edge of group, return second edge of group
                    if (edgeGroup[0] != null && (edge.Vertex1 == edgeGroup[0].Vertex1 && edge.Vertex2 == edgeGroup[0].Vertex2
                        || edge.Vertex1 == edgeGroup[0].Vertex2 && edge.Vertex2 == edgeGroup[0].Vertex1))
                    {
                        return edgeGroup[1];
                    }

                    // If coordinates of edge match coordinates of second edge of group, return first edge of group
                    if (edgeGroup[1] != null && (edge.Vertex1 == edgeGroup[1].Vertex1 && edge.Vertex2 == edgeGroup[1].Vertex2
                        || edge.Vertex1 == edgeGroup[1].Vertex2 && edge.Vertex2 == edgeGroup[1].Vertex1))
                    {
                        return edgeGroup[0];
                    }

                    edgeGroupIndex++;
                }
                return null;
            }


            private bool AddEdgeCrossPoint(Vector3 vertex1, Vector3 vertex2, ref List<WrapPoint> crossPoints, out bool isCrosspointOutOfTreshold, bool checkTreshold)
            {
                isCrosspointOutOfTreshold = false;
                if (_piecePlane.SameSide(vertex1, vertex2))
                    return false;
                if (!_crossPlane.GetSide(vertex1) && !_crossPlane.GetSide(vertex2))
                    return false;
                float distance;
                var ray = new Ray(vertex1, vertex2 - vertex1);
                _piecePlane.Raycast(ray, out distance);
                var crossPoint = ray.GetPoint(distance);
                if (!_crossPlane.GetSide(crossPoint))
                    return false;
                crossPoints.Add(new WrapPoint { Origin = crossPoint, LocalShift = _crossPlane.normal, Parent = _gameObject });
                isCrosspointOutOfTreshold = checkTreshold && IsPointOutOfTreshold(crossPoint);
                return true;
            }

            private bool IsCrossPointInWorkSpace(Vector3 vertex1, Vector3 vertex2)
            {
                if (_piecePlane.SameSide(vertex1, vertex2))
                    return false;
                if (!_crossPlane.GetSide(vertex1) && !_crossPlane.GetSide(vertex2))
                    return false;
                float distance;
                var ray = new Ray(vertex1, vertex2 - vertex1);
                _piecePlane.Raycast(ray, out distance);
                var crossPoint = ray.GetPoint(distance);
                if (!_crossPlane.GetSide(crossPoint))
                    return false;
                return true;
            }


            private bool IsPointOutOfTreshold(Vector3 point)
            {
                var pieceVector = _pieceInfo.FrontBandPoint - _pieceInfo.BackBandPoint;
                var worldPoint = _gameObject.transform.TransformPoint(point);
                var worldPointVector = worldPoint - _pieceInfo.BackBandPoint;
                var worldPointProjection = Vector3.Project(worldPointVector, pieceVector);
                var distance = (worldPointVector - worldPointProjection).sqrMagnitude;
                return distance > _sqrTreshold;
            }


            private List<WrapPoint> GetWrapPathFromCrossPoints(List<WrapPoint> crossPoints)
            {
                var points = new List<WrapPoint>();
                DefineWrapPatchPoint(_piecePlane, _localPiecePoints[3], crossPoints, ref points);
                return points;
            }

            private void DefineWrapPatchPoint(Plane sourcePlane, Vector3 sourcePoint, List<WrapPoint> crossPoints, ref List<WrapPoint> pathPoints)
            {
                // Global check
                var point = sourcePlane.normal + sourcePoint;
                var plane = Geometry.GetPlane(sourcePoint, _localPiecePoints[1], point);
                // If all points are under the plane
                if (crossPoints.All(crossPoint => !plane.GetSide(crossPoint.Origin)))
                {
                    return; // Add nothing
                }
                WrapPoint pathPoint = null;
                var inSideCache = new List<WrapPoint>();
                var inSideCrossPointsCount = crossPoints.Count;
                foreach (var crossPoint in crossPoints)
                {
                    plane = Geometry.GetPlane(sourcePoint, crossPoint.Origin, point);
                    var inSide = crossPoints.FindAll(p => plane.GetSide(p.Origin) && !p.Equals(crossPoint));
                    if (inSideCrossPointsCount > inSide.Count)
                    {
                        inSideCache = inSide;
                        inSideCrossPointsCount = inSide.Count;
                        if (inSideCrossPointsCount == 0)
                        {
                            pathPoint = crossPoint;
                            break;
                        }
                    }
                }
                if (pathPoint == null && inSideCache.Count == 0)
                    return;
                if (inSideCache.Count > 0)
                    pathPoint = inSideCache[0];
                var i = crossPoints.IndexOf(pathPoint);
                crossPoints.RemoveRange(0, i + 1);
                pathPoints.Add(pathPoint);
                DefineWrapPatchPoint(sourcePlane, pathPoint.Origin, crossPoints, ref pathPoints);
            }

        }
    }
}
