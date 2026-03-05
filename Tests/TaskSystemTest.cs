using System;
using System.Collections.Generic;
using GdUnit4;
using Godot;
using UtilityLibrary.TaskSystem;
using static GdUnit4.Assertions;

namespace Tests;

[TestSuite]
public class TaskSystemTest
{
    [TestCase]
    public void PackageFailedException_ConstructorWithAllParameters()
    {
        var ex = new PackageFailedException("TestPackage", 2, "TestStep", 3, "Test error message");

        AssertThat(ex.PackageName).IsEqual("TestPackage");
        AssertThat(ex.FailedStepIndex).IsEqual(2);
        AssertThat(ex.FailedStepName).IsEqual("TestStep");
        AssertThat(ex.RetryAttempts).IsEqual(3);
        AssertThat(ex.ErrorMessage).IsEqual("Test error message");
        AssertThat(ex.Message).Contains("TestPackage");
        AssertThat(ex.Message).Contains("TestStep");
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentNullException))]
    public void PackageFailedException_ConstructorThrowsOnNullPackageName()
    {
        new PackageFailedException(null!, 0, "Step", 1, "Error");
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentNullException))]
    public void PackageFailedException_ConstructorThrowsOnNullStepName()
    {
        new PackageFailedException("Package", 0, null!, 1, "Error");
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentOutOfRangeException))]
    public void PackageFailedException_ConstructorThrowsOnNegativeStepIndex()
    {
        new PackageFailedException("Package", -1, "Step", 1, "Error");
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentOutOfRangeException))]
    public void PackageFailedException_ConstructorThrowsOnNegativeRetryAttempts()
    {
        new PackageFailedException("Package", 0, "Step", -1, "Error");
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentNullException))]
    public void PackageFailedException_ConstructorThrowsOnNullErrorMessage()
    {
        new PackageFailedException("Package", 0, "Step", 1, null!);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WorkPackage_ExecuteSingleStepSuccessfully()
    {
        bool stepExecuted = false;
        var steps = new List<WorkStep>
        {
            new WorkStep(
                "Step1",
                () =>
                {
                    stepExecuted = true;
                }
            ),
        };

        var package = new WorkPackage("TestPackage", steps, TaskPriority.Normal);

        AssertThat(package.Name).IsEqual("TestPackage");
        AssertThat(package.TotalSteps).IsEqual(1);
        AssertThat(package.IsComplete).IsFalse();
        AssertThat(package.IsFailed).IsFalse();
        AssertThat(package.CurrentStepIndex).IsEqual(0);

        int result = package.ExecuteNextStep();

        AssertThat(result).IsEqual(0);
        AssertThat(stepExecuted).IsTrue();
        AssertThat(package.IsComplete).IsTrue();
        AssertThat(package.CurrentStepIndex).IsEqual(1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WorkPackage_RetryLogicRetriesOnFailure()
    {
        int attemptCount = 0;
        var steps = new List<WorkStep>
        {
            new WorkStep(
                "FailingStep",
                () =>
                {
                    attemptCount++;
                    if (attemptCount < 3)
                    {
                        throw new Exception("Simulated failure");
                    }
                }
            ),
        };

        var package = new WorkPackage("TestPackage", steps, TaskPriority.Normal, null, 3);
        int result = package.ExecuteNextStep();

        AssertThat(result).IsEqual(0);
        AssertThat(package.IsComplete).IsTrue();
        AssertThat(attemptCount).IsEqual(3);
    }

    [TestCase]
    [RequireGodotRuntime]
    [ThrowsException(typeof(PackageFailedException))]
    public void WorkPackage_ThrowsExceptionAfterMaxRetries()
    {
        var steps = new List<WorkStep>
        {
            new WorkStep(
                "AlwaysFailingStep",
                () =>
                {
                    throw new Exception("Always fails");
                }
            ),
        };

        var package = new WorkPackage("TestPackage", steps, TaskPriority.Normal, null, 2);
        package.ExecuteNextStep();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WorkPackage_MaxRetriesPropertyValidation()
    {
        var steps = new List<WorkStep> { new WorkStep("Step1", () => { }) };

        var package = new WorkPackage("Test", steps, TaskPriority.Normal, null, 5);
        AssertThat(package.MaxRetries).IsEqual(5);

        package.MaxRetries = 10;
        AssertThat(package.MaxRetries).IsEqual(10);

        package.MaxRetries = 0;
        AssertThat(package.MaxRetries).IsEqual(1);

        package.MaxRetries = -5;
        AssertThat(package.MaxRetries).IsEqual(1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WorkPackage_ResetClearsState()
    {
        var steps = new List<WorkStep>
        {
            new WorkStep("Step1", () => { }),
            new WorkStep("Step2", () => { }),
        };

        var package = new WorkPackage("TestPackage", steps, TaskPriority.Normal, null, 3);

        package.ExecuteNextStep();
        AssertThat(package.CurrentStepIndex).IsEqual(1);

        package.Reset();
        AssertThat(package.CurrentStepIndex).IsEqual(0);
        AssertThat(package.IsComplete).IsFalse();
        AssertThat(package.IsFailed).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    [ThrowsException(typeof(ArgumentNullException))]
    public void WorkPackage_ConstructorThrowsOnNullName()
    {
        new WorkPackage(null!, new List<WorkStep>(), TaskPriority.Normal);
    }

    [TestCase]
    [RequireGodotRuntime]
    [ThrowsException(typeof(ArgumentNullException))]
    public void WorkPackage_ConstructorThrowsOnNullSteps()
    {
        new WorkPackage("Test", null!, TaskPriority.Normal);
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentOutOfRangeException))]
    public void WorkPackageBuilder_WithMaxRetriesThrowsOnZero()
    {
        var builder = new WorkPackageBuilder();
        builder.WithMaxRetries(0);
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentOutOfRangeException))]
    public void WorkPackageBuilder_WithMaxRetriesThrowsOnNegative()
    {
        var builder = new WorkPackageBuilder();
        builder.WithMaxRetries(-1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WorkPackageBuilder_WithMaxRetriesSetsValue()
    {
        var builder = new WorkPackageBuilder()
            .WithName("TestPackage")
            .AddStep("Step1", () => { })
            .WithMaxRetries(5);

        var package = builder.Build();
        AssertThat(package.MaxRetries).IsEqual(5);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WorkPackageBuilder_MethodChaining()
    {
        var builder = new WorkPackageBuilder()
            .WithName("TestPackage")
            .WithPriority(TaskPriority.High)
            .WithBatchId("batch1")
            .WithMaxRetries(5)
            .AddStep("Step1", () => { })
            .AddStep("Step2", () => { })
            .OnCompleted(name => { })
            .OnStepCompleted((name, index, stepName) => { })
            .OnStepFailed((name, index, stepName, error) => { })
            .OnFailed((name, error) => { });

        AssertThat(builder).IsNotNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    [ThrowsException(typeof(InvalidOperationException))]
    public void WorkPackageBuilder_BuildThrowsOnMissingName()
    {
        var builder = new WorkPackageBuilder().AddStep("Step1", () => { });

        builder.Build();
    }

    [TestCase]
    [RequireGodotRuntime]
    [ThrowsException(typeof(InvalidOperationException))]
    public void WorkPackageBuilder_BuildThrowsOnNoSteps()
    {
        var builder = new WorkPackageBuilder().WithName("TestPackage");

        builder.Build();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WorkPackage_StepNamesArrayPopulated()
    {
        var steps = new List<WorkStep>
        {
            new WorkStep("FirstStep", () => { }),
            new WorkStep("SecondStep", () => { }),
            new WorkStep("ThirdStep", () => { }),
        };

        var package = new WorkPackage("TestPackage", steps);

        AssertThat(package.StepNames.Length).IsEqual(3);
        AssertThat(package.StepNames[0]).IsEqual("FirstStep");
        AssertThat(package.StepNames[1]).IsEqual("SecondStep");
        AssertThat(package.StepNames[2]).IsEqual("ThirdStep");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WorkPackage_ProgressCalculation()
    {
        var steps = new List<WorkStep>
        {
            new WorkStep("Step1", () => { }),
            new WorkStep("Step2", () => { }),
            new WorkStep("Step3", () => { }),
            new WorkStep("Step4", () => { }),
        };

        var package = new WorkPackage("TestPackage", steps);

        AssertThat(package.Progress).IsEqual(0f);

        package.ExecuteNextStep();
        AssertThat(package.Progress).IsEqual(0.25f);

        package.ExecuteNextStep();
        AssertThat(package.Progress).IsEqual(0.5f);

        package.ExecuteNextStep();
        AssertThat(package.Progress).IsEqual(0.75f);

        package.ExecuteNextStep();
        AssertThat(package.Progress).IsEqual(1f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WorkPackage_EmptyStepsProgress()
    {
        var steps = new List<WorkStep>();
        var package = new WorkPackage("TestPackage", steps);

        AssertThat(package.Progress).IsEqual(0f);
        AssertThat(package.TotalSteps).IsEqual(0);
        AssertThat(package.IsComplete).IsTrue();
    }
}
