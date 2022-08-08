// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    // TODO Discuss the design. We already have IScaleMonitorProvider that is used inside of the WebJobs SDK.
    // For new Scale Controller New Design scenario, we need 

    /// <summary>
    /// Provide the IScaleMonitor for ScaleController
    /// </summary>
    public interface IScaleMonitorProviderForScaleController
    {
        IScaleMonitor Create(ScaleMonitorContext context);
    }
}
