using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

public class RandomBackgroundLoader
{
    public static void insert(string name)
    {
        if (name.StartsWith(prefix_name))
            insertName(name.TrimStart(prefix_name.ToCharArray()));
        else if (name.StartsWith(prefix_logo))
            insertLogo(name.TrimStart(prefix_logo.ToCharArray()));
        else
            return;
    }

    public static string getName()
    {
        string str;
        if (set_names.Count <= 0)
            str = string.Empty;
        else
            str = set_names.ElementAt<string>(rnd.Next(set_names.Count));
        cur_name = prefix_name + str;
        Log.Out("Loading background: " + cur_name);
        return str;
    }

    public static string getLogo()
    {
        string str;
        if (set_logos.Count <= 0)
            str = string.Empty;
        else
            str = set_logos.ElementAt<string>(rnd.Next(set_logos.Count));
        cur_logo = prefix_logo + str;
        Log.Out("Loading logo: " + cur_logo);
        return str;
    }

    private static void insertName(string name)
    {
        Log.Out("background name :" + name);
        if (name.Length <= 0)
            return;

        if (set_names.Contains(name))
            Log.Warning("Window group already exists: " + name + ".");
        else
            set_names.Add(name);
    }

    private static void insertLogo(string name)
    {
        Log.Out("logo name: " + name);
        if (name.Length <= 0)
            return;

        if (set_logos.Contains(name))
            Log.Warning("Window group already exists: " + name + ".");
        else
            set_logos.Add(name);
    }

    private static HashSet<string> set_names = new HashSet<string>();
    private static HashSet<string> set_logos = new HashSet<string>();
    private static readonly Random rnd = new Random();
    private const string prefix_name = "menuBackground";
    private const string prefix_logo = "mainMenuLogo";
    private static string cur_name = string.Empty, cur_logo = string.Empty;

    public static string Cur_name { get => cur_name; }
    public static string Cur_logo { get => cur_logo; }
}

