using System.Diagnostics.CodeAnalysis;
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

        private const float time = 5;
        private const float trailTime = time / 4;

        private bool clearTrail = false;

        // Use this for initialization
        [SuppressMessage("CodeQuality", "IDE0051", Justification = "Called by Unity")]
        void Start()
        {
            trail = GetComponent<TrailRenderer>();
            trail.sortingOrder = 2;

            spriteRenderer = GetComponent<SpriteRenderer>();

            offsetY = Mathf.FloorToInt(0.2f * Screen.height);

            spos.z = -1;

            totalLenth = (int) (Screen.width / time * trail.time) + 150;
            trail.time = trailTime;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, trail.startWidth ), new Keyframe(0.7f, trail.startWidth), new Keyframe(1, trail.startWidth * 0.9f));
            clearTrail = true;
        }

        [SuppressMessage("CodeQuality", "IDE0051", Justification = "Called by Unity")]
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
                clearTrail = true;
            }

            float f = 2f * Mathf.PI * (activePos) / (Screen.width * 0.5f);

            float heightOffset = Mathf.Sin(f) * (spriteRenderer.sprite.rect.height * scale);

            spos.x = activePos;
            spos.y = offsetY + heightOffset;
            
            transform.position = KSP.UI.UIMainCamera.Camera.ScreenToWorldPoint(spos);
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Cos(f) * 0.25f * Mathf.PI * Mathf.Rad2Deg);

            if (clearTrail)
            {
                trail.Clear();
                clearTrail = false;
            }

        }

        
    }
}
