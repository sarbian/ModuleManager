﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ModuleManager.Extensions;
using ModuleManager.Progress;

namespace ModuleManager
{
    public static class PatchExtractor
    {
        private static readonly Regex firstRegex = new Regex(@":FIRST", RegexOptions.IgnoreCase);
        private static readonly Regex finalRegex = new Regex(@":FINAL", RegexOptions.IgnoreCase);
        private static readonly Regex beforeRegex = new Regex(@":BEFORE(?:\[([^\[\]]+)\])?", RegexOptions.IgnoreCase);
        private static readonly Regex forRegex = new Regex(@":FOR(?:\[([^\[\]]+)\])?", RegexOptions.IgnoreCase);
        private static readonly Regex afterRegex = new Regex(@":AFTER(?:\[([^\[\]]+)\])?", RegexOptions.IgnoreCase);

        public static PatchList SortAndExtractPatches(UrlDir databaseRoot, IEnumerable<string> modList, IPatchProgress progress)
        {
            PatchList list = new PatchList(modList);

            // Have to convert to an array because we will be removing patches
            foreach (UrlDir.UrlConfig url in databaseRoot.AllConfigs.ToArray())
            {
                try
                {
                    if (!url.type.IsBracketBalanced())
                    {
                        progress.Error(url, "Error - node name does not have balanced brackets (or a space - if so replace with ?):\n" + url.SafeUrl());
                        url.parent.configs.Remove(url);
                        continue;
                    }

                    Command command = CommandParser.Parse(url.type, out string name);;

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
                        progress.Error(url, $"Error - pass specifier detected on an insert node (not a patch): {url.SafeUrl()}");
                        error = true;
                    }
                    else if (command == Command.Replace)
                    {
                        progress.Error(url, $"Error - replace command (%) is not valid on a root node: {url.SafeUrl()}");
                        error = true;
                    }
                    else if (command == Command.Create)
                    {
                        progress.Error(url, $"Error - create command (&) is not valid on a root node: {url.SafeUrl()}");
                        error = true;
                    }
                    else if (command == Command.Rename)
                    {
                        progress.Error(url, $"Error - rename command (|) is not valid on a root node: {url.SafeUrl()}");
                        error = true;
                    }
                    else if (command == Command.Paste)
                    {
                        progress.Error(url, $"Error - paste command (#) is not valid on a root node: {url.SafeUrl()}");
                        error = true;
                    }
                    else if (command == Command.Special)
                    {
                        progress.Error(url, $"Error - special command (*) is not valid on a root node: {url.SafeUrl()}");
                        error = true;
                    }

                    if (matchCount > 1)
                    {
                        progress.Error(url, $"Error - more than one pass specifier on a node: {url.SafeUrl()}");
                        error = true;
                    }
                    if (beforeMatch.Success && !beforeMatch.Groups[1].Success)
                    {
                        progress.Error(url, "Error - malformed :BEFORE patch specifier detected: " + url.SafeUrl());
                        error = true;
                    }
                    if (forMatch.Success && !forMatch.Groups[1].Success)
                    {
                        progress.Error(url, "Error - malformed :FOR patch specifier detected: " + url.SafeUrl());
                        error = true;
                    }
                    if (afterMatch.Success && !afterMatch.Groups[1].Success)
                    {
                        progress.Error(url, "Error - malformed :AFTER patch specifier detected: " + url.SafeUrl());
                        error = true;
                    }
                    if (error)
                    {
                        url.parent.configs.Remove(url);
                        continue;
                    }

                    if (command == Command.Insert) continue;

                    url.parent.configs.Remove(url);

                    Match theMatch = null;
                    List<Patch> thePass = null;

                    if (firstMatch.Success)
                    {
                        theMatch = firstMatch;
                        thePass = list.firstPatches;
                    }
                    else if (finalMatch.Success)
                    {
                        theMatch = finalMatch;
                        thePass = list.finalPatches;
                    }
                    else if (beforeMatch.Success)
                    {
                        if (CheckMod(beforeMatch, list.modPasses, out string theMod))
                        {
                            theMatch = beforeMatch;
                            thePass = list.modPasses[theMod].beforePatches;
                        }
                        else
                        {
                            progress.NeedsUnsatisfiedBefore(url);
                            continue;
                        }
                    }
                    else if (forMatch.Success)
                    {
                        if (CheckMod(forMatch, list.modPasses, out string theMod))
                        {
                            theMatch = forMatch;
                            thePass = list.modPasses[theMod].forPatches;
                        }
                        else
                        {
                            progress.NeedsUnsatisfiedFor(url);
                            continue;
                        }
                    }
                    else if (afterMatch.Success)
                    {
                        if (CheckMod(afterMatch, list.modPasses, out string theMod))
                        {
                            theMatch = afterMatch;
                            thePass = list.modPasses[theMod].afterPatches;
                        }
                        else
                        {
                            progress.NeedsUnsatisfiedAfter(url);
                            continue;
                        }
                    }
                    else
                    {
                        thePass = list.legacyPatches;
                    }

                    string newName;
                    if (theMatch == null)
                        newName = name;
                    else
                        newName = name.Remove(theMatch.Index, theMatch.Length);

                    ConfigNode node = new ConfigNode(newName) { id = url.config.id };
                    node.ShallowCopyFrom(url.config);
                    Patch patch = new Patch(url, command, node);

                    thePass.Add(patch);
                    progress.PatchAdded();
                }
                catch(Exception e)
                {
                    progress.Exception(url, $"Exception while parsing pass for config: {url.SafeUrl()}", e);
                }
            }

            return list;
        }

        private static bool CheckMod(Match match, PatchList.ModPassCollection modPasses, out string theMod)
        {
            theMod = match.Groups[1].Value.Trim().ToLower();
            return modPasses.HasMod(theMod);
        }
    }
}
