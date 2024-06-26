﻿using System.Collections.Generic;
using System.Linq;

namespace Display.Helper.FileProperties.Name;

public class MatchHelper
{
    public static string Int32Subtract(int int1, int int2)
    {
        return (int1 - int2).ToString();
    }

    public static string GetSameFirstStringFromList(IReadOnlyCollection<string> list)
    {
        string sameContent = null;

        if (!list.Any()) return string.Empty;

        var firstStr = list.First();
        if (list.Count == 1) return firstStr;

        var otherContent = list.Skip(1).ToArray();

        var isSame = true;
        HashSet<string> matchList = new();
        foreach (var c in firstStr)
        {
            if (!isSame)
            {
                sameContent = null;
            }
            var tmp = sameContent + c;

            isSame = otherContent.All(j => j.Contains(tmp));

            if (isSame)
            {
                sameContent = tmp;
            }
            else if (!string.IsNullOrEmpty(sameContent))
            {
                matchList.Add(sameContent);
            }
        }

        if (matchList.Count == 0 && !string.IsNullOrEmpty(sameContent)) return sameContent;

        sameContent = matchList.MaxBy(x => x.Length);
        return sameContent ?? string.Empty;
    }

}
