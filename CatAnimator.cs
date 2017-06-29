using System.Collections;
using UnityEngine;

namespace ModuleManager
{
    class CatAnimator : MonoBehaviour
    {

        public Sprite[] frames;
        public float secFrame = 0.07f;

        private SpriteRenderer spriteRenderer;
        private int spriteIdx;

        void Start()
        {
            spriteRenderer = this.GetComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 3;
            StartCoroutine(Animate());
        }


        IEnumerator Animate()
        {
            if (frames.Length == 0)
                yield return null;

            WaitForSeconds yield = new WaitForSeconds(secFrame);

            while (true)
            {
                spriteIdx = (spriteIdx + 1) % frames.Length;
                spriteRenderer.sprite = frames[spriteIdx];
                yield return yield;
            }
        }
    }
}
