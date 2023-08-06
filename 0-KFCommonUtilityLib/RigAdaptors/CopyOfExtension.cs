using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class CopyOfExtension
{
    public static Transform FindInAllChilds(this Transform target, string name, bool onlyActive = false)
    {
        if (name == null)
        {
            return null;
        }

        if (!onlyActive || (onlyActive && (bool)target.gameObject && target.gameObject.activeSelf))
        {
            if (target.name == name)
            {
                return target;
            }

            for (int i = 0; i < target.childCount; i++)
            {
                Transform transform = target.GetChild(i).FindInAllChilds(name, onlyActive);
                if (transform != null)
                {
                    return transform;
                }
            }

            return null;
        }

        return null;
    }
}