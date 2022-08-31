using System;
using System.Collections.Generic;
using Vectrosity;
using WrappingRopeLibrary.Scripts;
using UnityEngine;


namespace Gamee_Hiukka.WrappingRope2D
{
    public class Rope2D : MonoBehaviour
    {
        public float capLineWidth;
        public VectorLine line;
        public Texture2D lineTexture;
        public Texture2D lineTexturePullBackTarget;
        public Color lineColor;
        public Rope rope;
        public float distanceRope;
        public int sortingOrder = 3;
        public EdgeCollider2D edgeCollider2D;
        public List<Vector3> WayPoints => wayPoints;
        public VectorLine Line => line;

        HandDrag handDrag;
        Collision2DComponent collision;
        Vector2[] edgePointDraw = new Vector2[10];

        float aspectRatio = 1f;
        float capLineWidthDefaut = 20f;
        float capLineWidthPullBack = 15f;
        public void Init(HandDrag handDrag)
        {
            this.handDrag = handDrag;
            collision = this.GetComponentInChildren<Collision2DComponent>();
        }
        public float Length
        {
            get
            {
                float length = 0f;
                for (int i = 1; i < wayPoints.Count; i++)
                {
                    length += (wayPoints[i] - wayPoints[i - 1]).magnitude;
                }
                return length;
            }
        }

        bool rending;
        public List<Vector3> wayPoints = new List<Vector3>();
        private bool _oneFrame;
        private Color lineColorCurrent = new Color();

        public void CreateLine(string name)
        {
            aspectRatio = Screen.width / 1080f;
            capLineWidthDefaut = capLineWidth;
            line = new VectorLine(name,
                new List<Vector3>(),
                lineTexture,
                capLineWidth * aspectRatio,
                LineType.Continuous,
                Joins.Weld);
            line.textureScale = 1f;
            line.drawDepth = 2;
            lineColorCurrent = lineColor;
            line.Draw();
            Calculate();
        }

        public void SetCameraCanvasLine(Camera cam = null)
        {
            if (cam == null) cam = Camera.main;
            VectorLine.SetCanvasCamera(cam);
            VectorLine.canvas.sortingOrder = sortingOrder;
        }
        public void SetBackEndPositionStart(Vector3 pos)
        {
            rope.BackEnd.transform.position = pos;
        }
        public void UpdateBackEndPositionStart(Vector3 pos)
        {
            rope.BackEnd.transform.position += pos;
        }

        public void SetFrontEndPositionStart(Vector3 pos)
        {
            rope.FrontEnd.transform.position = pos;
        }
        public void SetFontEnd(GameObject frontEnd) { rope.FrontEnd = frontEnd; }
        public void SetBacktEnd(GameObject backEnd) { rope.BackEnd = backEnd; }

        private void LateUpdate()
        {
            if (!rending) return;
            RenderLine();
        }

        public void EnableRender() { rending = true; }
        public void HideRender() { rending = false; }

        public void RenderLine()
        {
            if (line == null) return;
            Calculate();
            //edgeCollider2D.points = wayPoints.Select((V3) => (Vector2)V3 - offset).ToArray();
            line.textureOffset -= distanceRope;
            line.points3 = wayPoints;
            line.color = lineColorCurrent;
            line?.Draw();
        }

        public void UpdateTexturePullBackTarget()
        {
            line.texture = lineTexturePullBackTarget;
            line.lineWidth = capLineWidthDefaut * 1f;
        }
        public void UpdateTextureDefaut()
        {
            line.texture = lineTexture;
            line.lineWidth = capLineWidthDefaut;
        }
        public void UpdateLineCollor(Color collorUpdate)
        {
            lineColorCurrent = collorUpdate;
        }
        public void DefautLineCollor()
        {
            lineColorCurrent = lineColor;
        }
        private void AddPiece(Piece piece)
        {
            if (!wayPoints.Contains(piece.PrevFrontBandPoint)) wayPoints.Add(piece.PrevFrontBandPoint);
            if (!wayPoints.Contains(piece.PrevBackBandPoint)) wayPoints.Add(piece.PrevBackBandPoint);
        }

        public int PieceCout
        {
            get
            {
                return transform.childCount;
            }
        }
        public void ClearPiece()
        {
            for (int i = 1; i < transform.childCount; i++)
            {
                if (transform.GetChild(i).gameObject.activeInHierarchy)
                    Destroy(transform.GetChild(i).gameObject);
            }
        }
        public void RemovePiece(int index)
        {
            if (index <= 1) return;
            for (var i = 0; i < transform.childCount; i++)
            {
                if (i >= index)
                {
                    if (transform.GetChild(i).gameObject.activeInHierarchy)
                        Destroy(transform.GetChild(i).gameObject);
                }

            }
        }
        public void SetUpdateLine(bool isUpdate)
        {
            rope.IsKnee = isUpdate;
        }
        public void Calculate()
        {
            wayPoints.Clear();
            if (transform == null) return;

            var rootPiece = transform.GetChild(0).GetComponent<Piece>();
            AddPiece(rootPiece);

            for (int i = 1; i < transform.childCount; i++)
            {
                var nextPiece = rootPiece.BackPiece;
                if (nextPiece.FrontPiece == rootPiece)
                {
                    rootPiece = nextPiece;
                    AddPiece(rootPiece);
                }
            }

            edgePointDraw = new Vector2[wayPoints.Count];
            edgePointDraw[0] = edgeCollider2D.points[0];
            for (int i = 0; i < wayPoints.Count - 1; i++)
            {
                Vector2 offset = wayPoints[wayPoints.Count - 1 - i] - wayPoints[wayPoints.Count - 2 - i];
                edgePointDraw[i + 1] = edgePointDraw[i] - offset;
            }
            edgeCollider2D.points = edgePointDraw;
        }

        public Rope2D()
        {
            capLineWidth = 20f;
            _oneFrame = false;
            wayPoints = new List<Vector3>();
        }
    }
}