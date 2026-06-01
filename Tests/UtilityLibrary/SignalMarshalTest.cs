using System.Threading;
using GdUnit4;
using static GdUnit4.Assertions;
using UtilityLibrary;

namespace Tests.UtilityLibrary;

[TestSuite]
public class SignalMarshalTest
{
    [TestCase]
    public void Initialize_CapturesCallingThread_AsMainThread()
    {
        SignalMarshal.Initialize();

        AssertThat(SignalMarshal.IsOnMainThread).IsTrue();
    }

    [TestCase]
    public void IsOnMainThread_OnBackgroundThread_ReturnsFalse()
    {
        SignalMarshal.Initialize();
        bool observed = true;

        var worker = new Thread(() =>
        {
            observed = SignalMarshal.IsOnMainThread;
        });
        worker.Start();
        worker.Join();

        AssertThat(observed).IsFalse();
    }
}
