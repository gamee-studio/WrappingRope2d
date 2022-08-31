#define DEBUG
#undef DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WrappingRopeLibrary.Enums;
using WrappingRopeLibrary.Helpers;
using WrappingRopeLibrary.Attributes;
using WrappingRopeLibrary.Model;
using WrappingRopeLibrary.Utils;

namespace WrappingRopeLibrary.Scripts
{
    [ExecuteInEditMode]
    public class Piece : MonoBehaviour 
    {

        public Piece FrontPiece;

        public Piece BackPiece;

        public WrapPoint FrontBandPoint;

        public WrapPoint BackBandPoint;

        [SerializeField]
        internal Vector3 PrevFrontBandPoint;

        [SerializeField]
        internal Vector3 PrevBackBandPoint;

        [SerializeField]
        protected RopeBase _rope;

        [SerializeField]
        protected Guid PieceUid;

        internal float Threshold
        {
            get { return _rope.Threshold; }
        }

        internal float WrapDistance
        {
            get { return _rope.WrapDistance; }
        }

        internal Vector3? LastWrapPointPosition { get; set; }

        [SerializeField]
        private float _length;
        public float Length { get { return _length; } }

        private GameObject _bendPointInstance;

        [SerializeField]
        protected List<NodeArray> _sections;

        [SerializeField]
        protected float _backSectionDistance;
        [SerializeField]
        protected float _maxDistance;

        public int SharedNodeIndex;

        [Serializable]
        public class NodeArray
        {
            public Node[] _array;
            public NodeArray(Node[] array)
            {
                _array = array;
            }

            public Node this[int i]
            {
                get { return _array[i]; }
            }

            public Node[] SourceArray { get { return _array; } }

            public static implicit operator Node[] (NodeArray nodeArray)
            {
                return nodeArray.SourceArray;
            }
        }

        [NotCopy]
        private Material _workMaterial;

        [NotCopy]
        [SerializeField]
        private Material _material;


        void Awake()
        {
            TrySetMaterial();
            DontReorganizeWhenDestroy = false;
        }


        private bool TrySetMaterial()
        {
            var rend = GetComponent<Renderer>();
            if (rend != null)
            {
                if (_material == null)
                {
                    if (rend.sharedMaterial != null)
                    {
                        _material = rend.sharedMaterial;
                    }
                    else
                    {
                        return false;
                    }
                }
                _workMaterial = new Material(_material);
                rend.sharedMaterial = _workMaterial;
                return true;
            }
            return false;
        }

        public bool DontReorganizeWhenDestroy;

        public void Init(WrapPoint fBP, WrapPoint bBP, Piece fP, Piece bP, RopeBase rope, int layer, bool resetPrevBandPoints = true)
        {
            gameObject.layer = layer;
            FrontBandPoint = fBP;
            BackBandPoint = bBP;
            FrontPiece = fP;
            BackPiece = bP;
            _rope = rope;
            if (fP != null)
                _bendPointInstance = _rope.GetBendInstance();
            PieceUid = Guid.NewGuid();
            gameObject.name = String.Format("Piece {0}", PieceUid);
            if (fP != null) fP.BackPiece = this;
            if (bP != null) bP.FrontPiece = this;
            SaveBandPointPositions();
            Relocate(resetPrevBandPoints);
            InitMeshProfilesAndSections();
        }

        public void InitMeshProfilesAndSections()
        {
            _baseProfile = _rope.GetWorkProfileClone();
            _helpProfile = _rope.GetWorkProfileClone();
            CreateSections();
        }


        protected void CreateSections()
        {
            //Debug.Log("CreateSections");
            _sections = new List<NodeArray>();
            for (var i = 0; i < 2 + _rope.BendCrossectionsNumber; i++)
            {
                var nodes = new List<Node>();
                for (var j = 0; j < _baseProfile.Length; j++)
                {
                    nodes.Add(new Node(6));
                }
                _sections.Add(new NodeArray(nodes.ToArray()));
            }
        }

        public void Init(WrapPoint fBP, WrapPoint bBP, Piece fP, Piece bP, RopeBase rope, Vector3 pfBP, Vector3 pbBP, int layer, bool resetPrevBandPoints = true)
        {
            Init(fBP, bBP, fP, bP, rope, layer, resetPrevBandPoints);
            PrevBackBandPoint = pbBP;
            PrevFrontBandPoint = pfBP;
        }

