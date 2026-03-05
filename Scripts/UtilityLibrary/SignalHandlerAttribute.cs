using System;

namespace UtilityLibrary
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class SignalHandlerAttribute : Attribute
    {
        public string SignalName { get; }

        public SignalHandlerAttribute(string signalName)
        {
            SignalName = signalName;
        }
    }
}
