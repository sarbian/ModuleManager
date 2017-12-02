using System.Collections;
using UnityEngine;

namespace ModuleManager.Cats
{
    public class CatMover : MonoBehaviour
    {
        public Vector3 spos;

        public float vel = 5;
        private float offsetY;

        public TrailRenderer trail;
        private SpriteRenderer spriteRenderer;

        private int totalLenth = 100;
        private float activePos = 0;

        public float scale = 2;

        private float time = 5;
        private float trailTime = 0.5f;

        // Use this for initialization
        void Start()
        {
            trail = this.GetComponent<TrailRenderer>();
            trail.sortingOrder = 2;


            spriteRenderer = this.GetComponent<SpriteRenderer>();

            offsetY = Mathf.FloorToInt(0.2f * Screen.height);

            spos.z = -1;
            
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
                activePos = -spriteRenderer.sprite.rect.width;
                trail.time = 0;
            }

            float f = 2f * Mathf.PI * (activePos) / (Screen.width * 0.5f);

            float heightOffset = Mathf.Sin(f) * (spriteRenderer.sprite.rect.height * scale);

            spos.x = activePos;
            spos.y = offsetY + heightOffset;
            
            transform.position = KSP.UI.UIMainCamera.Camera.ScreenToWorldPoint(spos);
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Cos(f) * 0.25f * Mathf.PI * Mathf.Rad2Deg);
        }

        
    }
}