        private void SaveBandPointPositions()
        {
            var pos = BackBandPoint.PositionInWorldSpace;
            if (BackBandPoint != null)
            {
                PrevBackBandPoint = pos;
            }
            pos = FrontBandPoint.PositionInWorldSpace;
            if (FrontBandPoint != null)
            {
                PrevFrontBandPoint = pos;
            }
        }

        internal bool IsCurrentlyBanded { get; set; }


        public void Relocate(bool resetPrevBandPoints = true)
        {
            if (resetPrevBandPoints)
            {
                PrevBackBandPoint = BackBandPoint.PositionInWorldSpace;
                PrevFrontBandPoint = FrontBandPoint.PositionInWorldSpace;
            }

            _length = (BackBandPoint.PositionInWorldSpace - FrontBandPoint.PositionInWorldSpace).magnitude;
        }


        // Update is called once per frame
        void Update()
        {
            RefreshPosition();
        }

        internal void RefreshPosition()
        {
            FrontBandPoint.SetPointInWorldSpace(FrontPiece == null ? 0 : _rope.WrapDistance);
            BackBandPoint.SetPointInWorldSpace(BackPiece == null ? 0 : _rope.WrapDistance);

            Relocate(false);
            Vector3 direction = FrontBandPoint.PositionInWorldSpace - BackBandPoint.PositionInWorldSpace;
            transform.position = (FrontBandPoint.PositionInWorldSpace + BackBandPoint.PositionInWorldSpace) / 2;
            if (direction == Vector3.zero) return;
            switch (_rope.ExtendAxis)
            {
                case Axis.X:
                    {
                        transform.rotation = Quaternion.LookRotation(direction) * Quaternion.AngleAxis(90f, Vector3.down);
                        transform.localScale = new Vector3(_rope.PieceInstanceRatio.x * _length, _rope.PieceInstanceRatio.y, _rope.PieceInstanceRatio.z);
                        break;
                    }
                case Axis.Y:
                    {
                        transform.rotation = Quaternion.LookRotation(direction, new Vector3(0, 1, 0)) * Quaternion.AngleAxis(90f, Vector3.right);
                        transform.localScale = new Vector3(_rope.PieceInstanceRatio.x, _rope.PieceInstanceRatio.y * _length, _rope.PieceInstanceRatio.z);
                        break;
                    }
                case Axis.Z:
                    {
                        transform.rotation = Quaternion.LookRotation(direction);
                        transform.localScale = new Vector3(_rope.PieceInstanceRatio.x, _rope.PieceInstanceRatio.y, _rope.PieceInstanceRatio.z * _length);
                        break;
                    }
            }
            if (FrontPiece != null && _bendPointInstance != null)
            {
                _bendPointInstance.transform.localScale = new Vector3(_rope.BendInstanceRatio, _rope.BendInstanceRatio, _rope.BendInstanceRatio);
                _bendPointInstance.transform.position = FrontBandPoint.PositionInWorldSpace;
            }
        }

        internal void Knee(WrapPoint point)
        {
            GameObject pieceObject = _rope.GetPieceInstance();
            Piece newPiece = pieceObject.GetComponent<Piece>();
            if (newPiece == null) return;
            newPiece.Init(point, BackBandPoint, this, BackPiece, _rope, point.PositionInWorldSpace, PrevBackBandPoint, gameObject.layer, false);
            BackBandPoint = point;
            PrevBackBandPoint = point.PositionInWorldSpace;
            newPiece.IsCurrentlyBanded = true;
            IsCurrentlyBanded = true;
            if (_rope != null)
                if (newPiece.BackPiece == null) _rope.BackPiece = newPiece;
            Relocate(false);
        }


        internal Vector3 DefineBackBandPointVelocity()
        {
            return (BackBandPoint.PositionInWorldSpace - PrevBackBandPoint) / Time.fixedDeltaTime;
        }


        internal Vector3 DefineFrontBandPointVelocity()
        {
            return (FrontBandPoint.PositionInWorldSpace - PrevFrontBandPoint) / Time.fixedDeltaTime;
        }


        internal void TransformTexture(Vector2 scale, Vector2 translate)
        {
            if (_workMaterial == null)
                return;

            if (_workMaterial.HasProperty("_MainTex"))
            {
                _workMaterial.SetTextureScale("_MainTex", scale);
                _workMaterial.SetTextureOffset("_MainTex", translate);
            }
            else
            {
#if DEBUG
            var errorTextureName = "Material of piece instance has not texture named '{0}'. Texturing of rope is possible only with Unity's builtin shaders with common texture names.";
            Debug.Log(string.Format(errorTextureName, "_MainTex"));
#endif
            }
            if (_workMaterial.HasProperty("_BumpMap"))
            {
                _workMaterial.SetTextureScale("_BumpMap", scale);
                _workMaterial.SetTextureOffset("_BumpMap", translate);
            }
            else
            {
#if DEBUG
            Debug.Log(string.Format(errorTextureName, "_BumpMap"));
#endif
            }

        }

