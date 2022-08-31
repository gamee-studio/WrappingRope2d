using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gamee_Hiukka.WrappingRope2D
{
    public class Collision2DComponent : MonoBehaviour
    {
        public Action<GameObject> actionTriggerEnter;
        public Action<GameObject> actionTriggerStay;
        public Action<GameObject> actionTriggerExit;

        private void OnTriggerEnter2D(Collider2D collision)
        {
            actionTriggerEnter?.Invoke(collision.gameObject);
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            actionTriggerStay?.Invoke(collision.gameObject);
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            actionTriggerExit?.Invoke(collision.gameObject);
        }
    }
}

