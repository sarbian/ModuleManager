using System.Collections;
using UnityEngine;

namespace ModuleManager
{
    public class CatMover : MonoBehaviour
    {
        public Vector3 spos;

        public float vel = 5;
        private float offsetY;

        public TrailRenderer trail;
        
        public Sprite[] frames;
        public float secFrame = 0.07f;

        private SpriteRenderer spriteRenderer;
        private int spriteIdx;

        private int totalLenth = 100;
        private float activePos = 0;

        public float scale = 2;


        private float time = 5;
        private float trailTime = 0.5f;


        // Use this for initialization
        void Start()
        {
            trail = this.GetComponent<TrailRenderer>();
            spriteRenderer = this.GetComponent<SpriteRenderer>();

            spriteRenderer.sortingOrder = 3;
            trail.sortingOrder = 2;

            offsetY = Mathf.FloorToInt(0.2f * Screen.height);

            spos.z = -1;

            StartCoroutine(Animate());

            trailTime = time / 4;

            totalLenth = (int) (Screen.width / time * trail.time) + 150;
            trail.time = trailTime;
        }

        void Update()
        {
            if (trail.time <= 0f)
            {
                trail.time = trailTime;
            }

            activePos += ((Screen.width / time) * Time.deltaTime);

            if (activePos > (Screen.width + totalLenth))
            {
                activePos = -frames[spriteIdx].textureRect.width;
                trail.time = 0;;
            }

            float f = 2f * Mathf.PI * (activePos) / (Screen.width * 0.5f);

            float heightOffset = Mathf.Sin(f) * (frames[spriteIdx].textureRect.height * scale);

            spos.x = activePos;
            spos.y = offsetY + heightOffset;
            
            transform.position = KSP.UI.UIMainCamera.Camera.ScreenToWorldPoint(spos);
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Cos(f) * 0.25f * Mathf.PI * Mathf.Rad2Deg);
        }

        IEnumerator Animate()
        {
            if (frames.Length == 0)
                yield return null;
            
            WaitForSeconds yield = new WaitForSeconds(secFrame);

            while (true)
            {
                spriteIdx = (spriteIdx+1) % frames.Length;
                spriteRenderer.sprite = frames[spriteIdx];
                yield return yield;
            }
        }
    }
}