        [SerializeField]
        protected Vector3[] _baseProfile;
        [SerializeField]
        protected Vector3[] _helpProfile;

        


        protected void SetBaseProfile()
        {
            if (FrontPiece == null)
            {
                Array.Copy(_rope.GetBaseProfile(), _baseProfile, _baseProfile.Length);
                return;
            }
            var vec2 = BackBandPoint.PositionInWorldSpace - FrontBandPoint.PositionInWorldSpace; ;
            var vec1 = FrontPiece.BackBandPoint.PositionInWorldSpace - FrontPiece.FrontBandPoint.PositionInWorldSpace;
            var axis = new Plane(vec1, vec2, Vector3.zero).normal;
            var angle = Geometry.Angle(vec1, vec2, axis);
            Geometry.RotatePoly(_baseProfile, FrontPiece._baseProfile, angle, axis);
        }

        internal void LocateSections(float ropeLength, ref float lengthBefore)
        {
            SharedVertexIndex = null;
            SharedUvLeamVertexIndex = null;
            // !!! Important! Order of methods take sence
            SetBaseProfile();
            LocateMainSections(ropeLength, lengthBefore);
            SetKneeSections();
            lengthBefore = lengthBefore + _length;
        }


        internal List<Node[]> GetSections()
        {
            var res = new List<Node[]>();
            int sectionsCount = BackPiece == null ? 2 : _sections.Count;
            for (var i = 0; i < sectionsCount; i++)
            {
                res.Add(_sections[i]);
            }
            return res;
        }


        protected void LocateMainSections(float ropeLength, float lengthBefore)
        {
            var vec2 = BackBandPoint.PositionInWorldSpace - FrontBandPoint.PositionInWorldSpace;
            _length = vec2.magnitude;
            if (FrontPiece != null)
            {
                _frontExtend = (lengthBefore + FrontPiece._backSectionDistance) / ropeLength;
                var frontSectionPosition = FrontBandPoint.PositionInWorldSpace + vec2.normalized * FrontPiece._backSectionDistance;
                TranslateProfileToSection(_sections[0], _baseProfile, frontSectionPosition, _frontExtend);
            }
            else
            {
                _frontExtend = 0;
                TranslateProfileToSection(_sections[0], _baseProfile, FrontBandPoint.PositionInWorldSpace, _frontExtend);
            }
            if (BackPiece == null)
            {
                _backExtend = 1;
                TranslateProfileToSection(_sections[1], _baseProfile, BackBandPoint.PositionInWorldSpace, _backExtend);
                return;
            }
            var vec1 = BackPiece.BackBandPoint.PositionInWorldSpace - BackPiece.FrontBandPoint.PositionInWorldSpace;
            var cross = Vector3.Cross(vec1, vec2);
            var plane = new Plane(vec2, cross, Vector3.zero);
            var projects = new List<float>();
            Array.ForEach(_baseProfile, point => projects.Add(plane.GetDistanceToPoint(point)));
            
            var maxIndex = GetMaxIndex(projects);
            _maxDistance = maxIndex.Value;
            SharedNodeIndex = maxIndex.Index;
            var middle = Vector3.Lerp(-vec1.normalized, vec2.normalized, 0.5f) * -1;
            var angle = (float)Math.PI * Vector3.Angle(-plane.normal, middle) / 180;
            _backSectionDistance = Mathf.Abs(_maxDistance * Mathf.Tan(angle));
            var backSectionPosition = BackBandPoint.PositionInWorldSpace - vec2.normalized * _backSectionDistance;
            _backExtend = (lengthBefore + _length - _backSectionDistance) / ropeLength;

            TranslateProfileToSection(_sections[1], _baseProfile, backSectionPosition, _backExtend);

            _kneePoint = backSectionPosition + plane.normal * _maxDistance;
            var frontSectionPosition1 = BackBandPoint.PositionInWorldSpace + vec1.normalized * _backSectionDistance;

            _frontBound = backSectionPosition - _kneePoint;
            _backBound = frontSectionPosition1 - _kneePoint;

        }

private MaxIndex GetMaxIndex(List<float> list)
{
    var max = list[0];
    var index = 0;
    int i = 0;
    foreach(var val in list)
    {
        if (val > max)
        {
            max = val;
            index = i;
        }
        i++;
    }
    return new MaxIndex(){ Index = index, Value = max};
}

public class MaxIndex
{
    public int Index { get; set;}
    public float Value { get; set;}
}

