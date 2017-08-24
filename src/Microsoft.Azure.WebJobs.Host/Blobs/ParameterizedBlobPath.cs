// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;

namespace Microsoft.Azure.WebJobs.Host.Blobs
{
    internal class ParameterizedBlobPath : IBindableBlobPath
    {
        private readonly BindingTemplate _containerNameTemplate;
        private readonly BindingTemplate _blobNameTemplate;
        private readonly BindingTemplate _urlBindingTemplate;

        public ParameterizedBlobPath(BindingTemplate urlBindingTemplate)
        {
            Debug.Assert(urlBindingTemplate != null);

            _urlBindingTemplate = urlBindingTemplate;
            _containerNameTemplate = null;
            _blobNameTemplate = null;
        }

        public ParameterizedBlobPath(BindingTemplate containerNameTemplate, BindingTemplate blobNameTemplate)
        {
            Debug.Assert(containerNameTemplate != null);
            Debug.Assert(blobNameTemplate != null);

            _containerNameTemplate = containerNameTemplate;
            _blobNameTemplate = blobNameTemplate;
            _urlBindingTemplate = null; // does not need to initialize, since we don't have property
        }

        public string ContainerNamePattern
        {
            get
            {
                if (_urlBindingTemplate != null)
                {
                    // return template string for container
                    return _urlBindingTemplate.Pattern + ".getContainer()";
                }
                return _containerNameTemplate.Pattern;
            }
        }

        public string BlobNamePattern
        {
            get
            {
                if (_urlBindingTemplate != null)
                {
                    // return template string for blob
                    return _urlBindingTemplate.Pattern + ".getBlob()";
                }
                return _blobNameTemplate.Pattern;
            }
        }

        public bool IsBound
        {
            get { return false; }
        }

        public IEnumerable<string> ParameterNames
        {
            get
            {
                if (_urlBindingTemplate != null)
                {
                    return _urlBindingTemplate.ParameterNames;
                }
                return _containerNameTemplate.ParameterNames.Concat(_blobNameTemplate.ParameterNames);
            }
        }

        public BlobPath Bind(IReadOnlyDictionary<string, object> bindingData)
        {
            if (_urlBindingTemplate != null)
            {
                string url = _urlBindingTemplate.Bind(bindingData);
                return BlobPath.ParseAndValidate(BlobPath.ConvertAbsUrlToContainerBlob(url));
            }
            else
            {
                string containerName = _containerNameTemplate.Bind(bindingData);
                string blobName = _blobNameTemplate.Bind(bindingData);
                BlobClient.ValidateContainerName(containerName);
                if (!string.IsNullOrEmpty(_blobNameTemplate.Pattern))
                {
                    BlobClient.ValidateBlobName(blobName);
                }

                return new BlobPath(containerName, blobName);
            }
        }

        public override string ToString()
        {
            if (_urlBindingTemplate != null)
            {
                return _urlBindingTemplate.Pattern;
            }
            return _containerNameTemplate.Pattern + "/" + _blobNameTemplate.Pattern;
        }
    }
}
