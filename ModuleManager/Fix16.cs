using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace ModuleManager
{
    class Fix16 : LoadingSystem
    {
        [SuppressMessage("CodeQuality", "IDE0051", Justification = "Called by Unity")]
        private void Awake()
        {
            if (Instance != null)
            {
                DestroyImmediate(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private static Fix16 Instance { get; set; }

        private bool ready;

        private int count;

        private int current;

        private const int yieldStep = 20;

        public override bool IsReady()
        {
            return ready;
        }

        public override string ProgressTitle()
        {
            return "Fix 1.6.0 " + current + "/" + count;
        }

        public override float ProgressFraction()
        {
            return (float) current / count;
        }

        public override void StartLoad()
        {
            ready = false;
            
            count = PartLoader.LoadedPartsList.Count;

            StartCoroutine(DoFix());
        }

        private IEnumerator DoFix()
        {
            int yieldCounter = 0;
            for (current = 0; current < count; current++)
            {
                AvailablePart avp = PartLoader.LoadedPartsList[current];
                if (avp.partPrefab.dragModel == Part.DragModel.CUBE && !avp.partPrefab.DragCubes.Procedural &&
                    !avp.partPrefab.DragCubes.None && avp.partPrefab.DragCubes.Cubes.Count == 0)
                {
                    DragCubeSystem.Instance.LoadDragCubes(avp.partPrefab);
                }

                if (yieldCounter++ >= yieldStep)
                {
                    yieldCounter = 0;
                    yield return null;
                }
            }

            ready = true;
            yield return null;
        }

        public override float LoadWeight()
        {
            return 0.1f;
        }
    }
}