        protected Vector3 _kneePoint;

        protected Vector3 _frontBound;
        protected Vector3 _backBound;
        protected float _frontExtend;
        protected float _backExtend;

        public int? SharedVertexIndex = null;
        public int? SharedUvLeamVertexIndex = null;

        protected void TranslateProfileToSection(Node[] section, Vector3[] profile, Vector3 direction, float extend)
        {
            for (var i = 0; i < profile.Length; i++)
            {
                section[i].Vertex = profile[i] + direction;
                section[i].Uv = UVMappper.GetUv(_rope.UVLocation, (float)i / profile.Length, extend);
                section[i].ResetNormals();
            }
        }

        protected void SetKneeSections()
        {
            if (FrontPiece == null)
                return;
            for (float i = 2; i < _sections.Count; i++)
            {
                float position = (i - 1) / (_sections.Count - 1);
                SetMiddleSection(FrontPiece._sections[(int)i], position);
            }
        }


        protected void SetMiddleSection(Node[] targetSection, float amount)
        {
            for (var i = 0; i < targetSection.Length; i++)
            {
                var angle = Vector3.Angle(FrontPiece._frontBound, FrontPiece._backBound) * amount;
                var uv = FrontPiece._backExtend + (_frontExtend - FrontPiece._backExtend) * amount;
                var plane = new Plane(FrontPiece._frontBound, FrontPiece._backBound, Vector3.zero);
                Geometry.RotatePoly(_helpProfile, FrontPiece._baseProfile, angle, plane.normal);
                var dir = Vector3.Slerp(FrontPiece._frontBound, FrontPiece._backBound, amount);
                targetSection[i].Vertex = _helpProfile[i] + FrontPiece._kneePoint + dir;
                targetSection[i].Uv = UVMappper.GetUv(_rope.UVLocation, (float)i / targetSection.Length, uv);
                targetSection[i].ResetNormals();
            }
        }

        protected void DestroyBendPointInstance()
        {
            if (Application.isEditor)
                DestroyImmediate(_bendPointInstance);
            else
                Destroy(_bendPointInstance);
        }


        private bool _isDestroyed = false;

        void OnDestroy()
        {
            DistroyPiece();
        }

        protected void DistroyPiece()
        {
            if (!_isDestroyed)
            {
                if (FrontPiece == null && BackPiece != null)
                {
                    BackPiece.DestroyBendPointInstance();
                }
                DestroyBendPointInstance();
                if (!DontReorganizeWhenDestroy)
                {
                    Reorganize();
                }

                _isDestroyed = true;
            }
        }


        protected void Reorganize()
        {
            if (FrontPiece == null)
            {
                if (BackPiece != null)
                {
                    BackPiece.FrontBandPoint = FrontBandPoint;
                    BackPiece.PrevFrontBandPoint = PrevFrontBandPoint;
                    BackPiece.LastWrapPointPosition = BackBandPoint.PositionInWorldSpace;
                    BackPiece.FrontPiece = null;
                    if (_rope != null)
                        _rope.FrontPiece = BackPiece;
                }
                return;
            }
            FrontPiece.BackBandPoint = BackBandPoint;
            // This need to process anchoring and correct velocity of piece's end
            FrontPiece.PrevBackBandPoint = PrevBackBandPoint;
            FrontPiece.LastWrapPointPosition = FrontBandPoint.PositionInWorldSpace;
            if (BackPiece != null)
            {
                FrontPiece.BackPiece = BackPiece;
                BackPiece.FrontPiece = FrontPiece;
            }
            else
            {
                FrontPiece.BackPiece = null;
                _rope.BackPiece = FrontPiece;
            }
        }


        public void RefreshBendPointInstance(BodyType body)
        {
            if (_rope == null && FrontPiece != null)
                return;
            if (_bendPointInstance != null)
                DestroyBendPointInstance();
            if (body != BodyType.FiniteSegments)
                return;
            _bendPointInstance = _rope.GetBendInstance();
        }


        public override string ToString()
        {
            return string.Format("Piece: {0}", PieceUid);
        }
    }
}