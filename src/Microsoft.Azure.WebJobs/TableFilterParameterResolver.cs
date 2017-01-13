// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Class used to perform binding template parameter resolution and validation
    /// for the <see cref="TableAttribute.Filter"/>.
    /// </summary>
    public class TableFilterParameterResolver : ParameterResolver
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="inner">The inner resolver.</param>
        public TableFilterParameterResolver(ParameterResolver inner) : base(inner)
        {
        }

        /// <inheritdoc/>
        public override bool TryResolve(ParameterResolverContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (InnerResolver.TryResolve(context))
            {
                if (!ParameterIsValid(context))
                {
                    throw new InvalidOperationException($"An invalid parameter value was specified for filter parameter '{context.ParameterName}'");
                }
                return true;
            }

            return false;
        }

        internal static bool ParameterIsValid(ParameterResolverContext context)
        {
            // The set of supported operations for Table Service is very
            // limited. See: https://docs.microsoft.com/en-us/rest/api/storageservices/fileservices/querying-tables-and-entities
            // The simple checks below are just a first level of validation - we allow Table Storage to do any
            // remaining validation.

            bool isStringLiteral = false;
            int idxStartQuote = context.ParameterIndex - 1;
            int idxEndQuote = context.ParameterIndex + context.ParameterName.Length + 2;
            if ((idxStartQuote >= 0 && context.Template[idxStartQuote] == '\'') &&
                (idxEndQuote < context.Template.Length && context.Template[idxEndQuote] == '\''))
            {
                // we know we have a parameter enclosed in single quotes
                // this might be a string literal, or it might be a string
                // literal preceeded by a type keyword, e.g. datetime'{x}', guid'{x}' expressions.
                isStringLiteral = true;
            }

            if (isStringLiteral)
            {
                // quotes must be represented as two single quotes (e.g. o''clock)
                if (ContainsUnescapedSingleQuotes(context.Value))
                {
                    return false;
                }
            }
            else
            {
                // no spaces or other special characters are allowed
                // i.e. the value must be parsable of the OData data
                // types { long, int, double, guid, string, date, bool }
                foreach (char c in context.Value)
                {
                    if (c == ' ')
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        internal static bool ContainsUnescapedSingleQuotes(string value)
        {
            int idx = 0;
            while (idx < value.Length)
            {
                if (value[idx++] == '\'' && (idx == value.Length || value[idx++] != '\''))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
