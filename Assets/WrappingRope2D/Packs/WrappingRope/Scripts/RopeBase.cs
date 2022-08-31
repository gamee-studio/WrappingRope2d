#define DEBUG
#undef DEBUG
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using WrappingRopeLibrary.Events;
using UnityEditor;
using UnityEngine;
using WrappingRopeLibrary.ExtensionMethods;
using Ray = UnityEngine.Ray;
using WrappingRopeLibrary.Model;
using WrappingRopeLibrary.Utils;
using WrappingRopeLibrary.Enums;
using WrappingRopeLibrary.Helpers;
using WrappingRopeLibrary.Customization;

namespace WrappingRopeLibrary.Scripts
{

    [ExecuteInEditMode]
    public class RopeBase : MonoBehaviour, ISerializationCallbackReceiver
    {
        [SerializeField]
        private BodyType _body;
        /// <summary>
        /// Gets a body type of the rope.
        /// </summary>
        public BodyType Body { get { return _body; } }


        [SerializeField]
        private Material _material;
        /// <summary>
        /// Gets a material used when Body property set to Continuous.
        /// </summary>
        public Material Material { get { return _material; } }

        [SerializeField]
        private GameObject _frontEnd;
        /// <summary>
        /// Gets a game object used for position of the front end of the rope.
        /// </summary>
        public GameObject FrontEnd
        {
            set { _frontEnd = value; }
            get { return _frontEnd; }
        }


        [SerializeField]
        private GameObject _backEnd;
        /// <summary>
        /// Gets a game object used for position of the back end of the rope.
        /// </summary>
        public GameObject BackEnd
        {
            set { _backEnd = value; }
            get { return _backEnd; }
        }

        /// <summary>
        /// Minimal size of objects, that reliably will be processed in collisions with rope.
        /// </summary>
        public float Threshold;

        /// <summary>
        /// Distance between object surface and rope in wrap zone.
        /// </summary>
        public float WrapDistance;

        [SerializeField]
        private GameObject _pieceInstance;

        [SerializeField]
        private GameObject BendInstance;

        [SerializeField]
        //[Range(0, 100f)]
        private float _width;


        /// <summary>
        /// Gets or sets a width of the rope.
        /// </summary>
        public float Width
        {
            get
            {
                return _width;
            }
            set
            {
                _width = value;
                RefreshWidthDependencies(value);
            }
        }

        private void RefreshWidthDependencies(float value)
        {
            SetPieceInstanceRatio();
            SetBendInstanceRatio();
            SetProfileWidth(value);
        }


        private void SetProfileWidth(float width)
        {
            if (!AllowProcedural())
                return;
            Geometry.ScalePoly(_workProfile, _initProfile.ToArray(), width);
        }


        /// <summary>
        /// The direction of piece instance expansion (in local coordinate system). 
        /// </summary>
        public Axis ExtendAxis;


        /// <summary>
        /// Specifies how texture of material should be expand.
        /// </summary>
        public TexturingMode TexturingMode;


        /// <summary>
        /// Specifies how texture of material should be placed.
        /// </summary>
        public UVLocation UVLocation;


        /// <summary>
        /// Texture tiling.
        /// </summary>
        public float Tiling = 1f;

        [SerializeField]
        private AnchoringMode _anchoringMode;
        /// <summary>
        /// Gets or sets an anchored end of the rope for swinging physics. 
        /// </summary>
        public AnchoringMode AnchoringMode
        {
            get { return _anchoringMode; }
            set
            {
                if (_anchoringMode == value)
                    return;
                _anchoringMode = value;
                if (value != AnchoringMode.None)
                {
                    _initialLength = GetRopeLength();
                }
            }
        }


        /// <summary>
        /// Specifies the degree of elastic physics
        /// </summary>
        public float ElasticModulus = 0;

        private bool _isDestroyed;


        [SerializeField]
        private string _ignoreLayer;


        [SerializeField]
        private LayerMask IgnoreLayer;

        /// <summary>
        /// Maximal number of pieces.
        /// </summary>
        public int MaxPieceCount = 200;

        /// <summary>
        /// If "On" then If two ends of piece are belonging to one object then piece can’t wrap anything.
        /// </summary>
        public bool NotBendOnObject = false;

        [SerializeField]
        private Piece _frontPiece;
        /// <summary>
        /// Gets a first piece in the beginning of the rope.
        /// </summary>
        public Piece FrontPiece
        {
            get { return _frontPiece; }
            internal set { _frontPiece = value; }
        }

        [SerializeField]
        private Piece _backPiece;
        /// <summary>
        /// Gets a last piece of the rope.
        /// </summary>
        public Piece BackPiece
        {
            get { return _backPiece; }
            internal set { _backPiece = value; }
        }

        [SerializeField]
        private Vector3 _pieceInstanceSize;


        [SerializeField]
        private Vector3 _pieceInstanceRatio;
        /// <summary>
        /// Gets a scale of PieceInstance to the width of the rope by two dimensions and 1 unit by third dimension depending on ExtendAxis.
        /// </summary>
        public Vector3 PieceInstanceRatio { get { return _pieceInstanceRatio; } private set { _pieceInstanceRatio = value; } }


        [SerializeField]
        private float _bendInstanceRatio;
        /// <summary>
        /// Gets a uniform scale of BendInstance to the width of the rope. Scale factor is defined by x axis of bounds of BendInstance.
        /// </summary>
        public float BendInstanceRatio
        {
            get { return _bendInstanceRatio; }
            private set { _bendInstanceRatio = value; }
        }


        /// <summary>
        /// Gets a number of cross sections between linear pieces. 
        /// </summary>
        public int BendCrossectionsNumber
        {
            get { return MeshConfiguration.BendCrossectionsNumber; }
        }

        [SerializeField]
        private List<Vector3> _initProfile = new List<Vector3>();

        [SerializeField]
        private Vector3[] _workProfile;


        [SerializeField]
        private Vector3[] _baseProfile;


        private Renderer _rend;


        private MeshFilter _meshFilter;


        private Mesh _mesh;


        [SerializeField]
        private Material _workMaterial;


        protected float _initialLength;


        [SerializeField]
        private MeshConfigurator MeshConfiguration;

        [SerializeField]
        private List<Vector3> _triangulationPath1;


        [SerializeField]
        private List<Vector3> _triangulationPath2;


        /// <summary>
        /// Triggered when the rope is about to wrap a game object
        /// </summary>
        public event ObjectWrapEventHandler ObjectWrap;

        void Awake()
        {
        }

        #region Enabling, Starting, Initializing

        void OnEnable()
        {
            GetComponent<Transform>().hideFlags = HideFlags.HideInInspector;
            if (_deserialize)
            {
                RebuildPieceAndBandPointRelations();
                _deserialize = false;
            }
            _rend = GetComponent<MeshRenderer>();
            if (_rend == null) _rend = gameObject.AddComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();
            if (MeshConfiguration == null)
                MeshConfiguration = new MeshConfigurator() { Profile = Geometry.CreatePolygon(3, Axis.Z, 0.5f, 0f) };
            if (AllowProcedural())
            {
                UpdateMaterial();
                InitProcedural(_baseProfile == null || _baseProfile.Length == 0);
                _mesh = new Mesh();
                _meshFilter.sharedMesh = _mesh;
            }
            if (WrapDistance < 0.0005f) WrapDistance = 0.0005f;
            if (_width < 0.001f) _width = 0.001f;
            if (Threshold < .03f) Threshold = 0.03f;

            CheckAndCorrectIgnoreLayerName();
            SetPieceInstanceRatio();
            SetBendInstanceRatio();
            if (_frontEnd != null && _backEnd != null && FrontPiece == null)
                TryInitPieceSystem();
            if (_anchoringMode != AnchoringMode.None)
            {
                _initialLength = GetRopeLength();
            }
        }


        void Start()
        {
            RebuildPieceAndBandPointRelations();
        }

        protected void RebuildPieceAndBandPointRelations()
        {
            var piece = FrontPiece;
            if (piece == null)
                return;
            int i = 1;
            while (piece.BackPiece != null)
            {

                piece.BackBandPoint = piece.BackPiece.FrontBandPoint;
                piece = piece.BackPiece;
                i++;
            }
        }

        private void InitProcedural(bool resetBaseProfile)
        {
            InitProfile(resetBaseProfile);
        }

