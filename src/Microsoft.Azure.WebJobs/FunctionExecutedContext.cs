// FunctionExecutedContext.cs

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// The context for an executed function. This needs to be expanded on later.
    /// </summary>
    public class FunctionExecutedContext
    {
        /// <summary>Gets or sets the ID of the function.</summary>
        public string Id { get; set; }

        /// <summary>Gets or sets the fully qualified name of the function.</summary>
        public string FullName { get; set; }

#pragma warning disable CS3001 // Argument type is not CLS-compliant
        /// <summary>
        /// 
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="FullName"></param>
        public FunctionExecutedContext(string Id, string FullName)
#pragma warning restore CS3001 // Argument type is not CLS-compliant
        {

        }
    }
}