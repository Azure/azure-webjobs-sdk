// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Bindings.Path
{
    /// <summary>
    /// A binding template class providing method of resolving parameterized template into a string by replacing
    /// template parameters with parameter values.
    /// </summary>
    [DebuggerDisplay("{Pattern,nq}")]
    public class BindingTemplate
    {
        private readonly string _pattern;
        private readonly IReadOnlyList<BindingTemplateToken> _tokens;
        private readonly bool _ignoreCase;
        private readonly ParameterResolver _parameterResolver;

        internal BindingTemplate(string pattern, IReadOnlyList<BindingTemplateToken> tokens, bool ignoreCase = false, ParameterResolver parameterResolver = null)
        {
            if (pattern == null)
            {
                throw new ArgumentNullException("pattern");
            }

            if (tokens == null)
            {
                throw new ArgumentNullException("tokens");
            }

            _pattern = pattern;
            _tokens = tokens;
            _ignoreCase = ignoreCase;
            _parameterResolver = parameterResolver ?? new DefaultParameterResolver();
        }

        /// <summary>
        /// Gets the binding pattern.
        /// </summary>
        public string Pattern
        {
            get { return _pattern; }
        }

        internal IEnumerable<BindingTemplateToken> Tokens
        {
            get { return _tokens; }
        }

        /// <summary>
        /// Gets the collection of parameter names this pattern applies to.
        /// </summary>
        public IEnumerable<string> ParameterNames
        {
            get
            {
                return Tokens.Where(p => p.IsParameter).Select(p => p.Value);
            }
        }

        /// <summary>
        /// A factory method to parse input template string and construct a binding template instance using
        /// parsed tokens sequence.
        /// </summary>
        /// <param name="input">A binding template string in a format supported by <see cref="BindingTemplateParser"/></param>
        /// <param name="parameterResolver">The optional <see cref="ParameterResolver"/> to use when resolving parameter values.</param>
        /// <param name="ignoreCase">True if matching should be case insensitive.</param>
        /// <returns>Valid ready-to-use instance of <see cref="BindingTemplate"/>.</returns>
        public static BindingTemplate FromString(string input, bool ignoreCase = false, ParameterResolver parameterResolver = null)
        {
            IReadOnlyList<BindingTemplateToken> tokens = BindingTemplateParser.ParseTemplate(input);
            return new BindingTemplate(input, tokens, ignoreCase: ignoreCase, parameterResolver: parameterResolver);
        }

        /// <summary>
        /// Resolves original parameterized template into a string by replacing parameters with values provided as
        /// a dictionary.
        /// </summary>
        /// <param name="parameters">Dictionary providing parameter values.</param>
        /// <returns>Resolved string if succeeded.</returns>
        /// <exception cref="InvalidOperationException">Thrown when required parameter value is not available.
        /// </exception>
        public string Bind(IReadOnlyDictionary<string, string> parameters)
        {
            return BindCore(parameters);
        }

        /// <summary>
        /// Resolves original parameterized template into a string by replacing parameters with values provided by
        /// the specified <see cref="BindingContext"/>.
        /// </summary>
        /// <param name="bindingContext">The <see cref="BindingContext"/> to use.</param>
        /// <returns>Resolved string if succeeded.</returns>
        /// <exception cref="InvalidOperationException">Thrown when required parameter value is not available.
        /// </exception>
        public string Bind(BindingContext bindingContext)
        {
            if (bindingContext == null)
            {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            if (bindingContext.BindingData == null || !ParameterNames.Any())
            {
                return Pattern;
            }

            var convertedParameters = BindingDataPathHelper.ConvertParameters(bindingContext.BindingData);

            return BindCore(convertedParameters, bindingContext);
        }

        private string BindCore(IReadOnlyDictionary<string, string> parameters, BindingContext bindingContext = null)
        {
            if (_ignoreCase && parameters != null)
            {
                // convert to a case insensitive dictionary
                var caseInsensitive = new Dictionary<string, string>(parameters.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var pair in parameters)
                {
                    caseInsensitive.Add(pair.Key, pair.Value);
                }
                parameters = caseInsensitive;
            }

            StringBuilder builder = new StringBuilder();
            foreach (BindingTemplateToken token in Tokens)
            {
                if (token.IsParameter)
                {
                    var resolverContext = new ParameterResolverContext
                    {
                        ParameterIndex = token.Index,
                        ParameterName = token.Value,
                        Template = _pattern,
                        BindingData = parameters,
                        Properties = bindingContext?.Properties
                    };

                    if (!_parameterResolver.TryResolve(resolverContext))
                    {
                        throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "No value for named parameter '{0}'.", token.Value));
                    }

                    builder.Append(resolverContext.Value);
                }
                else
                {
                    builder.Append(token.Value);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Gets a string representation of the binding template.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return _pattern;
        }
    }
}