        protected void UpdateMaterial()
        {
            if (_material != null)
            {
                _workMaterial = new Material(_material);
                _rend.sharedMaterial = _workMaterial;
                _rend.sharedMaterial.hideFlags = HideFlags.HideInInspector;
            }
            else
            {
                _workMaterial = null;
                _rend.sharedMaterial = null;
            }
        }

        private void InitProfile(bool resetBaseProfile)
        {
            if (MeshConfiguration.Profile.Count < 3)
                throw new ApplicationException("Number of points in profile must be greater then two.");
            _initProfile = MeshConfiguration.Profile.ToList();
            var points = new List<Vector3>();
            _initProfile.ForEach(v => points.Add(v));
            _workProfile = points.ToArray();
            Geometry.ScalePoly(_workProfile, _workProfile, Width);
            // If do not reset profile suggest that base profile match MeshConfiguration.Profile
            if (resetBaseProfile)
            {
                _prevVect = Vector3.forward;
                _rotation = new Quaternion(0, 0, 0, 1);
                _baseProfile = GetWorkProfileClone();
            }
            var triangulator = new Triangulator();
            var profile = MeshConfiguration.Profile.Select(point => (Vector2)point).ToList();
            _triangulationPath1 = triangulator.GetTriangulationIndexes(profile);
            _triangulationPath2 = new List<Vector3>(_triangulationPath1.Count);
            for (var i = _triangulationPath1.Count - 1; i >= 0; i--)
            {
                var triangle = _triangulationPath1[_triangulationPath1.Count - 1 - i];
                _triangulationPath2.Add(new Vector3(triangle.x, triangle.z, triangle.y));
            }
        }

        protected bool TryInitPieceSystem()
        {
            GameObject pieceInstance = GetPieceInstance();
            if (pieceInstance == null)
                return false;
            Piece piece = pieceInstance.GetComponent<Piece>();
            piece = pieceInstance.GetComponent<Piece>();

            SetPieceInstanceRatio();
            FrontPiece = piece;
            BackPiece = piece;
            piece.Init(GetWrapPointByGameObject(_frontEnd), GetWrapPointByGameObject(_backEnd), null, null, this, GetIgnoreLayerForPiece());
            if (AllowProcedural())
                InitProfile(true);
            return true;

        }


        private WrapPoint GetWrapPointByGameObject(GameObject gameObject)
        {
            var point = new WrapPoint();
            point.Origin = gameObject.transform.InverseTransformPoint(gameObject.transform.position);
            point.LocalShift = Vector3.zero;
            point.Parent = gameObject;
            point.PositionInWorldSpace = gameObject.transform.position;
            return point;
        }


        private void SetPieceInstanceRatio()
        {
            if (_pieceInstance == null)
            {
                PieceInstanceRatio = new Vector3(1, 1, 1);
                return;
            }
            var renderer = _pieceInstance.GetComponent<MeshRenderer>();
            if (_body == BodyType.Continuous)
            {
                PieceInstanceRatio = new Vector3(1, 1, 1);
                return;
            }
            _pieceInstanceSize = renderer.bounds.size;
            switch (ExtendAxis)
            {
                case Axis.X:
                    PieceInstanceRatio = new Vector3(1 / _pieceInstanceSize.x, Width / _pieceInstanceSize.y, Width / _pieceInstanceSize.z);
                    break;
                case Axis.Y:
                    PieceInstanceRatio = new Vector3(Width / _pieceInstanceSize.x, 1 / _pieceInstanceSize.y, Width / _pieceInstanceSize.z);
                    break;
                case Axis.Z:
                    PieceInstanceRatio = new Vector3(Width / _pieceInstanceSize.x, Width / _pieceInstanceSize.y, 1 / _pieceInstanceSize.z);
                    break;
            }
        }


        private void SetBendInstanceRatio()
        {
            if (BendInstance == null || AllowProcedural())
            {
                BendInstanceRatio = 1f;
                return;
            }
            var BandInstanceSize = BendInstance.GetComponent<MeshRenderer>().bounds.size.x;
            BendInstanceRatio = Width / BandInstanceSize;
        }

        #endregion

        #region Serialization

        private bool _deserialize;
        public void OnAfterDeserialize()
        {
            _deserialize = true;
        }

        #endregion

        #region CommonMethods

        private GameObject GetPieceInstance(BodyType bodyType)
        {
            GameObject pieceInstance;

            if (AllowProcedural() && bodyType == BodyType.Continuous)
                pieceInstance = new GameObject();
            else if (_pieceInstance != null)
                pieceInstance = Instantiate(_pieceInstance) as GameObject;
            else
            {
                return null;
            }
            pieceInstance.AddComponent<Piece>();
            pieceInstance.transform.parent = transform;
            return pieceInstance;
        }


        internal GameObject GetPieceInstance()
        {
            return GetPieceInstance(_body);
        }


        internal GameObject GetBendInstance()
        {
            if (BendInstance == null)
                return null;
            GameObject bendInstance = Instantiate<GameObject>(BendInstance);
            return bendInstance;
        }


        protected int GetPiecesCount()
        {
            var piece = FrontPiece;
            int cnt = 0;
            do
            {
                cnt++;
                piece = piece.BackPiece;
            } while (piece != null);
            return cnt;
        }


        /// <summary>
        /// Gets the current length of the rope. Be careful: this method iterates through all pieces of the rope.
        /// </summary>
        /// <returns></returns>
        public float GetRopeLength()
        {
            Piece piece = FrontPiece;
            float length = 0;
            while (piece != null)
            {
                length += piece.Length;
                piece = piece.BackPiece;
            }
            return length;
        }

        #endregion

        void LateUpdate()
        {
            if (_body == BodyType.Continuous && FrontPiece != null)
            {
                if (!AllowProcedural())
                {
                    _rend = GetComponent<MeshRenderer>();
                    _meshFilter = GetComponent<MeshFilter>();
                    if (_rend == null || _meshFilter == null)
                    {
                        Debug.LogError("Сontinuous Body need MeshFilter and MeshRenderer.");
                        return;
                    }
                    if (_mesh != null)
                        _meshFilter.sharedMesh = _mesh;
                }
                UpdateProceduralRope();
            }
        }

        #region Procedural Mesh Generation
        [SerializeField]
        private Vector3 _prevVect;
        [SerializeField]
        private Quaternion _rotation = new Quaternion(0, 0, 0, 1);


        private void UpdateProceduralRope()
        {
            float length = 0;
            var piece = FrontPiece;
            UpdateBaseProfile();
            var ropeLength = GetRopeLength();
            do
            {
                piece.LocateSections(ropeLength, ref length);
                piece = piece.BackPiece;
            }
            while (piece != null);

            GenerateMesh();

            Vector2 textureTransform = new Vector2(0, 0);
            Vector2 textureScale;

            float textureRatio = 1f;
            if (_material != null && _workMaterial != null)
            {
                var texture = _workMaterial.mainTexture;
                if (texture != null)
                {
                    textureRatio = ((float)texture.width)/ texture.height;
                }
            }

            if (TexturingMode == TexturingMode.Stretched)
            {
                textureScale = new Vector2(1, 1);
            }
            else
            {
                if (UVLocation == UVLocation.ContraU || UVLocation == UVLocation.AlongU)
                {
                    var scalefactor = ropeLength / (Width * 3) / textureRatio / Tiling;
                    textureScale = new Vector2(scalefactor, 1);
                    if (TexturingMode == TexturingMode.TiledFromBackEnd)
                        textureTransform = new Vector2((float)Math.Truncate(scalefactor) - scalefactor, 0);
                }
                else
                {
                    var scalefactor = ropeLength / (Width * 3) * textureRatio / Tiling;
                    textureScale = new Vector2(1, scalefactor);
                    if (TexturingMode == TexturingMode.TiledFromBackEnd)
                        textureTransform = new Vector2(0, (float)Math.Truncate(scalefactor) - scalefactor);

                }

            }
            TransformTexture(textureScale, textureTransform);

        }


        private void TransformTexture(Vector2 scale, Vector2 translate)
        {
            if (_workMaterial == null)
            {
                return;
            }

            if (_workMaterial.HasProperty("_MainTex"))
            {
                _workMaterial.SetTextureScale("_MainTex", scale);
                _workMaterial.SetTextureOffset("_MainTex", translate);
            }
            else
            {
#if DEBUG
                var errorTextureName = "Material of rope has not texture named '{0}'. Texturing of rope is possible only with Unity's builtin shaders with common texture names.";
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
                var errorTextureName = "Material of rope has not texture named '{0}'. Texturing of rope is possible only with Unity's builtin shaders with common texture names.";
                Debug.Log(string.Format(errorTextureName, "_BumpMap"));
#endif
            }

        }


