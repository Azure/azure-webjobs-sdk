// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Hosting
{
    /// <summary>
    /// An <see cref="IOptionsFactory{TOptions}"/> decorator that logs options during creation.
    /// Options implementing <see cref="IOptionsFormatter"/> or with a registered <see cref="IOptionsFormatter{TOptions}"/>
    /// are formatted and buffered to an <see cref="IOptionsLoggingSource"/>.
    /// </summary>
    /// <typeparam name="TOptions">The options type to create.</typeparam>
    public sealed class LoggingOptionsFactory<TOptions> : IOptionsFactory<TOptions> where TOptions : class, new()
    {
        private readonly OptionsFactory<TOptions> _innerFactory;
        private readonly IOptionsLoggingSource _logSource;
        private readonly IOptionsFormatter<TOptions> _optionsFormatter;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingOptionsFactory{TOptions}"/> class.
        /// </summary>
        /// <param name="innerFactory">The underlying options factory.</param>
        /// <param name="logSource">The logging source to buffer log messages.</param>
        public LoggingOptionsFactory(OptionsFactory<TOptions> innerFactory, IOptionsLoggingSource logSource) :
            this(innerFactory, logSource, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingOptionsFactory{TOptions}"/> class.
        /// </summary>
        /// <param name="innerFactory">The underlying options factory.</param>
        /// <param name="logSource">The logging source to buffer log messages.</param>
        /// <param name="optionsFormatter">An optional formatter for formatting options that do not implement <see cref="IOptionsFormatter"/>.</param>
        public LoggingOptionsFactory(OptionsFactory<TOptions> innerFactory, IOptionsLoggingSource logSource, IOptionsFormatter<TOptions> optionsFormatter)
        {
            _innerFactory = innerFactory;
            _logSource = logSource;

            // This allows us to wrap behavior around an existing type. It will be null for types we don't log.
            _optionsFormatter = optionsFormatter;
        }

        /// <summary>
        /// Creates an options instance. If the options type implements <see cref="IOptionsFormatter"/> or has a
        /// registered <see cref="IOptionsFormatter{TOptions}"/>, the formatted options will be logged.
        /// </summary>
        /// <param name="name">The name of the options instance.</param>
        /// <returns>The created options instance.</returns>
        public TOptions Create(string name)
        {
            TOptions options = _innerFactory.Create(name);

            string formattedOptions = null;

            // See if we need to format these options, either from one of our Options
            // or from a registered IOptionsFormatter<TOptions>
            if (_optionsFormatter != null)
            {
                formattedOptions = _optionsFormatter.Format(options);
            }
            else if (options is IOptionsFormatter optionsFormatter)
            {
                formattedOptions = optionsFormatter.Format();
            }

            if (formattedOptions != null)
            {
                string logString = $"{typeof(TOptions).Name}{Environment.NewLine}{formattedOptions}";
                _logSource.LogOptions(logString);
            }

            return options;
        }
    }
}