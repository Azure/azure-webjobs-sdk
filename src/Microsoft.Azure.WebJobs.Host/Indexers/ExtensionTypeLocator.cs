// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Indexers
{
    internal class ExtensionTypeLocator : IExtensionTypeLocator
    {
        private readonly ITypeLocator _typeLocator;
        private IReadOnlyList<Type> _cloudBlobStreamBinderTypes;
        private readonly static IReadOnlyList<Type> _emptyList = new List<Type>().AsReadOnly();

        public ExtensionTypeLocator(ITypeLocator typeLocator)
        {
            if (typeLocator == null)
            {
                throw new ArgumentNullException("typeLocator");
            }

            _typeLocator = typeLocator;
        }

        public IReadOnlyList<Type> GetCloudBlobStreamBinderTypes()
            => _emptyList;

        // Search for any types that implement ICloudBlobStreamBinder<T>
        internal static IReadOnlyList<Type> GetCloudBlobStreamBinderTypes(IEnumerable<Type> types)
            => _emptyList;
    }
}
