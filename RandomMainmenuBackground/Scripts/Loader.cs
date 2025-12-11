using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

public class RandomBackgroundLoader
{
    public static void insert(string name)
    {
        if (name.StartsWith(prefix_bg))
            insert(list_bg, prefix_bg, name.Substring(prefix_bg.Length));
        else if (name.StartsWith(prefix_logo))
            insert(list_logos, prefix_logo,name.Substring(prefix_logo.Length));
        else
            return;
    }

    public static string modWindowName(string name, GUIWindowManager wm)
    {
        if (name == prefix_bg)
            return getCurrentOrNext(wm, list_bg, prefix_bg, ref cur_bg);
        if (name == prefix_logo)
            return getCurrentOrNext(wm, list_logos, prefix_logo, ref cur_logo);
        return name;
    }

    private static string getCurrentOrNext(GUIWindowManager wm, List<string> list_names, string prefix, ref string curName)
    {
        string winId = prefix + curName;
        if (list_names.Count <= 0)
        {
            Log.Out($"No override found for {prefix}, loading default.");
            return prefix;
        }
        else if (wm.IsWindowOpen(winId))
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

        if (list.Contains(name))
            Log.Warning($"Window group already exists: {prefix + name}.");
        else
            list.Add(name);
    }

    private static List<string> list_bg = new();
    private static List<string> list_logos = new();
    private const string prefix_bg = "menuBackground";
    private const string prefix_logo = "mainMenuLogo";
    private static string cur_bg = string.Empty, cur_logo = string.Empty;
}

