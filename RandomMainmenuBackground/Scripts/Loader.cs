using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

public class RandomBackgroundLoader
{
    public static void insert(string name)
    {
        if (name.StartsWith(prefix_bg))
            insert(list_bg, prefix_bg, name[prefix_bg.Length..]);
        else if (name.StartsWith(prefix_logo))
            insert(list_logos, prefix_logo,name[prefix_logo.Length..]);
    }

    public static string modWindowName(string name, GUIWindowManager wm)
    {
        return name switch
        {
            prefix_bg => getCurrentOrNext(wm, list_bg, prefix_bg, ref cur_bg),
            prefix_logo => getCurrentOrNext(wm, list_logos, prefix_logo, ref cur_logo),
            _ => name
        };
    }

    private static string getCurrentOrNext(GUIWindowManager wm, List<string> list_names, string prefix, ref string curName)
    {
        string winId = prefix + curName;
        if (list_names.Count <= 0)
        {
            Log.Out($"No override found for {prefix}, loading default.");
            return prefix;
        }

        if (wm.IsWindowOpen(winId))
        {
            Log.Out($"Window is open, using current override for {prefix}: {winId}");
            return winId;
        }
        curName = list_names[UnityEngine.Random.Range(0, list_names.Count)];
        winId = prefix + curName;

        Log.Out($"Loading override for {prefix}: {winId}");
        return winId;
    }

    private static void insert(List<string> list, string prefix, string name)
    {
        if (string.IsNullOrEmpty(name))
            return;
        Log.Out($"reading override for {prefix}: {name}");

        if (!list.Contains(name))
            list.Add(name);
    }

    private static readonly List<string> list_bg = new();
    private static readonly List<string> list_logos = new();
    private const string prefix_bg = "menuBackground";
    private const string prefix_logo = "mainMenuLogo";
    private static string cur_bg = string.Empty, cur_logo = string.Empty;
}
