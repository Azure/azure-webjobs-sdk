﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Scale
{

    /// <summary>
    /// Metadata descriptor for an <see cref="ITargetScaler"/>.
    /// </summary>
    public class TargetScalerDescriptor
    {
        public TargetScalerDescriptor(string functionId)
        {
            FunctionId = functionId;
        }

        /// <summary>
        /// Gets the ID of the function associated with this scaler.
        /// </summary>
        public string FunctionId { get; }

        /// <summary>
        /// Get or set configuation key name.
        /// </summary>
        /// <remarks>
        /// It is used to determinate if ScaleContorller suppots targed base scale on particular stamp.
        /// </remarks>
        public string ConfigurationKeyName { get; set; }
    }
}