        private void GenerateMesh()
        {
            var vertices = new List<Vertex>();
            var triangles = new List<int>();
            Node[] section1;
            Node[] section2;
            List<Node[]> sections = new List<Node[]>();
            var piece = FrontPiece;
            int i = 0;

            var piecesCount = GetPiecesCount();
            List<Vector3> newVertices = new List<Vector3>();
            List<Vector2> uvArr = new List<Vector2>();
            float extend;
            do
            {
                var globalSections = piece.GetSections();
                var sectionIndex = 0;
                foreach (var section in globalSections)
                {
                    var frontSharedNodeIndex = -1;
                    var backSharedNodeIndex = -1;
                    if (piece.FrontPiece != null)
                    {
                        frontSharedNodeIndex = piece.FrontPiece.SharedNodeIndex;
                    }
                    if (piece.BackPiece != null)
                    {
                        backSharedNodeIndex = piece.SharedNodeIndex;
                    }
                    for (i = 0; i < section.Length; i++)
                    {
                        if (sectionIndex == 0)
                        {
                            if (i == frontSharedNodeIndex)
                            {
                                section[i].VertexIndex = piece.FrontPiece.SharedVertexIndex.Value;
                            }
                            else
                            {
                                newVertices.Add(transform.InverseTransformPoint(section[i].Vertex));
                                uvArr.Add(section[i].Uv);
                                section[i].VertexIndex = newVertices.Count - 1;
                            }
                            if (i == section.Length - 1)
                            {
                                int uvIndex;
                                if (frontSharedNodeIndex == 0)
                                {
                                    uvIndex = piece.FrontPiece.SharedUvLeamVertexIndex.Value;
                                }
                                else
                                {
                                    if (UVLocation == UVLocation.ContraU || UVLocation == UVLocation.AlongU)
                                        extend = section[i].Uv.x;
                                    else
                                        extend = section[i].Uv.y;
                                    newVertices.Add(transform.InverseTransformPoint(section[0].Vertex));
                                    uvArr.Add(UVMappper.GetUv(UVLocation, 1, extend));
                                    uvIndex = newVertices.Count - 1;
                                }
                                section[0].UvLeamVertexIndex = uvIndex;
                            }

                        }
                        else
                        {
                            if (i == backSharedNodeIndex)
                            {
                                if (piece.SharedVertexIndex.HasValue)
                                {
                                    section[i].VertexIndex = piece.SharedVertexIndex.Value;
                                }
                                else
                                {
                                    newVertices.Add(transform.InverseTransformPoint(section[i].Vertex));
                                    uvArr.Add(section[i].Uv);
                                    piece.SharedVertexIndex = section[i].VertexIndex = newVertices.Count - 1;
                                }
                            }
                            else
                            {
                                newVertices.Add(transform.InverseTransformPoint(section[i].Vertex));
                                uvArr.Add(section[i].Uv);
                                section[i].VertexIndex = newVertices.Count - 1;

                            }
                            if (i == section.Length - 1)
                            {
                                if (backSharedNodeIndex == 0)
                                {
                                    if (piece.SharedUvLeamVertexIndex.HasValue)
                                    {
                                        section[0].UvLeamVertexIndex = piece.SharedUvLeamVertexIndex.Value;
                                    }
                                    else
                                    {
                                        newVertices.Add(transform.InverseTransformPoint(section[0].Vertex));
                                        if (UVLocation == UVLocation.ContraU || UVLocation == UVLocation.AlongU)
                                            extend = section[i].Uv.x;
                                        else
                                            extend = section[i].Uv.y;
                                        uvArr.Add(UVMappper.GetUv(UVLocation, 1, extend));
                                        piece.SharedUvLeamVertexIndex = section[0].UvLeamVertexIndex = newVertices.Count - 1;
                                    }
                                }
                                else
                                {
                                    if (UVLocation == UVLocation.ContraU || UVLocation == UVLocation.AlongU)
                                        extend = section[i].Uv.x;
                                    else
                                        extend = section[i].Uv.y;
                                    newVertices.Add(transform.InverseTransformPoint(section[0].Vertex));
                                    uvArr.Add(UVMappper.GetUv(UVLocation, 1, extend));
                                    section[0].UvLeamVertexIndex = newVertices.Count - 1;
                                }
                            }
                        }
                    }
                    sectionIndex++;
                }
                sections.AddRange(globalSections);
                piece = piece.BackPiece;
            }
            while (piece != null);

            i = 0;
            var flipNormals = MeshConfiguration.FlipNormals;

            var sectionsEnum = sections.GetEnumerator();
            sectionsEnum.MoveNext();
            section1 = sectionsEnum.Current;
            sectionsEnum.MoveNext();

            var verCount = (sections.Count - 1) * 6 * section1.Length + 2 * _triangulationPath1.Count * 3;
            var vertArr = new Vector3[verCount];
            do
            {
                section2 = sectionsEnum.Current;
                // For all points
                for (var j = 0; j < section1.Length; j++)
                {
                    int vInd1, vInd2, vInd3;
                    //float extend;
                    var nextJ = j == section1.Length - 1 ? 0 : j + 1;
                    // Face:
                    // Triangle 1:
                    var normal = new Plane(section1[j].Vertex, section2[j].Vertex, section1[nextJ].Vertex).normal;
                    // In the band point one of face triangle collapses to the line so check this condition and get normal from other triangle
                    if (normal == Vector3.zero)
                    {
                        normal = new Plane(section1[nextJ].Vertex, section2[j].Vertex, section2[nextJ].Vertex).normal;
                        // If triangle 2 collapses to the line, get noraml from previouse face.
                        if (normal == Vector3.zero)
                        {
                            // Normal number is defined by algorithm of collecting normal list of node. 
                            normal = j == 0 ? section1[j].Normals[0] : section1[j].Normals[1];
                        }
                    }

                    // Vertex 1
                    vInd1 = section1[j].VertexIndex;
                    if (!flipNormals)
                    {
                        // Vertex 2
                        vInd2 = section2[j].VertexIndex;
                        // Vertex 3
                        if (nextJ == 0)
                            vInd3 = section1[nextJ].UvLeamVertexIndex;
                        else
                            vInd3 = section1[nextJ].VertexIndex;
                    }
                    else
                    {
                        // Vertex 2
                        if (nextJ == 0)
                            vInd2 = section1[nextJ].UvLeamVertexIndex;
                        else
                            vInd2 = section1[nextJ].VertexIndex;
                        // Vertex 3
                        vInd3 = section2[j].VertexIndex;
                    }
                    if (vInd1 != vInd2 && vInd1 != vInd3 && vInd2 != vInd3)
                    {
                        triangles.Add(vInd1);
                        triangles.Add(vInd2);
                        triangles.Add(vInd3);
                    }
                    section1[j].AddNormal(normal);
                    section1[nextJ].AddNormal(normal);

                    // Triangle 2:
                    // Vertex 1
                    if (nextJ == 0)
                        vInd1 = section1[nextJ].UvLeamVertexIndex;
                    else
                        vInd1 = section1[nextJ].VertexIndex;
                    if (!flipNormals)
                    {
                        // Vertex 2
                        vInd2 = section2[j].VertexIndex;
                        // Vertex 3
                        if (nextJ == 0)
                            vInd3 = section2[nextJ].UvLeamVertexIndex;
                        else
                            vInd3 = section2[nextJ].VertexIndex;
                    }
                    else
                    {
                        // Vertex 2
                        if (nextJ == 0)
                            vInd2 = section2[nextJ].UvLeamVertexIndex;
                        else
                            vInd2 = section2[nextJ].VertexIndex;
                        // Vertex 3
                        vInd3 = section2[j].VertexIndex;
                    }
                    if (vInd1 != vInd2 && vInd1 != vInd3 && vInd2 != vInd3)
                    {
                        triangles.Add(vInd1);
                        triangles.Add(vInd2);
                        triangles.Add(vInd3);
                    }
                    section2[j].AddNormal(normal);
                    section2[nextJ].AddNormal(normal);
                }
                section1 = section2;
                i++;
            }
            while (sectionsEnum.MoveNext());

            var normalArr = new Vector3[newVertices.Count];

            foreach (var section in sections)
            {
                for (i = 0; i < section.Length; i++)
                {
                    var node = section[i];

                    var normal = node.GetAverageNormal();
                    normal = flipNormals ? normal * (-1) : normal;
                    var index = node.VertexIndex;
                    normalArr[index] = normal;
                    if (i == 0)
                    {
                        index = node.UvLeamVertexIndex;
                        normalArr[index] = normal;
                    }
                }
            }

            var normals = normalArr.ToList();
            if (!flipNormals)
            {
                CreateCap(_triangulationPath1, sections[0], 0, newVertices, triangles, uvArr, normals);
                CreateCap(_triangulationPath2, sections[sections.Count - 1], sections.Count - 1, newVertices, triangles, uvArr, normals);
            }
            else
            {
                CreateCap(_triangulationPath2, sections[0], 0, newVertices, triangles, uvArr, normals);
                CreateCap(_triangulationPath1, sections[sections.Count - 1], sections.Count - 1, newVertices, triangles, uvArr, normals);
            }
            if (_meshFilter != null)
            {
                _mesh.Clear();
                _mesh.vertices = newVertices.ToArray();
                _mesh.triangles = triangles.ToArray();
                _mesh.uv = uvArr.ToArray();
                _mesh.normals = normals.ToArray();
            }

        }


