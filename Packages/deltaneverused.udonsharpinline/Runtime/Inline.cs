using System;
using JetBrains.Annotations;

namespace UdonSharpInline {
    [PublicAPI]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class InlineAttribute : Attribute { }
}