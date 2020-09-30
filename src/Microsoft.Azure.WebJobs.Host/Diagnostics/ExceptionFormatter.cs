// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// Adapted from code here: https://github.com/dotnet/roslyn/blob/master/src/VisualStudio/Core/Def/Implementation/Workspace/VisualStudioErrorReportingService.ExceptionFormatting.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Diagnostics
{
    /// <summary>
    /// Provides functionality to format and sanitize an exception for logging.
    /// </summary>
    public static class ExceptionFormatter
    {
        private static readonly string[] EmtpyArray = new string[0];

        /// <summary>
        /// Formats an exception for logging.
        /// </summary>
        /// <param name="exception">The exception to be formatted.</param>
        /// <returns>A string representation of the exception with a sanitized stack trace.</returns>
        public static string GetFormattedException(Exception exception)
        {
            var sb = new StringBuilder();
            GetFormattedException(sb, exception);
            return sb.ToString();
        }

        static void GetFormattedException(StringBuilder sb, Exception exception)
        {
            try
            {
                if (exception is AggregateException aggregate)
                {
                    GetStackForAggregateException(sb, exception, aggregate);
                }
                else
                {
                    GetStackForException(sb, exception, includeMessageOnly: false);
                }
            }
            catch (Exception)
            {
                if (exception != null)
                {
                    sb.Append(exception);
                }
            }
        }

        private static void GetStackForAggregateException(StringBuilder sb, Exception exception, AggregateException aggregate)
        {
            GetStackForException(sb, exception, includeMessageOnly: true);
            for (int i = 0; i < aggregate.InnerExceptions.Count; i++)
            {
                sb.AppendLine();
                sb.Append($"---> (Inner Exception #{i}) ");
                GetFormattedException(sb, aggregate.InnerExceptions[i]);
                sb.AppendLine("<---");
            }
        }

        private static void GetStackForException(StringBuilder sb, Exception exception, bool includeMessageOnly)
        {
            var message = exception.Message;
            var className = exception.GetType().ToString();
            sb.Append(message.Length <= 0
                ? className
                : className + " : " + message);
            var innerException = exception.InnerException;
            if (innerException != null)
            {
                if (includeMessageOnly)
                {
                    do
                    {
                        sb.Append(" ---> ");
                        sb.Append(innerException.Message);
                        innerException = innerException.InnerException;
                    }
                    while (innerException != null);
                }
                else
                {
                    sb.Append(" ---> ");
                    GetFormattedException(sb, innerException);
                    sb.AppendLine();
                    sb.Append("   End of inner exception");
                }
            }

            AddAsyncStackTrace(sb, exception);
        }

        private static void AddAsyncStackTrace(StringBuilder sb, Exception exception)
        {
            var stackTrace = new StackTrace(exception, fNeedFileInfo: true);
            var stackFrames = stackTrace.GetFrames();
            if (stackFrames == null)
            {
                return;
            }

            foreach (var frame in stackFrames)
            {
                FormatFrame(sb, frame);
            }
        }

        private static bool ShouldShowFrame(Type declaringType) =>
            !(declaringType != null && typeof(INotifyCompletion).IsAssignableFrom(declaringType));

        private static void FormatFrame(StringBuilder stringBuilder, StackFrame frame)
        {
            var method = frame.GetMethod();
            var declaringType = method?.DeclaringType;
            
            if (!ShouldShowFrame(declaringType))
            {
                return;
            }

            stringBuilder.AppendLine();

            stringBuilder.Append("   at ");
            bool isAsync;
            FormatMethodName(stringBuilder, declaringType, out isAsync);
            if (!isAsync)
            {
                stringBuilder.Append(method?.Name);
                var methodInfo = method as MethodInfo;
                if (methodInfo?.IsGenericMethod == true)
                {
                    FormatGenericArguments(stringBuilder, methodInfo.GetGenericArguments());
                }
            }
            else if (declaringType?.IsGenericType == true)
            {
                FormatGenericArguments(stringBuilder, declaringType.GetGenericArguments());
            }

            stringBuilder.Append("(");
            if (isAsync)
            {
                // Best effort
                string methodFromType = GetMethodFromAsyncStateMachineType(declaringType);
                List<MethodInfo> methods = null;
                if (methodFromType != null)
                {
                    methods = declaringType?.DeclaringType.GetMethods((BindingFlags)int.MaxValue)
                        .Where(m => string.Equals(m.Name, methodFromType))
                        .ToList();
                }

                if (methods != null && methods.Count == 1)
                {
                    FormatParameters(stringBuilder, methods.First());
                }
                else
                {
                    stringBuilder.Append("??");
                }
            }
            else
            {
                FormatParameters(stringBuilder, method);
            }

            stringBuilder.Append(")");

            FormatFileName(stringBuilder, frame);
        }

        private static void FormatFileName(StringBuilder stringBuilder, StackFrame frame)
        {
            try
            {
                var fileName = frame.GetFileName();
                if (fileName != null)
                {
                    stringBuilder.Append($" at {fileName} : {frame.GetFileLineNumber()}");
                }
            }
            catch
            {
                // If we're unable to get the file name, move on...
            }
        }

        private static void FormatMethodName(StringBuilder stringBuilder, Type declaringType, out bool isAsync)
        {
            if (declaringType == null)
            {
                isAsync = false;
                return;
            }

            var fullName = declaringType.FullName.Replace('+', '.');
            if (typeof(IAsyncStateMachine).GetTypeInfo().IsAssignableFrom(declaringType))
            {
                stringBuilder.Append("async ");
                var start = fullName.LastIndexOf('<');
                var end = fullName.LastIndexOf('>');
                if (start >= 0 && end >= 0)
                {
                    stringBuilder.Append(fullName, 0, start);
                    stringBuilder.Append(fullName, start + 1, end - start - 1);
                }
                else
                {
                    stringBuilder.Append(fullName);
                }

                isAsync = true;
            }
            else
            {
                stringBuilder.Append(fullName);
                stringBuilder.Append(".");
                isAsync = false;
            }
        }

        private static string GetMethodFromAsyncStateMachineType(Type type)
        {
            var fullName = type.FullName.Replace('+', '.');
            var start = fullName.LastIndexOf('<');
            var end = fullName.LastIndexOf('>') - 1;
            if (start >= 0 && end >= 0)
            {
                return fullName.Substring(start + 1, end - start);
            }

            return null;
        }

        private static void FormatGenericArguments(StringBuilder stringBuilder, Type[] genericTypeArguments)
        {
            if (genericTypeArguments.Length <= 0)
            {
                return;
            }

            stringBuilder.Append("[");
            stringBuilder.Append(string.Join(",", genericTypeArguments.Select(args => args.Name)));
            stringBuilder.Append("]");
        }

        private static void FormatParameters(StringBuilder stringBuilder, MethodBase method) =>
            stringBuilder.Append(string.Join(",", method?.GetParameters().Select(t => (t.ParameterType?.Name ?? "<UnknownType>") + " " + t.Name) ?? EmtpyArray));
    }
}
