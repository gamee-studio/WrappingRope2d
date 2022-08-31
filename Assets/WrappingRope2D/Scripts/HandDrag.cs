using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamee_Hiukka.WrappingRope2D
{
    public class HandDrag : MonoBehaviour
    {
        public Rope2D rope2D;
        [SerializeField] SpriteRenderer srHand;
        [SerializeField] GameObject point;
        [SerializeField] Sprite spHandHold;
        [SerializeField] float speed = 1f;
        [SerializeField] bool isHoldTarget = false;
        [SerializeField] bool canMoveBack = true;
        [SerializeField] Collision2DComponent colider2d;
        [SerializeField] GameObject boxMove;
        [SerializeField] bool isRotation = true;
        [SerializeField] float speedRotation = 10f;
        [SerializeField] float speedDrag = 8f;
        [SerializeField] GameObject objFollow;

        float collisionOffset = 0.0f;
        public ContactFilter2D movementFilter;

        Rigidbody2D rig;
        bool isMoveBack = false;
        bool isLock = false;
        IHander hander = null;
        Vector3 posDefaut = Vector3.zero;
        Quaternion rotationDefaut;
        Vector3 posUpdate = Vector3.zero;
        Vector2 posRigDefaut = Vector3.zero;
        Vector3 objFollowPosition = Vector3.zero;

        Sprite spHand;
        bool isFollowObj = false;
        bool isHold = false;
        Camera cam = null;
        Vector3 offsetPointDefaut = Vector3.zero;
        Vector3 offsetPoint = Vector3.zero;

        public bool IsLock { set => isLock = value; get => isLock; }
        public bool IsFollowObj { set => isFollowObj = value; get => isFollowObj; }
        public bool IsHold { set => isHold = value; get => isHold; }
        public bool IsMoveTeleport { get; set; }
        public bool IsMoved { get; set; }
        public bool IsMoveBack => isMoveBack;

        float timeDragCurrent = 0;
        float timeDrag = .2f;

        public void Awake()
        {
            rig = this.GetComponentInChildren<Rigidbody2D>();
            colider2d.actionTriggerEnter = OnTriggerEnterEvent;
            boxMove.gameObject.SetActive(true);
            spHand = GetComponentInChildren<SpriteRenderer>().sprite;
            if (objFollow != null)
            {
                objFollowPosition = objFollow.transform.position;
                isFollowObj = true;
            }
            this.posUpdate = this.transform.position;
            posDefaut = this.transform.position;
            rotationDefaut = srHand.transform.localRotation;
            offsetPointDefaut = this.transform.position - point.transform.position;
            posRigDefaut = rig.position;
        }
        public void Init()
        {
            rope2D.Init(this);
            rope2D.CreateLine("line");
            rope2D.EnableRender();
        }

        public void UpdateCanvasLine(Camera cam)
        {
            this.cam = cam;
            rope2D.SetCameraCanvasLine(cam);
        }

        public void Move(Vector3 posUpdate)
        {
            //if (hander != null) return;
            if (rig == null) return;
            if (isMoveBack) return;
            this.posUpdate = posUpdate;

            //Vector3 v3 = Vector3.Lerp(this.transform.position, posUpdate, speedDrag * Time.deltaTime);
            //rig.MovePosition((Vector2)v3);

            var dir = posUpdate - this.transform.position;
            rig.MovePosition(rig.position + (Vector2)(posUpdate - this.transform.position) * speedDrag * Time.deltaTime);
            offsetPoint -= (Vector3)(rig.position - posRigDefaut) * speedDrag * Time.deltaTime;
            if ((rig.position - posRigDefaut).magnitude > 0.01f)
            {
                timeDragCurrent -= Time.deltaTime;
                if (timeDragCurrent <= 0)
                {
                    timeDragCurrent = timeDrag;
                    // play sound move
                }
            }
        }

        public void StopAudioMove()
        {
            timeDragCurrent = 0;
            // stop sound move
        }

        public void FixedUpdate()
        {
            if (isRotation)
            {
                Vector2 direction = posUpdate - this.transform.position;
                if (direction.magnitude > 0.1f)
                {
                    Quaternion toQuaterion = Quaternion.Euler(new Vector3(0f, 0f, AngleBetweenTwoPoints(posUpdate) + 90));
                    srHand.transform.localRotation = Quaternion.Slerp(srHand.transform.localRotation, toQuaterion, speedRotation * Time.deltaTime);
                }
            }

            if (objFollow == null) return;
            Vector3 offset = objFollow.transform.position - objFollowPosition;
            if (isFollowObj && !isMoveBack)
            {
                //Vector2 v2 = Vector2.Lerp(this.transform.position, this.transform.position + offset, speed * 50f * Time.deltaTime);
                rig.MovePosition(this.transform.position + offset);
            }

            rope2D.UpdateBackEndPositionStart(offset);
            objFollowPosition = objFollow.transform.position;

            //point.transform.position = this.transform.position - this.offsetPoint;
            this.offsetPoint = offsetPointDefaut;
            posRigDefaut = rig.position;
            this.posUpdate = this.transform.position;
        }

        private float AngleBetweenTwoPoints(Vector2 posUpdate)
        {
            return Mathf.Atan2(this.transform.position.y - posUpdate.y, this.transform.position.x - posUpdate.x) * Mathf.Rad2Deg;
        }

        public void UpdateLine(Color color)
        {
            rope2D.UpdateLineCollor(color);
        }
        public void UpdateTexturePullBackTarget()
        {
            rope2D.UpdateTexturePullBackTarget();
        }
        public void UpdateTextureDefaut()
        {
            rope2D.UpdateTextureDefaut();
        }

        Vector3[] WayPoint
        {
            get
            {
                List<Vector3> points = new List<Vector3>();
                points = rope2D.WayPoints;
                points[points.Count - 1] = posDefaut;
                return points.ToArray();
            }
        }
        public void MoveBack(Action actionCompleted = null)
        {
            if (!canMoveBack) return;
            if (isLock) return;
            if (isMoveBack) return;
            isMoveBack = true;
            isHold = false;
            StopAudioMove();
            rope2D.SetUpdateLine(false);
            UpdateHandHoldSprite();
            this.transform.DOKill();
            boxMove.gameObject.SetActive(false);
            var seq = this.transform.DOPath(WayPoint, rope2D.Length / speed, gizmoColor: Color.red);
            seq.SetEase(Ease.Linear).OnComplete(() =>
            {
                srHand.transform.localRotation = rotationDefaut;
                boxMove.gameObject.SetActive(true);
                rope2D.DefautLineCollor();
                HandSpriteDefaut();
                isMoveBack = false;
                rope2D.ClearPiece();
                rope2D.SetUpdateLine(true);

                actionCompleted?.Invoke();
                Defaut();
            });
            var count = rope2D.PieceCout;
            seq.OnWaypointChange((index) =>
            {
                rope2D.RemovePiece(count - index + 1);
            });
        }

        public void Defaut()
        {
            if (!isHoldTarget)
            {
                hander.OntriggerHand(null);
                hander = null;
            }
        }
        void UpdateHandHoldSprite()
        {
            srHand.sprite = spHandHold;
        }
        void HandSpriteDefaut()
        {
            srHand.sprite = spHand;
        }
        void OnTriggerEnterEvent(GameObject go)
        {
            TriggerTarget(go);
        }

        public void ActiveCollison(bool active = true)
        {
            colider2d.enabled = active;
        }
        void TriggerTarget(GameObject go)
        {
            var hander = go.GetComponentInParent<IHander>();
            if (hander == null) return;
            if (this.hander == hander) return;
            this.hander = hander;
            MoveBack();
            hander.OntriggerHand(this);
        }
    }
}

