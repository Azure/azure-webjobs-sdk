// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Configuration;
using Autofac;
using Dashboard.Data;
using Xunit;


namespace Dashboard.UnitTests.Data
{
    public class AutoFacTests
    {
        private const string FunctionLogTableAppSettingName = "AzureWebJobsLogTableName";

        [Theory]
        [InlineData(FunctionLogTableAppSettingName, "logTestTable123")] // USe fast logging 
        [InlineData(FunctionLogTableAppSettingName, null)] // use classic SDK logging
        public void RegistrationTest(string appsetting, string value)
        {
            var oldSetting = ConfigurationManager.AppSettings[appsetting];
            try
            {
                ConfigurationManager.AppSettings[appsetting] = value;

                var container = MvcApplication.BuildContainer();

                // Verify we can create all the API & MVC controller classes. 
                // This is really testing that all dependencies are properly registered 
                container.Resolve<ApiControllers.DiagnosticsController>();
                container.Resolve<ApiControllers.FunctionsController>();
                container.Resolve<ApiControllers.LogController>();

                container.Resolve<Controllers.FunctionController>();
                container.Resolve<Controllers.MainController>();
            }
            finally
            {
                ConfigurationManager.AppSettings[appsetting] = oldSetting;
            }
        }
    }
}