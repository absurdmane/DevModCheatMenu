using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace DevMenu
{
    /// <summary>
    /// A static class of buttons in a plugin, found by name in the namespace of its plugin class (<c>DevMenu</c> for the Mods tab,
    /// <c>DebugScripts</c> for the Debug tab): every public static void method without parameters is a button (label: its
    /// [Description], else the name), an optional <c>public static string Status()</c> is a live line. Neither side references the other.
    /// </summary>
    internal sealed class ButtonClass
    {
        public MethodInfo Status;
        public List<(string Label, MethodInfo Method)> Buttons;

        /// <summary>Null when the plugin has no such static class.</summary>
        public static ButtonClass Find(Type pluginType, string className)
        {
            Type type = pluginType.Assembly.GetType(pluginType.Namespace + "." + className);
            // Only a static class: an unrelated class that happens to have this name isn't taken for a section.
            if (type == null || !(type.IsAbstract && type.IsSealed))
                return null;
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            return new ButtonClass
            {
                Status = methods.FirstOrDefault(m => m.Name == "Status" && m.ReturnType == typeof(string) && m.GetParameters().Length == 0),
                Buttons = methods
                    .Where(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 0)
                    .Select(m => (m.GetCustomAttribute<DescriptionAttribute>()?.Description ?? m.Name, m))
                    .ToList()
            };
        }

        /// <summary>Calls a button or Status; an exception is logged and comes back as a "failed" text, the menu keeps working.</summary>
        public static object Invoke(MethodInfo method)
        {
            try
            {
                return method.Invoke(null, null);
            }
            catch (Exception e)
            {
                Exception inner = e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e;
                Plugin.Log.LogError($"{method.DeclaringType?.FullName}.{method.Name} failed: {inner}");
                return $"{method.Name} failed: {inner.Message}";
            }
        }
    }
}
