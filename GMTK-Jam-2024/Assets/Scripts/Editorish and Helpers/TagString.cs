using System;
using UnityEngine;

[Serializable]
public struct TagString
{
    [TagSelector] public string str;
    public TagString(string str) => this.str = str;

    public static implicit operator string(TagString tagstr) => tagstr.str;
    public static implicit operator TagString(string str) => new TagString(str);
}