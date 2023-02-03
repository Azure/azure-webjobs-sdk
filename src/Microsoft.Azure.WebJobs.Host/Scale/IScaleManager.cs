// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    public interface IScaleManager
    {
        /// <summary>
        /// Gets aggrigated <see cref="ScaleStatus"> for all the trigger.
        /// </summary>
        /// <param name="context">The scale status context.</param>
        /// <returns></returns>
        Task<ScaleStatus> GetScaleStatusAsync(ScaleStatusContext context);

        /// <summary>
        /// Gets <see cref="ScaleStatus"> for each trigger.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        Task<IDictionary<string, ScaleStatus>> GetScaleStatusesAsync(ScaleStatusContext context);
    }
}
