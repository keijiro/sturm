using System.Reflection;
using UnityEngine;

// Usage: script run --entry Select.Preset --args '[index]'
public static class Select
{
    public static string Preset(int index)
    {
        var app = Object.FindFirstObjectByType<Sturm.AppController>();
        typeof(Sturm.AppController)
            .GetMethod("SelectEffect", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(app, new object[] { index });
        return $"selected {index}";
    }
}
