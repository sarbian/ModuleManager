using System;
using System.Text.RegularExpressions;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Patches;
using ModuleManager.Progress;

namespace ModuleManager
{
    public class PatchExtractor
    {
        private static readonly Regex firstRegex = new Regex(@":FIRST", RegexOptions.IgnoreCase);
        private static readonly Regex finalRegex = new Regex(@":FINAL", RegexOptions.IgnoreCase);
        private static readonly Regex beforeRegex = new Regex(@":BEFORE(?:\[([^\[\]]+)\])?", RegexOptions.IgnoreCase);
        private static readonly Regex forRegex = new Regex(@":FOR(?:\[([^\[\]]+)\])?", RegexOptions.IgnoreCase);
        private static readonly Regex afterRegex = new Regex(@":AFTER(?:\[([^\[\]]+)\])?", RegexOptions.IgnoreCase);

        private readonly IPatchList patchList;
        private readonly IPatchProgress progress;
        private readonly IBasicLogger logger;

        public PatchExtractor(IPatchList patchList, IPatchProgress progress, IBasicLogger logger)
        {
            this.patchList = patchList ?? throw new ArgumentNullException(nameof(patchList));
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void ExtractPatch(UrlDir.UrlConfig urlConfig)
        {
            try
            {
                if (!urlConfig.type.IsBracketBalanced())
                {
                    progress.Error(urlConfig, "Error - node name does not have balanced brackets (or a space - if so replace with ?):\n" + urlConfig.SafeUrl());
                    urlConfig.parent.configs.Remove(urlConfig);
                    return;
                }

                Command command = CommandParser.Parse(urlConfig.type, out string name);

                Match firstMatch = firstRegex.Match(name);
                Match finalMatch = finalRegex.Match(name);
                Match beforeMatch = beforeRegex.Match(name);
                Match forMatch = forRegex.Match(name);
                Match afterMatch = afterRegex.Match(name);

                int matchCount = 0;

                if (firstMatch.Success) matchCount++;
                if (finalMatch.Success) matchCount++;
                if (beforeMatch.Success) matchCount++;
                if (forMatch.Success) matchCount++;
                if (afterMatch.Success) matchCount++;

                if (firstMatch.NextMatch().Success) matchCount++;
                if (finalMatch.NextMatch().Success) matchCount++;
                if (beforeMatch.NextMatch().Success) matchCount++;
                if (forMatch.NextMatch().Success) matchCount++;
                if (afterMatch.NextMatch().Success) matchCount++;

                bool error = false;

                if (command == Command.Insert && matchCount > 0)
                {
                    progress.Error(urlConfig, $"Error - pass specifier detected on an insert node (not a patch): {urlConfig.SafeUrl()}");
                    error = true;
                }
                else if (command == Command.Replace)
                {
                    progress.Error(urlConfig, $"Error - replace command (%) is not valid on a root node: {urlConfig.SafeUrl()}");
                    error = true;
                }
                else if (command == Command.Create)
                {
                    progress.Error(urlConfig, $"Error - create command (&) is not valid on a root node: {urlConfig.SafeUrl()}");
                    error = true;
                }
                else if (command == Command.Rename)
                {
                    progress.Error(urlConfig, $"Error - rename command (|) is not valid on a root node: {urlConfig.SafeUrl()}");
                    error = true;
                }
                else if (command == Command.Paste)
                {
                    progress.Error(urlConfig, $"Error - paste command (#) is not valid on a root node: {urlConfig.SafeUrl()}");
                    error = true;
                }
                else if (command == Command.Special)
                {
                    progress.Error(urlConfig, $"Error - special command (*) is not valid on a root node: {urlConfig.SafeUrl()}");
                    error = true;
                }

                if (matchCount > 1)
                {
                    progress.Error(urlConfig, $"Error - more than one pass specifier on a node: {urlConfig.SafeUrl()}");
                    error = true;
                }
                if (beforeMatch.Success && !beforeMatch.Groups[1].Success)
                {
                    progress.Error(urlConfig, "Error - malformed :BEFORE patch specifier detected: " + urlConfig.SafeUrl());
                    error = true;
                }
                if (forMatch.Success && !forMatch.Groups[1].Success)
                {
                    progress.Error(urlConfig, "Error - malformed :FOR patch specifier detected: " + urlConfig.SafeUrl());
                    error = true;
                }
                if (afterMatch.Success && !afterMatch.Groups[1].Success)
                {
                    progress.Error(urlConfig, "Error - malformed :AFTER patch specifier detected: " + urlConfig.SafeUrl());
                    error = true;
                }
                if (error)
                {
                    urlConfig.parent.configs.Remove(urlConfig);
                    return;
                }

                if (command == Command.Insert) return;

                urlConfig.parent.configs.Remove(urlConfig);

                Match theMatch = null;
                Action<IPatch> addPatch = null;

                if (firstMatch.Success)
                {
                    theMatch = firstMatch;
                    addPatch = patchList.AddFirstPatch;
                }
                else if (finalMatch.Success)
                {
                    theMatch = finalMatch;
                    addPatch = patchList.AddFinalPatch;
                }
                else if (beforeMatch.Success)
                {
                    if (CheckMod(beforeMatch, patchList, out string theMod))
                    {
                        theMatch = beforeMatch;
                        addPatch = p => patchList.AddBeforePatch(theMod, p);
                    }
                    else
                    {
                        progress.NeedsUnsatisfiedBefore(urlConfig);
                        return;
                    }
                }
                else if (forMatch.Success)
                {
                    if (CheckMod(forMatch, patchList, out string theMod))
                    {
                        theMatch = forMatch;
                        addPatch = p => patchList.AddForPatch(theMod, p);
                    }
                    else
                    {
                        progress.NeedsUnsatisfiedFor(urlConfig);
                        return;
                    }
                }
                else if (afterMatch.Success)
                {
                    if (CheckMod(afterMatch, patchList, out string theMod))
                    {
                        theMatch = afterMatch;
                        addPatch = p => patchList.AddAfterPatch(theMod, p);
                    }
                    else
                    {
                        progress.NeedsUnsatisfiedAfter(urlConfig);
                        return;
                    }
                }
                else
                {
                    addPatch = patchList.AddLegacyPatch;
                }

                string newName;
                if (theMatch == null)
                    newName = name;
                else
                    newName = name.Remove(theMatch.Index, theMatch.Length);

                addPatch(PatchCompiler.CompilePatch(urlConfig, command, newName));
                progress.PatchAdded();
            }
            catch(Exception e)
            {
                progress.Exception(urlConfig, $"Exception while parsing pass for config: {urlConfig.SafeUrl()}", e);

                try
                {
                    urlConfig.parent.configs.Remove(urlConfig);
                }
                catch (Exception ex)
                {
                    logger.Exception("Exception while attempting to clean up bad config", ex);
                }
            }
        }

        private static bool CheckMod(Match match, IPatchList patchList, out string theMod)
        {
            theMod = match.Groups[1].Value.Trim().ToLower();
            return patchList.HasMod(theMod);
        }
    }
}
