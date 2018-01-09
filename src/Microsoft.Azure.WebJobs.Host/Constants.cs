// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host
{
    internal static class Constants
    {
        public const string EnvironmentSettingName = "AzureWebJobsEnv";
        public const string DevelopmentEnvironmentValue = "Development";
        public const string ExtensionInitializationMessage = "If you're using binding extensions (e.g. ServiceBus, Timers, etc.) make sure you've called the registration method for the extension(s) in your startup code (e.g. config.UseServiceBus(), config.UseTimers(), etc.).";
        public const string UnableToBindParameterFormat = "Cannot bind parameter '{0}' to type {1}. Make sure the parameter Type is supported by the binding. {2}";
        public const string UnableToResolveBindingParameterFormat = "Unable to resolve binding parameter '{0}'. Binding expressions must map to either a value provided by the trigger or a property of the value the trigger is bound to, or must be a system binding expression (e.g. sys.randguid, sys.utcnow, etc.).";
        public const string BindingAssemblyConflictMessage = "Tried binding to '{0}' but user type assembly was '{1}.";
    }
}
