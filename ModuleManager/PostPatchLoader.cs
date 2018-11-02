using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using ModuleManager.Extensions;
using ModuleManager.Logging;

using static ModuleManager.FilePathRepository;

namespace ModuleManager
{
    public delegate void ModuleManagerPostPatchCallback();

    public class PostPatchLoader : LoadingSystem
    {
        public static PostPatchLoader Instance { get; private set; }

        public IEnumerable<IProtoUrlConfig> databaseConfigs = null;

        private static readonly List<ModuleManagerPostPatchCallback> postPatchCallbacks = new List<ModuleManagerPostPatchCallback>();

        private readonly IBasicLogger logger = new ModLogger("ModuleManager", new UnityLogger(UnityEngine.Debug.unityLogger));

        private bool ready = false;

        public static void AddPostPatchCallback(ModuleManagerPostPatchCallback callback)
        {
            if (!postPatchCallbacks.Contains(callback))
                postPatchCallbacks.Add(callback);
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        public override bool IsReady() => ready;

        public override float LoadWeight() => 0;

        public override float ProgressFraction() => 1;

        public override string ProgressTitle() => "ModuleManager : post patch";

        public override void StartLoad()
        {
            ready = false;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Stopwatch waitTimer = new Stopwatch();
            waitTimer.Start();

            while (databaseConfigs == null) yield return null;

            waitTimer.Stop();
            logger.Info("Waited " + ((float)waitTimer.ElapsedMilliseconds / 1000).ToString("F3") + "s for patching to finish");

            Stopwatch postPatchTimer = new Stopwatch();
            postPatchTimer.Start();

            logger.Info("Applying patched game database");

            foreach (UrlDir.UrlFile file in GameDatabase.Instance.root.AllConfigFiles)
            {
                file.configs.Clear();
            }

            foreach (IProtoUrlConfig protoConfig in databaseConfigs)
            {
                protoConfig.UrlFile.AddConfig(protoConfig.Node);
            }

            databaseConfigs = null;

            yield return null;

            if (File.Exists(logPath))
            {
                logger.Info("Dumping ModuleManager log to main log");
                logger.Info("\n#### BEGIN MODULEMANAGER LOG ####\n\n\n" + File.ReadAllText(logPath) + "\n\n\n#### END MODULEMANAGER LOG ####");
            }
            else
            {
                logger.Error("ModuleManager log does not exist: " + logPath);
            }

            yield return null;

#if DEBUG
            InGameTestRunner testRunner = new InGameTestRunner(logger);
            testRunner.RunTestCases(GameDatabase.Instance.root);
#endif

            yield return null;

            logger.Info("Reloading resources definitions");
            PartResourceLibrary.Instance.LoadDefinitions();

            logger.Info("Reloading Trait configs");
            GameDatabase.Instance.ExperienceConfigs.LoadTraitConfigs();

            logger.Info("Reloading Part Upgrades");
            PartUpgradeManager.Handler.FillUpgrades();

            yield return null;

            logger.Info("Running post patch callbacks");

            foreach (ModuleManagerPostPatchCallback callback in postPatchCallbacks)
            {
                try
                {
                    callback();
                }
                catch (Exception e)
                {
                    logger.Exception("Exception while running a post patch callback", e);
                }
                yield return null;
            }
            yield return null;

            // Call all "public static void ModuleManagerPostLoad()" on all class
            foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type type in ass.GetTypes())
                    {
                        MethodInfo method = type.GetMethod("ModuleManagerPostLoad", BindingFlags.Public | BindingFlags.Static);

                        if (method != null && method.GetParameters().Length == 0)
                        {
                            try
                            {
                                logger.Info("Calling " + ass.GetName().Name + "." + type.Name + "." + method.Name + "()");
                                method.Invoke(null, null);
                            }
                            catch (Exception e)
                            {
                                logger.Exception("Exception while calling " + ass.GetName().Name + "." + type.Name + "." + method.Name + "()", e);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Exception("Post run call threw an exception in loading " + ass.FullName, e);
                }
            }

            yield return null;

            // Call "public void ModuleManagerPostLoad()" on all active MonoBehaviour instance
            foreach (MonoBehaviour obj in FindObjectsOfType<MonoBehaviour>())
            {
                MethodInfo method = obj.GetType().GetMethod("ModuleManagerPostLoad", BindingFlags.Public | BindingFlags.Instance);

                if (method != null && method.GetParameters().Length == 0)
                {
                    try
                    {
                        logger.Info("Calling " + obj.GetType().Name + "." + method.Name + "()");
                        method.Invoke(obj, null);
                    }
                    catch (Exception e)
                    {
                        logger.Exception("Exception while calling " + obj.GetType().Name + "." + method.Name + "() :\n", e);
                    }
                }
            }

            yield return null;

            if (ModuleManager.dumpPostPatch)
                ModuleManager.OutputAllConfigs();

            postPatchTimer.Stop();
            logger.Info("Post patch ran in " + ((float)postPatchTimer.ElapsedMilliseconds / 1000).ToString("F3") + "s");

            ready = true;
        }
    }
}
