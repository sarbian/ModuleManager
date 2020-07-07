using System.Collections;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace ModuleManager.Cats
{
    class CatAnimator : MonoBehaviour
    {

        public Sprite[] frames;
        public float secFrame = 0.07f;

        private SpriteRenderer spriteRenderer;
        private int spriteIdx;

        [SuppressMessage("CodeQuality", "IDE0051", Justification = "Called by Unity")]
        void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
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
