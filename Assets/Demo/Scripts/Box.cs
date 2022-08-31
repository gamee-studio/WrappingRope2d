using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gamee_Hiukka.WrappingRope2D 
{
    public class Box : MonoBehaviour, IHander
    {
        [SerializeField] float speedFollow = 1f;
        HandDrag hand;
        float timeSmooth = 0f;

        public void OntriggerHand(HandDrag hand)
        {
            this.hand = hand;
        }

        public void Update()
        {
            if (hand == null) return;
            timeSmooth += Time.deltaTime * speedFollow;
            this.transform.position = Vector2.Lerp(this.transform.position, hand.transform.position, timeSmooth);
            //this.transform.position = handDrag.transform.position;
        }
    }
}

