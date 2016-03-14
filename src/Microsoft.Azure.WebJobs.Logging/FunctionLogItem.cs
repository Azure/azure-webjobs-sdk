// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Logging
{
    /// <summary>
    /// Publically visible. 
    /// Represent a function invocation. 
    /// </summary>
    public class FunctionLogItem
    {
        /// <summary>Gets or sets the function instance ID.</summary>
        public Guid FunctionInstanceId { get; set; }

        /// <summary>Gets or sets the Function ID of the ancestor function instance.</summary>
        public Guid? ParentId { get; set; }

        /// <summary></summary>
        public string FunctionName { get; set; }

        /// <summary>Gets or sets the time the function started executing.</summary>
        public DateTime StartTime { get; set; }

        /// <summary></summary>
        public DateTime? EndTime { get; set; }
                
        // Running, Completed
        /// <summary></summary>
        public bool IsCompleted()
        {
            return this.EndTime != null;
        }

        /// <summary>
        /// Null on success.
        /// Else, set to some string with error details. 
        /// </summary>
        public string ErrorDetails { get; set; }

        /// <summary></summary>
        public bool IsSucceeded()
        {
            return this.ErrorDetails == null;
        }

        /// <summary>Gets or sets the function's argument values and help strings.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public IDictionary<string, string> Arguments { get; set; }

        // Direct inline capture for small log outputs. For large log outputs, this is faulted over to a blob. 
        /// <summary></summary>
        public string LogOutput { get; set; }


        public string GetDisplayTitle()
        {
            IEnumerable<string> argumentValues = null;
            if (this.Arguments != null)
            {
                argumentValues = this.Arguments.Values;
            }

            return BuildFunctionDisplayTitle(this.FunctionName, argumentValues);
        }
        public static string BuildFunctionDisplayTitle(string functionName, IEnumerable<string> argumentValues)
        {
            var name = new StringBuilder(functionName);
            if (argumentValues != null)
            {
                string parametersDisplayText = String.Join(", ", argumentValues);
                if (parametersDisplayText != null)
                {
                    // Remove newlines to avoid 403/forbidden storage exceptions when saving display title to blob metadata
                    // for function indexes. Newlines may be present in JSON-formatted arguments.
                    parametersDisplayText = parametersDisplayText.Replace("\r\n", String.Empty);

                    name.Append(" (");
                    if (parametersDisplayText.Length > 20)
                    {
                        name.Append(parametersDisplayText.Substring(0, 18))
                            .Append(" ...");
                    }
                    else
                    {
                        name.Append(parametersDisplayText);
                    }
                    name.Append(")");
                }
            }

            return name.ToString();
        }
    }
}