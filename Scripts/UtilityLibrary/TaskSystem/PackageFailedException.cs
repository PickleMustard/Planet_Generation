using System;

namespace UtilityLibrary.TaskSystem
{
    /// <summary>
    /// Exception thrown when a work package step fails after all retry attempts have been exhausted.
    /// </summary>
    public class PackageFailedException : Exception
    {
        /// <summary>
        /// Gets the name of the failed package.
        /// </summary>
        public string PackageName { get; }

        /// <summary>
        /// Gets the index of the step that failed.
        /// </summary>
        public int FailedStepIndex { get; }

        /// <summary>
        /// Gets the name of the step that failed.
        /// </summary>
        public string FailedStepName { get; }

        /// <summary>
        /// Gets the number of retry attempts made before failure.
        /// </summary>
        public int RetryAttempts { get; }

        /// <summary>
        /// Gets the error message describing the failure.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageFailedException"/> class.
        /// </summary>
        /// <param name="packageName">The name of the failed package.</param>
        /// <param name="failedStepIndex">The index of the step that failed.</param>
        /// <param name="failedStepName">The name of the step that failed.</param>
        /// <param name="retryAttempts">The number of retry attempts made.</param>
        /// <param name="errorMessage">The error message describing the failure.</param>
        public PackageFailedException(
            string packageName,
            int failedStepIndex,
            string failedStepName,
            int retryAttempts,
            string errorMessage
        )
            : base(
                $"Package '{packageName}' failed at step '{failedStepName}' (index {failedStepIndex}) after {retryAttempts} retry attempts: {errorMessage}"
            )
        {
            if (string.IsNullOrEmpty(packageName))
                throw new ArgumentNullException(nameof(packageName));
            if (string.IsNullOrEmpty(failedStepName))
                throw new ArgumentNullException(nameof(failedStepName));
            if (failedStepIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(failedStepIndex), "Step index cannot be negative");
            if (retryAttempts < 0)
                throw new ArgumentOutOfRangeException(nameof(retryAttempts), "Retry attempts cannot be negative");
            if (string.IsNullOrEmpty(errorMessage))
                throw new ArgumentNullException(nameof(errorMessage));

            PackageName = packageName;
            FailedStepIndex = failedStepIndex;
            FailedStepName = failedStepName;
            RetryAttempts = retryAttempts;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageFailedException"/> class with an inner exception.
        /// </summary>
        /// <param name="packageName">The name of the failed package.</param>
        /// <param name="failedStepIndex">The index of the step that failed.</param>
        /// <param name="failedStepName">The name of the step that failed.</param>
        /// <param name="retryAttempts">The number of retry attempts made.</param>
        /// <param name="errorMessage">The error message describing the failure.</param>
        /// <param name="innerException">The exception that caused the failure.</param>
        public PackageFailedException(
            string packageName,
            int failedStepIndex,
            string failedStepName,
            int retryAttempts,
            string errorMessage,
            Exception innerException
        )
            : base(
                $"Package '{packageName}' failed at step '{failedStepName}' (index {failedStepIndex}) after {retryAttempts} retry attempts: {errorMessage}",
                innerException
            )
        {
            if (string.IsNullOrEmpty(packageName))
                throw new ArgumentNullException(nameof(packageName));
            if (string.IsNullOrEmpty(failedStepName))
                throw new ArgumentNullException(nameof(failedStepName));
            if (failedStepIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(failedStepIndex), "Step index cannot be negative");
            if (retryAttempts < 0)
                throw new ArgumentOutOfRangeException(nameof(retryAttempts), "Retry attempts cannot be negative");
            if (string.IsNullOrEmpty(errorMessage))
                throw new ArgumentNullException(nameof(errorMessage));

            PackageName = packageName;
            FailedStepIndex = failedStepIndex;
            FailedStepName = failedStepName;
            RetryAttempts = retryAttempts;
            ErrorMessage = errorMessage;
        }
    }
}