        private void CreateCap(List<Vector3> triangulationPath, Node[] section, int sectionIndex, List<Vector3> vertArr, List<int> triangles, List<Vector2> uvArr, List<Vector3> normals)
        {
            // 3 vertices of triangle 1
            var normal = new Plane(section[(int)triangulationPath[0].x - 1].Vertex, section[(int)triangulationPath[0].y - 1].Vertex, section[(int)triangulationPath[0].z - 1].Vertex).normal;
            foreach (var triIndex in triangulationPath)
            {
                var nodeIndex = new int[] { sectionIndex, (int)triIndex.x - 1 };
                vertArr.Add(transform.InverseTransformPoint(section[(int)triIndex.x - 1].Vertex));
                uvArr.Add(new Vector2(0, 0));
                triangles.Add(vertArr.Count - 1);
                normals.Add(normal);
                nodeIndex = new int[] { sectionIndex, (int)triIndex.y - 1 };
                vertArr.Add(transform.InverseTransformPoint(section[(int)triIndex.y - 1].Vertex));
                uvArr.Add(new Vector2(0, 0));
                triangles.Add(vertArr.Count - 1);
                normals.Add(normal);
                nodeIndex = new int[] { sectionIndex, (int)triIndex.z - 1 };
                vertArr.Add(transform.InverseTransformPoint(section[(int)triIndex.z - 1].Vertex));
                uvArr.Add(new Vector2(0, 0));
                triangles.Add(vertArr.Count - 1);
                normals.Add(normal);
            }
        }


        public bool AllowProcedural()
        {
            return _meshFilter != null && _rend != null && MeshConfiguration.Profile.Count > 2;
        }


		private Quaternion _rotation2 = new Quaternion();

        internal void UpdateBaseProfile()
        {
            var vec1 = _prevVect;
            var vec2 = FrontPiece.BackBandPoint.PositionInWorldSpace - FrontPiece.FrontBandPoint.PositionInWorldSpace;
            _rotation2.SetFromToRotation(vec1, vec2);
            if (vec2 == Vector3.zero)
                return;
            _rotation = _rotation2 * _rotation;
            Geometry.RotatePoly(_baseProfile, _workProfile, _rotation);
            _prevVect = vec2;
        }

        internal Vector3[] GetBaseProfile()
        {
            return _baseProfile;

        }


        internal Vector3[] GetWorkProfileClone()
        {
            if (!AllowProcedural())
            {
                return new Vector3[] { };
            }
            var points = new List<Vector3>();
            for (var i = 0; i < _workProfile.Length; i++)
            {
                points.Add(_workProfile[i]);
            }
            return points.ToArray();
        }
        #endregion

        #region MainUpdating

