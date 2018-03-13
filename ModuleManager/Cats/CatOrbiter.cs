using System;
using System.Collections.Generic;
using KSP.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ModuleManager.Cats
{
    class CatOrbiter : MonoBehaviour
    {
        private static List<CatOrbiter> orbiters = new List<CatOrbiter>();

        private static CatOrbiter sun;

        private double _mass;
        public Rigidbody2D rb;

        private Vector2d pos;
        private Vector2d vel;
        private Vector2d force;
        private float scale = 1;

        private double G = 6.67408E-11;

        public double Mass
        {
            get { return _mass; }
            set
            {
                _mass = value;
                if (rb!=null)
                    rb.mass = (float)_mass;
            }
        }
        
        public void Init(CatOrbiter parent, float soi)
        {

            TimingManager.FixedUpdateAdd(TimingManager.TimingStage.Earlyish, DoForces);

            orbiters.Add(this);
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.isKinematic = true;

            if (orbiters.Count == 1)
            {
                sun = this;
                Vector3 spos = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, -1);
                transform.position = KSP.UI.UIMainCamera.Camera.ScreenToWorldPoint(spos);
                Mass = 2E17;
                pos.x = transform.position.x;
                pos.y = transform.position.y;
            }
            else
            {
                Vector2 relativePos = Random.insideUnitCircle;
                if (relativePos.magnitude < 0.2)
                    relativePos = relativePos.normalized * 0.3f;
                Vector3 spos = UIMainCamera.Camera.WorldToScreenPoint(parent.transform.position) + (Vector3)(relativePos * soi);
                spos.z = -1;
                transform.position = UIMainCamera.Camera.ScreenToWorldPoint(spos);

                pos.x = transform.position.x;
                pos.y = transform.position.y;
                
                //int scaleRange = 10;
                //
                //float factor = (1 + (scaleRange - 1) * Random.value);
                //
                //scale = parent.scale * factor / scaleRange;
                scale = parent.scale * 0.6f;

                transform.localScale *= scale;
                TrailRenderer trail = gameObject.GetComponent<TrailRenderer>();
                trail.colorGradient = new Gradient() {alphaKeys = new GradientAlphaKey[3] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1)  }};
                trail.startWidth *= scale;
                //trail.endWidth *= scale;
                trail.widthCurve = new AnimationCurve(new Keyframe(0, trail.startWidth ), new Keyframe(0.7f, trail.startWidth), new Keyframe(1, trail.startWidth * 0.9f));

                //Mass = factor * 2E16;

                Mass = parent.Mass * 0.025;

                Vector2d dist = parent.pos - pos;
                double circularVel = Math.Sqrt(G * (Mass + parent.Mass) / dist.magnitude);
                if (parent == sun)
                    circularVel *= Random.Range(0.9f, 1.1f);
                Debug.Log("CatOrbiter " + circularVel.ToString("F3") + " " + Mass.ToString("F2") + " " + orbiters[0].Mass.ToString("F2") + " " +
                          dist.magnitude.ToString("F2"));

                Vector3d normal = (Random.value >= 0.3) ? Vector3d.back : Vector3d.forward;

                Vector3d vel3d = Vector3d.Cross(dist, normal).normalized * circularVel;
                vel.x = parent.vel.x + vel3d.x;
                vel.y = parent.vel.y + vel3d.y;
            }

            rb.MovePosition(new Vector2((float)pos.x, (float)pos.y));
        }

        private void DoForces()
        {
            force = Vector2d.zero;
            foreach (CatOrbiter cat in orbiters)
            {
                if (cat == this)
                    continue;

                // F = G * (m1 * m2) / r^2
                Vector2d dir = cat.pos - pos;
                double f = G * (cat.Mass * Mass) / (dir.sqrMagnitude + 10); // +10 to avoid div/0
                force += (float)f * dir.normalized;
            }
        }


        void OnDestroy()
        {
            orbiters.Remove(this);
            TimingManager.FixedUpdateRemove(TimingManager.TimingStage.Earlyish, DoForces);
        }

        void FixedUpdate()
        {
            //if (this == sun)
            //    return;

            vel += Time.fixedDeltaTime * force / Mass;
            pos += Time.fixedDeltaTime * vel;

            rb.MovePosition(new Vector2((float)pos.x, (float)pos.y));

            double angle = Math.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
            rb.MoveRotation((float)angle);
        }
    }
}
