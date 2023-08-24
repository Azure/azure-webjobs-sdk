// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Represents a snapshot of the current concurrency status of a function.
    /// </summary>
    public class FunctionConcurrencySnapshot
    {
        /// <summary>
        /// Gets or sets the concurrency level of the function.
        /// </summary>
        public int Concurrency { get; set; }

        public override bool Equals(object obj) => Equals(obj as FunctionConcurrencySnapshot);

        private bool Equals(FunctionConcurrencySnapshot other)
        {
            if (other == null)
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return other.Concurrency == Concurrency;
        }

        public override int GetHashCode()
        {
            return Concurrency.GetHashCode();
        }
    }
}
