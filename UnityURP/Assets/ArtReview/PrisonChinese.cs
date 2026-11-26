using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public static class PrisonChinese
{
    private static string[][] entries;
    public static Font Font => Resources.Load<Font>("PrisonChinese");
    public static string T(string text)
    {
        if(string.IsNullOrEmpty(text))return "";
        entries ??= Resources.Load<TextAsset>("Chinese").text.Split('\n')
            .Select(s=>s.TrimEnd('\r').Split('\t')).Where(a=>a.Length==2).OrderByDescending(a=>a[0].Length).ToArray();
        foreach(var pair in entries) text=text.Replace(pair[0],pair[1]);
        return Regex.Replace(text,@"(?<=\d)s\b","秒");
    }
}

