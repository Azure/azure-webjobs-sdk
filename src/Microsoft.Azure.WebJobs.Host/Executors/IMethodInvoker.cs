// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    /// <summary>
    /// Interface for efficiently invoking a method. See <see cref="MethodInvokerFactory"/>.
    /// </summary>
    /// <typeparam name="TReflected">The Type the method is defined on.</typeparam>
    /// <typeparam name="TReturnValue">The return type of the method. If the type returns a <see cref="Task{TResult}"/> the
    /// Type specified should be the inner type. If the method returns <see cref="void"/> or <see cref="Task"/> the
    /// Type specified should be <see cref="object"/>.</typeparam>
    public interface IMethodInvoker<TReflected, TReturnValue>
    {
        /// <summary>
        /// Invokes the method.
        /// </summary>
        /// <param name="instance">The instance to invoke the method on. If the method is static, pass <see cref="null"/>.</param>
        /// <param name="arguments">The array of method arguments. he cancellation token, if any, is provided along
        /// with the other arguments.</param>
        /// <returns></returns>
        Task<TReturnValue> InvokeAsync(TReflected instance, object[] arguments);
    }
}
