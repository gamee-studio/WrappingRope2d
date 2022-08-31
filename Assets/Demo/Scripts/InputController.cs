using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using Vectrosity;

namespace Gamee_Hiukka.WrappingRope2D
{
    public class InputController : MonoBehaviour
    {
        [SerializeField] List<HandDrag> hands;
        [SerializeField] Camera cam;
        [SerializeField] bool isTapMove;
        bool isSellectHand = false;
        Vector3 posMouse = Vector3.zero;
        Vector3 posOffset = Vector3.zero;
        HandDrag handSellect = null;

        public List<HandDrag> Hands => hands;
        private void Awake()
        {
            isSellectHand = false;
            handSellect = null;
        }

        private void Start()
        {
            if (cam == null) cam = Camera.main;
            foreach (var hand in hands)
            {
                hand.Init();
            }
            hands[0].UpdateCanvasLine(cam);

            if (VectorLine.canvas != null)
            {
                VectorLine.canvas.transform.parent = this.transform;
            }
            //TargetAlert();
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (IsPointerOverUIObject())
                {
                    return;
                }

                posMouse = cam.ScreenToWorldPoint(Input.mousePosition);
                var temp = 1000f;
                foreach (var hand in hands)
                {
                    var dis = (posMouse - hand.transform.position).magnitude;
                    if (dis < temp)
                    {
                        temp = dis;
                        handSellect = hand;
                    }
                }
                handSellect.IsHold = true;
                if (isTapMove)
                {
                    posOffset = Vector3.zero;
                    handSellect.Move(posMouse);
                }
                else
                {
                    posOffset = posMouse - handSellect.transform.position;
                }
                handSellect.IsFollowObj = false;
                isSellectHand = true;
            }

            if (Input.GetMouseButtonUp(0))
            {
                isSellectHand = false;

                if (handSellect != null)
                {
                    handSellect.IsFollowObj = true;
                    handSellect.IsHold = false;
                    handSellect.StopAudioMove();
                    handSellect = null;
                }
            }
        }

        public void FixedUpdate()
        {
            if (isSellectHand)
            {
                if (handSellect == null) return;
                if (!handSellect.IsHold) return;
                posMouse = cam.ScreenToWorldPoint(Input.mousePosition);
                handSellect.Move(posMouse - posOffset);
            }
        }

        private bool IsPointerOverUIObject()
        {
            if (EventSystem.current == null) return false;
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
            eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventDataCurrentPosition, results);
            return results.Count > 0;
        }
    }
}

