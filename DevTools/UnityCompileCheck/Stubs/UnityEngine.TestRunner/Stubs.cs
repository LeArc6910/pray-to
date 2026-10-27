// Stand-ins for the parts of Unity Test Framework 1.8 (com.unity.test-framework, assembly UnityEngine.TestRunner).
// Signatures are copied from the package source; nothing here runs.
using System;

namespace UnityEngine.TestTools
{
    [AttributeUsage(AttributeTargets.Method)]
    public class UnityTestAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class UnityTearDownAttribute : Attribute
    {
    }
}
