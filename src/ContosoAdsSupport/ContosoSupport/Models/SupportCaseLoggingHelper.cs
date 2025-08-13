namespace ContosoSupport.Models
{
    public static class SupportCaseLoggingHelper
    {
        private static readonly Action<ILogger, string, string, string, string?, Exception?> LogCreateFailureAction =
            LoggerMessage.Define<string, string, string, string?>(
                LogLevel.Warning,
                new EventId(1, "CreateFailure"),
                "Unhandled Exception thrown creating support case {subscriptionId} {resourceGroup} {resourceId} {id}.");

        private static readonly Action<ILogger, string, string, string, string?, Exception?> LogReadFailureAction =
            LoggerMessage.Define<string, string, string, string?>(
                LogLevel.Warning,
                new EventId(2, "ReadFailure"),
                "Unhandled Exception thrown reading support case {subscriptionId} {resourceGroup} {resourceId} {id}.");

        private static readonly Action<ILogger, string, string, string, string, Exception?> LogUpdateFailureAction =
            LoggerMessage.Define<string, string, string, string>(
                LogLevel.Warning,
                new EventId(3, "UpdateFailure"),
                "Unhandled Exception thrown updating support case {subscriptionId} {resourceGroup} {resourceId} {id}.");

        private static readonly Action<ILogger, string, string, string, string, Exception?> LogRemoveFailureAction =
            LoggerMessage.Define<string, string, string, string>(
                LogLevel.Warning,
                new EventId(4, "RemoveFailure"),
                "Unhandled Exception thrown removing support case {subscriptionId} {resourceGroup} {resourceId} {id}.");

        public static void LogCreateFailure(ILogger logger, string subscriptionId, string resourceGroup, string resourceId, string? id, Exception exception)
        {
            LogCreateFailureAction(logger, subscriptionId, resourceGroup, resourceId, id, exception);
        }

        public static void LogReadFailure(ILogger logger, string subscriptionId, string resourceGroup, string resourceId, string? id, Exception exception)
        {
            LogReadFailureAction(logger, subscriptionId, resourceGroup, resourceId, id, exception);
        }

        public static void LogUpdateFailure(ILogger logger, string subscriptionId, string resourceGroup, string resourceId, string id, Exception exception)
        {
            LogUpdateFailureAction(logger, subscriptionId, resourceGroup, resourceId, id, exception);
        }

        public static void LogRemoveFailure(ILogger logger, string subscriptionId, string resourceGroup, string resourceId, string id, Exception exception)
        {
            LogRemoveFailureAction(logger, subscriptionId, resourceGroup, resourceId, id, exception);
        }
    }
}
