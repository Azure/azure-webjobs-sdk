// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    /// Defines an <see cref="IValueBinder"/> that provides an ordering hint.
    /// An <c>IValueBinder</c> instance that does not implement this iface is assumed to have
    /// a <c>StepOrder</c> = <seealso cref="BindStepOrder.Default" />.<br />
    /// <br />
    /// The <c>StepOrder</c> hint influences in which order the value-providers and value-binders
    /// are executed before and after the function invocation respectively:<br />
    /// <br />
    /// The value providers/binders are first assigned into buckets according to their <c>StepOrder</c> values.
    /// <ul>
    ///   <li>Within the buckets, the value providers/binders are executed in the order in which
    ///   the respective parameters are declared (invocation order).</li>
    ///   
    ///   <li>Before function invocation, the value provider buckets are executed in this order:<br />
    ///   (<seealso cref="BindStepOrder.Enqueue" /> | <seealso cref="BindStepOrder.Default" />)-bucket THEN (<seealso cref="BindStepOrder.TightFunctionWrapper" />)-bucket</li>
    ///   
    ///   <li>After function invocation, the value binder buckets are executed in this order:<br />
    ///   (<seealso cref="BindStepOrder.TightFunctionWrapper" />)-bucket THEN (<seealso cref="BindStepOrder.Default" />)-bucket THEN (<seealso cref="BindStepOrder.Enqueue" />)-bucket</li>
    /// </ul>
    /// 
    /// </summary>
    public interface IOrderedValueBinder : IValueBinder
    {
        /// <summary>
        /// Gets the bind order for the binder.
        /// </summary>
        BindStepOrder StepOrder { get; }
    }
}
