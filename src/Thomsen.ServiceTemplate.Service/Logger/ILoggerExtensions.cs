using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Thomsen.ServiceTemplate.Service.Logger {
    public static class ILoggerExtensions {
        public static void LogTrace(this ILogger logger, string message, [CallerMemberName] string facility = "") {
            logger.LogTrace("{facility}: {message}", facility, message);
        }

        public static void LogDebug(this ILogger logger, string message, [CallerMemberName] string facility = "") {
            logger.LogDebug("{facility}: {message}", facility, message);
        }

        public static void LogInformation(this ILogger logger, string message, [CallerMemberName] string facility = "") {
            logger.LogInformation("{facility}: {message}", facility, message);
        }

        public static void LogWarning(this ILogger logger, string message, [CallerMemberName] string facility = "") {
            logger.LogWarning("{facility}: {message}", facility, message);
        }

        public static void LogError(this ILogger logger, string message, [CallerMemberName] string facility = "") {
            logger.LogError("{facility}: {message}", facility, message);
        }

        public static void LogCritical(this ILogger logger, string message, [CallerMemberName] string facility = "") {
            logger.LogCritical("{facility}: {message}", facility, message);
        }

        public static void LogError(this ILogger logger, string message, Exception ex, [CallerMemberName] string facility = "") {
            logger.LogError(ex, "{facility}: {message}", facility, message);
        }

        public static void LogCritical(this ILogger logger, string message, Exception ex, [CallerMemberName] string facility = "") {
            logger.LogCritical(ex, "{facility}: {message}", facility, message);
        }
    }
}
