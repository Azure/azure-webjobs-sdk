// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    // Test helpers for testing converter manager. 
    // In product case, converters are added via the FluentBindingRules; 
    // and retrieved via the internal Binding providers. So we don't expect end-users to need these. 
    internal static class ConverterManagerExtensions
    {
        public delegate TDestination FuncConverter<TSource, TAttribute, TDestination>(TSource src, TAttribute attribute, ValueBindingContext context)
            where TAttribute : Attribute;

        // Provide a strong typed wrapper. 
        public static FuncAsyncConverter<TSource, TDestination> GetConverter<TSource, TDestination, TAttribute>(this IConverterManager converterManager)
            where TAttribute : Attribute
        {
            var func = converterManager.GetConverter<TAttribute>(typeof(TSource), typeof(TDestination));
            if (func == null)
            {
                return null;
            }
            return async (src, attr, ctx) =>
            {
                var result = await func((TSource)src, attr, ctx);
                return (TDestination)result;
            };
        }

        // Provide a sync wrapper for convenience in calling. 
        public static FuncConverter<TSource, TAttribute, TDestination> GetSyncConverter<TSource, TDestination, TAttribute>(
            this IConverterManager converterManager)
            where TAttribute : Attribute
        {
            var func = converterManager.GetConverter<TAttribute>(typeof(TSource), typeof(TDestination));
            if (func == null)
            {
                return null;
            }
            return (src, attr, ctx) =>
            {
                var result = Task.Run(() => func((TSource)src, attr, ctx)).GetAwaiter().GetResult();
                return (TDestination)result;
            };
        }

        /// <summary>
        /// Register a new converter function that applies for all attributes. 
        /// If TSource is object, then this converter is applied to any attempt to convert to TDestination. 
        /// </summary>
        /// <typeparam name="TSource">Source type.</typeparam>
        /// <typeparam name="TDestination">Destination type.</typeparam>
        /// <param name="converterManager"></param>
        /// <param name="converter">A function to convert from the source to the destination type.</param>
        public static void AddConverter<TSource, TDestination>(this ConverterManager converterManager, Func<TSource, TDestination> converter)
        {
            Func<TSource, Attribute, TDestination> func = (src, attr) => converter(src);
            converterManager.AddConverter(func);
        }

        /// <summary>
        /// Register a new converter function that is influenced by the attribute. 
        /// If TSource is object, then this converter is applied to any attempt to convert to TDestination. 
        /// </summary>
        /// <typeparam name="TSource">Source type.</typeparam>
        /// <typeparam name="TDestination">Destination type.</typeparam>
        /// <typeparam name="TAttribute">Attribute on the binding. </typeparam>
        /// <param name="converterManager"></param>
        /// <param name="converter">A function to convert from the source to the destination type.</param>
        public static void AddConverter<TSource, TDestination, TAttribute>(this ConverterManager converterManager, Func<TSource, TAttribute, TDestination> converter)
            where TAttribute : Attribute
        {
            FuncAsyncConverter func = (src, attr, context) => Task.FromResult<object>(converter((TSource)src, (TAttribute)attr));
            FuncConverterBuilder builder = (srcType, destType) => func;
            converterManager.AddConverter<TSource, TDestination, TAttribute>(builder);
        }

        /// <summary>
        /// Register a new converter function that is influenced by the attribute. 
        /// If TSource is object, then this converter is applied to any attempt to convert to TDestination. 
        /// </summary>
        /// <typeparam name="TSource">Source type.</typeparam>
        /// <typeparam name="TDestination">Destination type.</typeparam>
        /// <typeparam name="TAttribute">Attribute on the binding. </typeparam>
        /// <param name="converterManager"></param>
        /// <param name="converter">A function to convert from the source to the destination type.</param>
        public static void AddConverter<TSource, TDestination, TAttribute>(this ConverterManager converterManager, FuncAsyncConverter<TSource, TDestination> converter)
            where TAttribute : Attribute
        {
            FuncAsyncConverter func = (src, attr, context) => Task.FromResult<object>(converter((TSource)src, attr, context));
            FuncConverterBuilder builder = (srcType, destType) => func;
            converterManager.AddConverter<TSource, TDestination, TAttribute>(builder);
        }

        /// <summary>
        /// Register a new converter function that is influenced by the attribute. 
        /// If TSource is object, then this converter is applied to any attempt to convert to TDestination. 
        /// </summary>
        /// <typeparam name="TSource">Source type.</typeparam>
        /// <typeparam name="TDestination">Destination type.</typeparam>
        /// <typeparam name="TAttribute">Attribute on the binding. </typeparam>
        /// <param name="converterManager"></param>
        /// <param name="converter">A function to convert from the source to the destination type.</param>
        public static void AddConverter<TSource, TDestination, TAttribute>(this ConverterManager converterManager, FuncAsyncConverter converter)
            where TAttribute : Attribute
        {
            FuncConverterBuilder builder = (srcType, destType) => converter;
            converterManager.AddConverter<TSource, TDestination, TAttribute>(builder);
        }

        /// <summary>
        /// Add a converter for the given Source to Destination conversion.
        /// The typeConverter type is instantiated with the type arguments and constructorArgs is passed. 
        /// </summary>
        /// <typeparam name="TSource">Source type.</typeparam>
        /// <typeparam name="TDestination">Destination type.</typeparam>
        /// <typeparam name="TAttribute">Attribute on the binding. </typeparam>
        /// <param name="converterManager">Instance of Converter Manager.</param>
        /// <param name="typeConverter">A type with conversion methods. This can be generic and will get instantiated with the 
        /// appropriate type parameters. </param>
        /// <param name="constructorArgs">Constructor Arguments to pass to the constructor when instantiated. This can pass configuration and state.</param>
        public static void AddConverter<TSource, TDestination, TAttribute>(
            this ConverterManager converterManager,
            Type typeConverter,
            params object[] constructorArgs)
            where TAttribute : Attribute
        {
            var patternMatcher = PatternMatcher.New(typeConverter, constructorArgs);
            converterManager.AddConverterBuilder<TSource, TDestination, TAttribute>(patternMatcher);
        }

        /// <summary>
        /// Add a converter for the given Source to Destination conversion.
        /// </summary>
        /// <typeparam name="TSource">Source type.</typeparam>
        /// <typeparam name="TDestination">Destination type.</typeparam>
        /// <typeparam name="TAttribute">Attribute on the binding. </typeparam>
        /// <param name="converterManager">Instance of Converter Manager.</param>
        /// <param name="converterInstance">Instance of an object with convert methods on it.</param>
        public static void AddConverter<TSource, TDestination, TAttribute>(
          this ConverterManager converterManager,
          object converterInstance)
          where TAttribute : Attribute
        {
            var patternMatcher = PatternMatcher.NewObj(converterInstance);
            converterManager.AddConverterBuilder<TSource, TDestination, TAttribute>(patternMatcher);
        }

        private static void AddConverterBuilder<TSource, TDestination, TAttribute>(
          this ConverterManager converterManager,
          PatternMatcher patternMatcher)
          where TAttribute : Attribute
        {
            if (converterManager == null)
            {
                throw new ArgumentNullException("converterManager");
            }

            converterManager.AddConverter<TSource, TDestination, TAttribute>(
                (typeSource, typeDest) => patternMatcher.TryGetConverterFunc(typeSource, typeDest));
        }
    }
}