        protected void MainUpdate()
        {
            try
            {
                if (FrontPiece == null)
                    return;
                RelocateWrapPoints();
                // Define actual piece length
                RelocatePieces(FrontPiece, false);
                MergeRope(FrontPiece);
                if (GetPiecesCount() < MaxPieceCount)
                    WrapObjects();
                AnchoreRope();
                ProcessElastic();
                // Need reset previouse piece positon, ??? bat what about piece length???
                RelocatePieces(FrontPiece, true);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        protected void RelocateWrapPoints()
        {
            if (FrontPiece == null)
                return;
            var piece = FrontPiece;
            piece.FrontBandPoint.SetPointInWorldSpace(0);
            while (piece != null)
            {
                if (piece.BackPiece == null)
                    piece.BackBandPoint.SetPointInWorldSpace(0);
                else
                    piece.BackBandPoint.SetPointInWorldSpace(WrapDistance);
                piece = piece.BackPiece;
            }
        }

        private void RelocatePieces(Piece piece, bool resetBackPointPosition)
        {
            do
            {
                piece.Relocate(resetBackPointPosition);
                piece = piece.BackPiece;
            } while (piece != null);
        }

        #endregion

        #region Shrinking

        /// <summary>
        /// Changes the length of the rope. If value of AnchoringMode property is AnchoringMode.None the length of the rope will be only decreased, otherwise the length of the rope can be whether decreased or increased depending on the sign of the length parameter.
        /// </summary>
        /// <param name="length">A difference of rope length before and after changing. If AnchoringMode property is not AnchoringMode.None and parameter value is negative, the length of the rope will be increased. If AnchoringMode property is AnchoringMode.None, negative values are not allowed. Positive value of this parameter means that the length of the rope will be decreased.</param>
        /// <param name="dir">Specifies fixed and movable ends of rope. If AnchoringMode property is not equal to AnchoringMode.None, this value will not have any effect, since the decreasing direction will be determined by the value of AnchoringMode property.</param>
        public void CutRope(float length, Direction dir)
        {
            if (_anchoringMode != AnchoringMode.None)
            {
                if (_initialLength >= length)
                    _initialLength = _initialLength - length;
                return;
            }
            if (length < 0)
            {
#if DEBUG
                Debug.Log("Negative value of length is not possible with AnchoringMode.None.");
#endif
                return;
            }
            CutRopeNotAnchoring(length, dir);
        }

        /// <summary>
        /// Sets the length of the rope. This method will work when the AnchoringMode property is not set to the AnchoringMode.None. 
        /// </summary>
        /// <param name="length">The length of the rope to be set.</param>
        /// <exception cref="ApplicationException"></exception>
        public void SetRopeLength(float length)
        {
            if (length < 0)
                throw new ApplicationException("The length can not be negative!");
            if (_anchoringMode != AnchoringMode.None)
            {
                _initialLength =  length;
            }
        }

        /// <summary>
        /// Shrinks the length of the rope.
        /// </summary>
        /// <param name="length">A difference of rope length before and after shrinking.</param>
        /// <param name="dir">Specifies fixed and movable ends of rope.</param>
        public void CutRopeNotAnchoring(float length, Direction dir = Direction.BackToFront)
        {
            if (length < 0)
            {
                Debug.LogWarning("Negative values of length parameter not allowed.");
                return;
            }
            if (dir == Direction.BackToFront)
            {
                var pieceLength = BackPiece.Length;
                if (pieceLength < length)
                {
                    if (BackPiece.FrontPiece == null)
                    {
                        BackPiece.BackBandPoint.PositionInWorldSpace = BackPiece.FrontBandPoint.PositionInWorldSpace;
                        BackPiece.BackBandPoint.Parent.transform.position = BackPiece.BackBandPoint.PositionInWorldSpace;
                        return;
                    }
                    else
                    {
                        // Shift back end of piece to the front end
                        var newBackBandPointPosition = BackPiece.FrontBandPoint.PositionInWorldSpace;
                        // Remove piece itself
                        var dist = BackPiece.gameObject;
                        BackPiece = BackPiece.FrontPiece;
                        DestroyImmediate(dist);
                        BackPiece.BackBandPoint.PositionInWorldSpace = newBackBandPointPosition;
                        BackPiece.BackBandPoint.Parent.transform.position = newBackBandPointPosition;
                        length = length - pieceLength;
                        CutRopeNotAnchoring(length, dir);
                    }
                }
                else
                {
                    Vector3 pieceDirection = BackPiece.FrontBandPoint.PositionInWorldSpace - BackPiece.BackBandPoint.PositionInWorldSpace;
                    BackPiece.BackBandPoint.PositionInWorldSpace = BackPiece.BackBandPoint.PositionInWorldSpace + pieceDirection.normalized * length;
                    BackPiece.BackBandPoint.Parent.transform.position = BackPiece.BackBandPoint.PositionInWorldSpace;

                    BackPiece.Relocate(false);
                }
            }
            else
            {
                var pieceLength = FrontPiece.Length;
                if (pieceLength < length)
                {
                    if (FrontPiece.BackPiece == null)
                    {
                        FrontPiece.FrontBandPoint.PositionInWorldSpace = FrontPiece.BackBandPoint.PositionInWorldSpace;
                        FrontPiece.FrontBandPoint.Parent.transform.position = FrontPiece.FrontBandPoint.PositionInWorldSpace;
                        return;
                    }
                    else
                    {
                        // Shift fron end of piece to the back end
                        var newFrontBandPointPosition = FrontPiece.BackBandPoint.PositionInWorldSpace;
                        var dist = FrontPiece.gameObject;
                        FrontPiece = FrontPiece.BackPiece;
                        // Remove piece itself
                        DestroyImmediate(dist);
                        FrontPiece.FrontBandPoint.PositionInWorldSpace = newFrontBandPointPosition;
                        FrontPiece.FrontBandPoint.Parent.transform.position = newFrontBandPointPosition;
                        length = length - pieceLength;
                        CutRopeNotAnchoring(length, dir);
                    }
                }
                else
                {
                    Vector3 pieceDirection = FrontPiece.BackBandPoint.PositionInWorldSpace - FrontPiece.FrontBandPoint.PositionInWorldSpace;
                    FrontPiece.FrontBandPoint.PositionInWorldSpace = FrontPiece.FrontBandPoint.PositionInWorldSpace + pieceDirection.normalized * length;
                    FrontPiece.FrontBandPoint.Parent.transform.position = FrontPiece.FrontBandPoint.PositionInWorldSpace;
                    FrontPiece.Relocate(false);
                }
            }
        }

        #endregion

        #region Anchoring

        protected void AnchoreRope()
        {
            float difLength;
            var length = GetRopeLength();
            if (_anchoringMode == AnchoringMode.None || length < _initialLength)
                return;
            difLength = length - _initialLength;
            HoldLength(difLength);
            ProcessPendulum();
        }


        protected void GetPendulumInfo(out Piece piece, out Vector3 pieceDirection, out Rigidbody weight, out Vector3 weightVelocity)
        {
            if (_anchoringMode == AnchoringMode.ByBackEnd)
            {
                pieceDirection = FrontPiece.BackBandPoint.PositionInWorldSpace - FrontPiece.FrontBandPoint.PositionInWorldSpace;
                weight = FrontEnd.GetComponent<Rigidbody>();
                piece = FrontPiece;
                weightVelocity = FrontPiece.DefineFrontBandPointVelocity();
            }
            else
            {
                pieceDirection = BackPiece.FrontBandPoint.PositionInWorldSpace - BackPiece.BackBandPoint.PositionInWorldSpace;
                weight = BackEnd.GetComponent<Rigidbody>();
                piece = BackPiece;
                weightVelocity = BackPiece.DefineBackBandPointVelocity();
            }
        }


        protected void HoldLength(float difLength)
        {
            if (_anchoringMode == AnchoringMode.ByFrontEnd)
            {
                CutRopeNotAnchoring(difLength, Direction.BackToFront);
            }
            else
            {
                CutRopeNotAnchoring(difLength, Direction.FrontToBack);
            }
        }

        protected void ProcessPendulum()
        {
            Vector3 pieceDirection;
            Rigidbody weight;
            Piece endPiece;
            Vector3 weightVeloc;
            GetPendulumInfo(out endPiece, out pieceDirection, out weight, out weightVeloc);
            if (weight == null)
                return;

            var grav = Physics.gravity;
            var force = Vector3.Project(-grav, pieceDirection).magnitude;
            weight.AddForce(-weight.velocity, ForceMode.VelocityChange);
            weight.AddForce(pieceDirection.normalized * force, ForceMode.Acceleration);

            var weightVelocMagn = weightVeloc.magnitude;
            var angle = Vector3.Angle(weightVeloc, pieceDirection);
            // If angle between piece and velocity vector near 90  degrees, suggest pendulum is free suspended
            if (angle > 85 && angle < 95)
            {
                var cross = Vector3.Cross(weightVeloc, pieceDirection);
                // скорость груза направляем перпендикулярно куску
                Vector3.OrthoNormalize(ref pieceDirection, ref cross, ref weightVeloc);
            }
            else
            {
                // otherwise suggest that pendulum is pulled, i.e. do not change velocity direction (velocity vector match pull direction), just normalize
                weightVeloc.Normalize();
            }
            weight.AddForce(weightVeloc * weightVelocMagn, ForceMode.VelocityChange);
        }

        #endregion

        #region Elastic

        protected void ProcessElastic()
        {
            if (ElasticModulus == 0)
                return;
            var piece = FrontPiece;
            while ((piece != null) && piece.BackPiece != null)
            {
                var ropeInteraction = piece.BackBandPoint.Parent.GetComponent<IRopeInteraction>();
                if (ropeInteraction != null)
                {
                    var force = (piece.FrontBandPoint.PositionInWorldSpace - piece.BackBandPoint.PositionInWorldSpace).normalized + (piece.BackPiece.BackBandPoint.PositionInWorldSpace - piece.BackPiece.FrontBandPoint.PositionInWorldSpace).normalized;
                    ropeInteraction.AddForceAtPosition(force * ElasticModulus, piece.BackBandPoint.PositionInWorldSpace, ForceMode.Impulse);

                }
                else
                {
                    var rBody = (Rigidbody)piece.BackBandPoint.Parent.GetComponent<Rigidbody>();
                    if (rBody != null)
                    {
                        var force = (piece.FrontBandPoint.PositionInWorldSpace - piece.BackBandPoint.PositionInWorldSpace).normalized + (piece.BackPiece.BackBandPoint.PositionInWorldSpace - piece.BackPiece.FrontBandPoint.PositionInWorldSpace).normalized;
                        rBody.AddForceAtPosition(force * ElasticModulus, piece.BackBandPoint.PositionInWorldSpace, ForceMode.Impulse);
                    }
                }

                piece = piece.BackPiece;
            }
        }

        #endregion

        #region Merging

        private void MergeRope(Piece piece)
        {

            if (piece.BackPiece == null) 
            {
                piece.IsCurrentlyBanded = false;
                return;
            }
                
            if (!piece.IsCurrentlyBanded && !piece.BackPiece.IsCurrentlyBanded)
            {
                // For optimization check angle only if pieces are moving, or the parent of band point is not exist
                if (IsMoveEndsOfKnee(piece) || (piece.BackBandPoint.Parent == null))
                {
                    var isKnee = CheckKnee(piece.FrontBandPoint, piece.BackBandPoint, piece.BackPiece.BackBandPoint);
                    if (!isKnee)
                    {
                        MergePieces(piece, piece.BackPiece);
                        MergeRope(piece);
                    }
                }
            }
            piece.IsCurrentlyBanded = false;
            if (piece.BackPiece != null) MergeRope(piece.BackPiece);
        }

        /// <summary>
        /// Check the motion of piece and naighbour piece 
        /// </summary>
        /// <param name="piece">Piece</param>
        /// <returns></returns>
        private bool IsMoveEndsOfKnee(Piece piece)
        {
            return (piece.FrontBandPoint.Parent != piece.BackBandPoint.Parent
                    && (piece.DefineBackBandPointVelocity() != Vector3.zero
                            || piece.DefineFrontBandPointVelocity() != Vector3.zero))
                    ||
                    (piece.BackBandPoint.Parent != piece.BackPiece.BackBandPoint.Parent
                    && (piece.DefineBackBandPointVelocity() != Vector3.zero
                            || piece.BackPiece.DefineBackBandPointVelocity() != Vector3.zero));
        }

        private void MergePieces(Piece piece1, Piece piece2)
        {
            try
            {

                if (piece2.BackPiece == null)
                {

                    BackPiece = piece1;
                }
                DestroyImmediate(piece2.gameObject);
                piece1.Relocate(false);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }


        private bool CheckKnee(WrapPoint endPoint1, WrapPoint bendPoint, WrapPoint endPoint2)
        {
            if (bendPoint.Parent == null) return false;
            Vector3 direct1 = (endPoint1.PositionInWorldSpace - bendPoint.PositionInWorldSpace).normalized;
            Vector3 direct2 = (endPoint2.PositionInWorldSpace - bendPoint.PositionInWorldSpace).normalized;
            var end1 = bendPoint.PositionInWorldSpace;
            var direction1 = (direct2 + direct1).normalized;
            var direction2 = endPoint2.PositionInWorldSpace - endPoint1.PositionInWorldSpace;
            var controlDistance = WrapDistance + 0.03f;
            var ang = (double)(Math.PI * Vector3.Angle(direction1 * controlDistance, direct1) / 180.0);
            var sin = Math.Sin(ang);
            var len = (float)(controlDistance / Math.Sqrt(1 - sin * sin));
            var dir1 = direct1 * len;
            var dir2 = direct2 * len;
            var or1 = end1 + dir1;
            var dir3 = dir2 - dir1;
#if DEBUG
            Debug.DrawRay(or1, dir3, Color.red, 0);
            Debug.DrawRay(or1, dir1, Color.red, 0);
#endif
#if DEBUG
            Debug.DrawRay(end1, direction1, Color.green, 0);
#endif
            HitInfo hitInfo;
            try
            {
                if (!dir3.Equals(Vector3.zero)
                        && Geometry.TryRaycast(new Ray(or1, Vector3.Normalize(dir3)), bendPoint.Parent.gameObject, dir3.magnitude, out hitInfo)
                    )
                    return true;
                if (!direction1.Equals(Vector3.zero)
                        && Geometry.TryRaycast(new Ray(end1, direction1), bendPoint.Parent.gameObject, direction1.magnitude * 1.2f, out hitInfo)
                    )
                    return true;
                if (
                        Geometry.TryRaycast(new Ray(endPoint1.PositionInWorldSpace, direction2.normalized), bendPoint.Parent.gameObject, direction2.magnitude, out hitInfo)
                    )
                {
                    return true;
                }

            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            return false;
        }

        #endregion

        #region Banding


       private void BandRope(Piece sourcePiece, IEnumerable<GameObject> exclude, int recurtionCount)
        {

            if (recurtionCount > 30)
            {
#if DEBUG
                Debug.Log("RecurtionCount > 10!!! Return to avoid infinite cycle");
#endif
                return;
            }
            HitInfo hitInfo1, hitInfo2;
            bool isIntersect = false;
            var pieceInfo = new PieceInfo(sourcePiece);
            var sourcePieceInfo = new PieceInfo(sourcePiece);
            if (TryGetIntersect(ref pieceInfo, exclude, out hitInfo1, out hitInfo2))
            {
                try
                {
                    var wrapper = new GameObjectWraper(pieceInfo, hitInfo1, hitInfo2);
                    var wrapPoints = wrapper.GetWrapPoints();
                    var piece = sourcePiece;
                    if (wrapPoints.Count > 0)
                    {
                        // Check that newly created pieces intersect the object. This may be due to wrong wrap path
                        isIntersect = IsIntersection(sourcePiece.FrontBandPoint.PositionInWorldSpace, wrapPoints[0].PositionInWorldSpace, hitInfo1.GameObject) ||
                            IsIntersection(sourcePiece.BackBandPoint.PositionInWorldSpace, wrapPoints[wrapPoints.Count - 1].PositionInWorldSpace, hitInfo1.GameObject);
                        if (!isIntersect)
                        {
                            if (wrapPoints.Count > 1)
                            {
                                for (var i = 0; i < wrapPoints.Count - 1; i++)
                                {
                                    if (IsIntersection(wrapPoints[i].PositionInWorldSpace, wrapPoints[i + 1].PositionInWorldSpace, hitInfo1.GameObject))
                                    {
                                        isIntersect = true;
#if DEBUG
                                        Debug.LogWarning("One or more pieces created from wrap path intersect game object. Wrapping will be canceled");
#endif
                                        break;
                                    }
                                }
                            }
                            var widthSqr = _width * _width;
                            if (!isIntersect)
                            {
                                var bandPoints = new List<WrapPoint>();
                                var prevPoint = piece.FrontBandPoint;
                                foreach (var point in wrapPoints)
                                {
                                    if (point.PositionInWorldSpace != prevPoint.PositionInWorldSpace)
                                    {
                                        bandPoints.Add(point);
                                    }
                                    prevPoint = point;
                                }
                                if (bandPoints.Count > 0)
                                {
                                    bool cancelWrapping = false;
                                    OnObjectWrapping(hitInfo1.GameObject, bandPoints.Select(p => p.PositionInWorldSpace).ToArray(), out cancelWrapping);
                                    if (!cancelWrapping && isKnee)
                                    {
#if DEBUG
                                        if (recurtionCount > 0)
                                            Debug.Log("Recurtioned wrap with recurtionCount=" + recurtionCount.ToString());
#endif
                                        var fronPiece = piece;
                                        foreach (var point in bandPoints)
                                        {
                                            KneePiece(piece, point, hitInfo1.GameObject);
                                            piece = piece.BackPiece;
                                        }

                                        var objectVelocity = GetVelocity(fronPiece.BackBandPoint.Parent, fronPiece.BackBandPoint.PositionInWorldSpace);
                                        var pieceVelocity = GetVelocityInPoint(sourcePieceInfo, fronPiece.BackBandPoint.PositionInWorldSpace);

                                        fronPiece.PrevBackBandPoint = fronPiece.BackBandPoint.PositionInWorldSpace - (objectVelocity + pieceVelocity) * Time.fixedDeltaTime;

                                        var backPiece = piece;
                                        objectVelocity = GetVelocity(backPiece.FrontBandPoint.Parent, backPiece.FrontBandPoint.PositionInWorldSpace);
                                        pieceVelocity = GetVelocityInPoint(sourcePieceInfo, backPiece.FrontBandPoint.PositionInWorldSpace);
                                        backPiece.PrevFrontBandPoint = backPiece.FrontBandPoint.PositionInWorldSpace - (objectVelocity + pieceVelocity) * Time.fixedDeltaTime;
                                        List<GameObject> frontExclude = new List<GameObject>(exclude);
                                        List<GameObject> backExclude = new List<GameObject>(exclude);
                                        frontExclude.Add(fronPiece.BackBandPoint.Parent);
                                        backExclude.Add(backPiece.FrontBandPoint.Parent);
                                        BandRope(fronPiece, frontExclude, recurtionCount++);
                                        BandRope(backPiece, backExclude, recurtionCount++);
                                    }
                                }
                            }
                        }
                        else
                        {
#if DEBUG
                            Debug.Log("IsIntersect at the ends after wrap!");
                            Debug.DrawLine(sourcePiece.FrontBandPoint.PositionInWorldSpace, wrapPoints[0].PositionInWorldSpace, Color.blue, 0.5f);
                            for (var i = 1; i < wrapPoints.Count; i++)
                            {
                                Debug.DrawLine(wrapPoints[i].PositionInWorldSpace, wrapPoints[i - 1].PositionInWorldSpace, Color.blue, 0.5f);
                            }
                            Debug.DrawLine(sourcePiece.BackBandPoint.PositionInWorldSpace, wrapPoints.Last().PositionInWorldSpace, Color.blue, 0.5f);

#endif
                        }

                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private Vector3 GetVelocity(GameObject o, Vector3 point)
        {
            var velocity = Vector3.zero;
            // Priority for object implemented interface IRopeInteraction
            var ropeInteraction = gameObject.GetComponent<IRopeInteraction>();
            if (ropeInteraction != null)
            {
                velocity = ropeInteraction.GetPointVelocity(point);
            }
            else 
            {
                var rigidBody = o.GetComponent<Rigidbody>();
                if(rigidBody != null)
                    velocity = rigidBody.GetPointVelocity(point);
            }
            return velocity;
        }


        private Vector3 GetVelocityInPoint(PieceInfo pieceInfo, Vector3 point)
        {
            var frontVeloc = (pieceInfo.FrontBandPoint - pieceInfo.PrevFrontBandPoint) / Time.fixedDeltaTime; //piece.DefineFrontBandPointVelocity();
            var backVeloc = (pieceInfo.BackBandPoint - pieceInfo.PrevBackBandPoint) / Time.fixedDeltaTime;
            var ratio = Mathf.Abs((pieceInfo.FrontBandPoint - point).magnitude / (pieceInfo.BackBandPoint - point).magnitude);
            return Vector3.Lerp(frontVeloc, backVeloc, ratio);
        }


        private void WrapObjects()
        {
            var piece = FrontPiece;
            while (piece != null)
            {
                // save real PrevFrontBandPoint and PrevBackBandPoint position is need to process anchoring and correct velocity one of piece's ends
                var prevFrontBandPoint = piece.PrevFrontBandPoint; 
                var prevBackBandPoint = piece.PrevBackBandPoint; 

                // If piece is merge result, set previous position to last wrap point for imitation of fast movement 
                if (piece.LastWrapPointPosition != null)
                {
                    piece.PrevFrontBandPoint = piece.LastWrapPointPosition.Value;
                    piece.PrevBackBandPoint = piece.LastWrapPointPosition.Value;
                }
                // Remember the next piece, because after band the BackPiece will be newly created piece which no need to process in this update
                // Sometime when next piece getted from BackPiece after band, the infinite cycle is performed. The core of this problem is not that
                // next piece is newly created piece but that this newly created piece band again and again in BandRope method that is not correct
                var nextPiece = piece.BackPiece;
                BandRope(piece, new List<GameObject>(), 0);
                piece.LastWrapPointPosition = null;

                // restore real PrevFrontBandPoint and PrevBackBandPoint position is need to process anchoring and correct velocity one of piece's end
                piece.PrevFrontBandPoint = prevFrontBandPoint; 
                piece.PrevBackBandPoint = prevBackBandPoint;
                piece = nextPiece;
            }
        }

        private void OnObjectWrapping(GameObject gameObject, Vector3[] wrapPoints, out bool cancel)
        {
            var args = new ObjectWrapEventArgs(gameObject, wrapPoints);
            if (ObjectWrap != null)
                ObjectWrap(this, args);
            cancel = args.Cancel;
        }

        private bool IsIntersection(Vector3 startPoint, Vector3 stopPoint, GameObject gameObject)
        {
            var dir = stopPoint - startPoint;
            if (stopPoint == startPoint)
                return false;
            Ray ray = new Ray() { origin = startPoint, direction = dir.normalized };
            HitInfo hitInfo;
            return Geometry.TryRaycast(ray, gameObject, dir.magnitude, out hitInfo);
        }

        private bool isKnee = true;
        public bool IsKnee { set => isKnee = value; get => isKnee; }
        private void KneePiece(Piece piece, WrapPoint point, GameObject gameObject)
        {
            piece.Knee(point);
        }

        private bool TryGetIntersect(ref PieceInfo pieceInfo, IEnumerable<GameObject> exclude, out HitInfo hitInfo1, out HitInfo hitInfo2)
        {
            hitInfo1 = new HitInfo();
            hitInfo2 = new HitInfo();
            // Optimiztion: if both ends of piece connected to same object, do not process collision
            if (NotBendOnObject && pieceInfo.Piece.BackBandPoint.Parent == pieceInfo.Piece.FrontBandPoint.Parent)
                return false;
            int layer = GetActionLayer();
            int stageCounter = 0;
            var pieceMoveStages = GetPieceMoveStages(pieceInfo);
            foreach (var stage in pieceMoveStages)
            {
                stageCounter++;
                var needCheckSameObject = pieceInfo.Piece.BackBandPoint.Parent != pieceInfo.Piece.FrontBandPoint.Parent && stageCounter == pieceMoveStages.Count;
                if (needCheckSameObject &&
                   (
                        (pieceInfo.Piece.BackPiece != null && Geometry.TryRaycast(new Ray(stage[0], stage[1] - stage[0]), pieceInfo.Piece.BackBandPoint.Parent, (stage[1] - stage[0]).magnitude, out hitInfo1))
                        || (pieceInfo.Piece.FrontPiece != null && Geometry.TryRaycast(new Ray(stage[0], stage[1] - stage[0]), pieceInfo.Piece.FrontBandPoint.Parent, (stage[1] - stage[0]).magnitude, out hitInfo1))
                   )
                   ||
                   Geometry.RaycastClosed(new Ray(stage[0], stage[1] - stage[0]), out hitInfo1, (stage[1] - stage[0]).magnitude, layer, exclude)
                   )
                {
                    if (stageCounter != pieceMoveStages.Count && ((pieceInfo.Piece.BackBandPoint.Parent != null && hitInfo1.GameObject == pieceInfo.Piece.BackBandPoint.Parent) || (pieceInfo.Piece.FrontBandPoint.Parent != null && hitInfo1.GameObject == pieceInfo.Piece.FrontBandPoint.Parent)))
                        continue;
                    // if other end of piece inside the object that piece collide with, do not accept collision
                    var ray = new Ray(stage[1], stage[0] - stage[1]);
                    if (!Geometry.TryRaycast(ray, hitInfo1.GameObject, (stage[0] - stage[1]).magnitude, out hitInfo2))
                    {
#if DEBUG
                        Debug.Log(string.Format("Inner Piece: {0}", pieceInfo.Piece));
#endif
                        if (pieceInfo.Piece.BackPiece == null)
                            return false;
                        else
                            continue;
                    }
                    pieceInfo.FrontBandPoint = stage[0];
                    pieceInfo.BackBandPoint = stage[1];
                    return true;
                }
            }
            return false;
        }


        private List<Vector3[]> GetPieceMoveStages(PieceInfo pieceInfo)
        {
            var stages = new List<Vector3[]>();
            var frontShift = (pieceInfo.PrevFrontBandPoint - pieceInfo.FrontBandPoint).sqrMagnitude;
            var backShift = (pieceInfo.PrevBackBandPoint - pieceInfo.BackBandPoint).sqrMagnitude;
            float i;
            Vector3 frontStep;
            Vector3 backStep;
            var sqrThreshold = Threshold * Threshold;
            if (frontShift > sqrThreshold || backShift > sqrThreshold)
            {
                if (frontShift > backShift)
                {
                    i = (float)Math.Sqrt(frontShift) / Threshold;
                    frontStep = (pieceInfo.PrevFrontBandPoint - pieceInfo.FrontBandPoint).normalized * Threshold;
                    backStep = (pieceInfo.PrevBackBandPoint - pieceInfo.BackBandPoint) / (i);
                }
                else
                {
                    i = (float)Math.Sqrt(backShift) / Threshold;
                    backStep = (pieceInfo.PrevBackBandPoint - pieceInfo.BackBandPoint).normalized * Threshold;
                    frontStep = (pieceInfo.PrevFrontBandPoint - pieceInfo.FrontBandPoint) / (i);
                }

                var frontBandPointStage = pieceInfo.PrevFrontBandPoint;
                var backBandPointStage = pieceInfo.PrevBackBandPoint;
                for (int j = 1; j < (int)i + 1; j++)
                {
                    frontBandPointStage = frontBandPointStage - frontStep;
                    backBandPointStage = backBandPointStage - backStep;
                    stages.Add(new[] { frontBandPointStage, backBandPointStage });
                }
            }
            stages.Add(new[] { pieceInfo.FrontBandPoint, pieceInfo.BackBandPoint });
            return stages;
        }

        #endregion

        #region IgnoreLayer

        protected int GetActionLayer()
        {
            return ~IgnoreLayer;
        }


        private int GetIgnoreLayerForPiece()
        {
            var ignoreLayerBits = new BitArray(new[] { IgnoreLayer.value });
            int firsLayerNmb = 0;
            for (var i = 0; i < ignoreLayerBits.Length; i++)
            {
                if (ignoreLayerBits[i])
                {
                    firsLayerNmb = i;
                    break;
                }
            }
            return firsLayerNmb;
        }

        private void CheckAndCorrectIgnoreLayerName()
        {
            // Check the layer from new property
            if (IgnoreLayer.value != 0 && IgnoreLayer.value != -1)
                return;
            // If layer from new property invalid take value from old property
            var oldLayer = GetAndCorrectLayerFormString();
            IgnoreLayer = oldLayer;
        }

        private int GetAndCorrectLayerFormString()
        {
            bool isCorrect = true;
            string warning = string.Empty;
            if (string.IsNullOrEmpty(_ignoreLayer))
            {
                warning = "IgnoreLayer not setted.";
                isCorrect = false;
            }
            else if (_ignoreLayer == "Default")
            {
                warning = "IgnoreLayer couldn't be 'Default' layer.";
                isCorrect = false;
            }
            else if (LayerMask.NameToLayer(_ignoreLayer) == -1)
            {
                warning = "IgnoreLayer not exists in layers list.";
                isCorrect = false;
            }
            if (!isCorrect)
            {
                Debug.LogWarning(string.Format("{0} IgnoreLayer will be setted to 'Ignore Raycast'.", warning));
                _ignoreLayer = "Ignore Raycast";
            }
            return 1 << LayerMask.NameToLayer(_ignoreLayer);
        }
        #endregion

        #region Texturing

        protected float GetStretchAmount()
        {
            switch (ExtendAxis)
            {
                case Axis.X:
                    return PieceInstanceRatio.x / PieceInstanceRatio.y;
                case Axis.Y:
                    return PieceInstanceRatio.y / PieceInstanceRatio.x;
                case Axis.Z:
                    return PieceInstanceRatio.z / PieceInstanceRatio.x;
            }
            return 1f;
        }


        protected void Texturing()
        {
            switch (TexturingMode)
            {
                case TexturingMode.None:
                    ResetTexture();
                    break;
                case TexturingMode.Stretched:
                    {
                        if (UVLocation == UVLocation.ContraU || UVLocation == UVLocation.ContraV)
                            TexturingMode5(UVLocation);
                        else
                            TexturingMode6(UVLocation);
                        break;
                    }
                case TexturingMode.TiledFromFrontEnd:
                    {
                        if (UVLocation == UVLocation.ContraU || UVLocation == UVLocation.ContraV)
                            TexturingMode4(UVLocation);
                        else
                            TexturingMode2(UVLocation);

                        break;
                    }
                case TexturingMode.TiledFromBackEnd:
                    {
                        if (UVLocation == UVLocation.ContraU || UVLocation == UVLocation.ContraV)
                            TexturingMode3(UVLocation);
                        else
                            TexturingMode1(UVLocation);

                        break;
                    }
            }
        }

        private void ResetTexture()
        {
            Piece piece = BackPiece;
            if (piece == null) return;
            do
            {
                piece.TransformTexture(new Vector2(1, 1), new Vector2(0, 0));
                piece = piece.FrontPiece;
            }
            while (piece != null);
        }

        // Fixed - Back, UV - Along U (left to right), Along V (bottom to top)
        private void TexturingMode1(UVLocation uvLocation)
        {
            Piece piece = BackPiece;
            if (piece == null) return;
            float ratio = GetStretchAmount();
            float length = 0;
            var scale = new Vector2();
            var translate = new Vector2();
            do
            {
                var translateFactor = length * ratio / Tiling;
                if (uvLocation == UVLocation.AlongU)
                {
                    scale = new Vector2(piece.Length * ratio / Tiling, 1);
                    translate = new Vector2(translateFactor - (float)Math.Truncate(translateFactor), 0);
                }
                else
                {
                    scale = new Vector2(1, piece.Length * ratio / Tiling);
                    translate = new Vector2(0, translateFactor - (float)Math.Truncate(translateFactor));
                }

                piece.TransformTexture(scale, translate);
                length += piece.Length;
                piece = piece.FrontPiece;
            }
            while (piece != null);
        }

        // Fixed - Front, UV - Along U (left to right), Along V (bottom to top)
        private void TexturingMode2(UVLocation uvLocation)
        {
            Piece piece = FrontPiece;
            if (piece == null) return;
            float ratio = GetStretchAmount();
            float length = 0;
            var scale = new Vector2();
            var translate = new Vector2();
            do
            {
                length += piece.Length;
                var translateFactor = length * ratio / Tiling;
                if (uvLocation == UVLocation.AlongU)
                {
                    scale = new Vector2(piece.Length * ratio / Tiling, 1);
                    translate = new Vector2(1 - translateFactor - (float)Math.Truncate(translateFactor), 0);
                }
                else
                {
                    scale = new Vector2(1, piece.Length * ratio / Tiling);
                    translate = new Vector2(0, 1 - translateFactor - (float)Math.Truncate(translateFactor));
                }
                piece.TransformTexture(scale, translate);
                piece = piece.BackPiece;
            }
            while (piece != null);
        }

        // Fixed - Back, UV - Contra U (right to left), Contra V (top to bottom)
        private void TexturingMode3(UVLocation uvLocation)
        {
            Piece piece = BackPiece;
            if (piece == null) return;
            float ratio = GetStretchAmount();
            float length = 0;
            var scale = new Vector2();
            var translate = new Vector2();

            do
            {
                length += piece.Length;
                var translateFactor = length * ratio / Tiling;
                if (uvLocation == UVLocation.ContraU)
                {
                    scale = new Vector2(piece.Length * ratio / Tiling, 1);
                    translate = new Vector2(1 - translateFactor - (float)Math.Truncate(translateFactor), 0);
                }
                else
                {
                    scale = new Vector2(1, piece.Length * ratio / Tiling);
                    translate = new Vector2(0, 1 - translateFactor - (float)Math.Truncate(translateFactor));
                }
                piece.TransformTexture(scale, translate);
                piece = piece.FrontPiece;
            }
            while (piece != null);
        }


        // Fixed - Front, UV - Contra U (right to left), Contra V (top to bottom)
        private void TexturingMode4(UVLocation uvLocation)
        {
            Piece piece = FrontPiece;
            if (piece == null) return;
            float ratio = GetStretchAmount();
            float length = 0;
            var scale = new Vector2();
            var translate = new Vector2();
            do
            {
                var translateFactor = length * ratio / Tiling;
                if (uvLocation == UVLocation.ContraU)
                {
                    scale = new Vector2(piece.Length * ratio / Tiling, 1);
                    translate = new Vector2(translateFactor - (float)Math.Truncate(translateFactor), 0);
                }
                else
                {
                    scale = new Vector2(1, piece.Length * ratio / Tiling);
                    translate = new Vector2(0, translateFactor - (float)Math.Truncate(translateFactor));
                }
                piece.TransformTexture(scale, translate);
                length += piece.Length;
                piece = piece.BackPiece;
            }
            while (piece != null);
        }


        // Contra U, Contra V
        private void TexturingMode5(UVLocation uvLocation)
        {
            Piece piece = BackPiece;
            if (piece == null) return;
            float length = 0;
            float totalLength = GetRopeLength();
            var scale = new Vector2();
            var translate = new Vector2();
            do
            {
                length += piece.Length;

                if (uvLocation == UVLocation.ContraU)
                {
                    scale = new Vector2(piece.Length / totalLength, 1);
                    translate = new Vector2(1 - length / totalLength, 0);
                }
                else
                {
                    scale = new Vector2(1, piece.Length / totalLength);
                    translate = new Vector2(0, 1 - length / totalLength);
                }

                piece.TransformTexture(scale, translate);
                piece = piece.FrontPiece;
            }
            while (piece != null);
        }

        private void TexturingMode6(UVLocation uvLocation)
        {
            Piece piece = BackPiece;
            if (piece == null) return;
            float length = 0;
            float totalLength = GetRopeLength();
            var scale = new Vector2();
            var translate = new Vector2();
            do
            {
                if (uvLocation == UVLocation.AlongU)
                {
                    scale = new Vector2(piece.Length / totalLength, 1);
                    translate = new Vector2(length / totalLength, 0);
                }
                else
                {
                    scale = new Vector2(1, piece.Length / totalLength);
                    translate = new Vector2(0, length / totalLength);
                }
                piece.TransformTexture(scale, translate);
                length += piece.Length;
                piece = piece.FrontPiece;
            }
            while (piece != null);
        }


        #endregion

        #region Destroying

        void OnDestroy()
        {
            if (Application.isEditor)
                DestroyRopeInEditor();

            else
                DestroyRope();
        }


        protected void DestroyRope()
        {
            if (_isDestroyed)
                return;
            Piece piece = FrontPiece;
            piece.FrontBandPoint = null;
            do
            {
                if (piece.gameObject != null)
                    Destroy(piece.gameObject);
                piece = piece.BackPiece;

            }
            while (piece != null);
            FrontPiece = null;
            if (BackPiece != null)
                BackPiece.BackBandPoint = null;

        }


        protected void DestroyRopeInEditor()
        {
            if (_isDestroyed)
                return;
            Piece piece = FrontPiece;
            if (piece != null)
                piece.FrontBandPoint = null;
            while (piece != null)
            {
                piece.DontReorganizeWhenDestroy = true;
                if (piece.gameObject != null)
                    DestroyImmediate(piece.gameObject);
                piece = piece.BackPiece;
            }
            FrontPiece = null;
            if (BackPiece != null)
                BackPiece.BackBandPoint = null;
        }


        void OnApplicationQuit()
        {
            OnDestroy();
            _isDestroyed = true;
        }

        public void OnBeforeSerialize()
        {
        }

        #endregion

    }

}
