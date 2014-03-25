using System;
using System.Reflection;
using UnityEngine;
using KSP;

namespace ModuleAnimation2Value
{
    public class ModuleAnimation2Value : PartModule
    {
        [KSPField(isPersistant = false)]
        public string valueModule = "";

        [KSPField(isPersistant = false)]
        public string valueName = "";

        [KSPField(isPersistant = false)]
        public string animationName = "";

        [KSPField(isPersistant = false)]
        public FloatCurve valueCurve = new FloatCurve();

        protected Animation[] anims;
        protected PartModule module;
        protected Type moduleType;
        protected FieldInfo field;
        protected PropertyInfo property;

        public override void OnStart(PartModule.StartState state)
        {
            if (state != PartModule.StartState.Editor)
            {
                anims = part.FindModelAnimators(animationName);
                if ((anims == null) || (anims.Length == 0))
                {
                    print("ModuleAnimation2Value - animation not found: " + animationName);
                }

                moduleType = part.GetType();
                if (valueModule != "")
                {
                    if (part.Modules.Contains(valueModule))
                    {
                        module = part.Modules[valueModule];
                        moduleType = module.GetType();
                    }
                    else
                    {
                        print("ModuleAnimation2Value - module not found: " + valueModule);
                    }
                }

                field = moduleType.GetField(valueName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                {
                    property = moduleType.GetProperty(valueName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (property == null)
                    {
                        print("ModuleAnimation2Value - field/property not found: " + valueName);
                    }
                }
            }

            base.OnStart(state);
        }

        public void Update()
        {
            if (FlightGlobals.ready)
            {
                if ((anims != null) && (anims.Length > 0) && ((field != null) || (property != null)))
                {
                    object target = part;

                    if (module != null)
                    {
                        target = module;
                    }

                    if (field != null)
                    {
                        field.SetValue(target, valueCurve.Evaluate(anims[0][animationName].normalizedTime));
                    }
                    else
                    {
                        property.SetValue(target, valueCurve.Evaluate(anims[0][animationName].normalizedTime), null);
                    }
                }
            }
        }
    }
}
