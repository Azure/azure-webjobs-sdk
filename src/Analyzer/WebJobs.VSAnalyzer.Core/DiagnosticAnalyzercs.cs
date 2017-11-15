using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Azure.WebJobs.Host;
using System.Text;
using ClassLibrary1;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.WebJobs;

namespace MyAnalyzer
{
#if false
    
    For each attribute, invoke the "Validate" on it. 

    What extensions are loaded? 

    Can we instantiate the attribute?

    Attr Type mismatch?
    1. 


#endif

    public class TTT
    {

        public static void Foo()
        {
            var x = new JobHostConfiguration();
        }
    }


    // Can't access the workspace. 
    // https://stackoverflow.com/questions/23203206/roslyn-current-workspace-in-diagnostic-with-code-fix-project

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MyAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MyAnalyzer";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        internal const string Title = "Regex error parsing string argument";
        internal const string MessageFormat = "Regex error {0}";
        internal const string Description = "Regex patterns should be syntactically valid.";
        internal const string Category = "Syntax";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);


        // $$$ This should be scoped to per-project 
        IJobHostMetadataProvider _tooling;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public static void Foo()
        {
            var x = new JobHostConfiguration();
        }

        public override void Initialize(AnalysisContext context)
        {

            Foo();

            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            // context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);

            context.RegisterSyntaxNodeAction(AnalyzeNode2, SyntaxKind.MethodDeclaration);

            context.RegisterCompilationStartAction(compilationAnalysisContext =>
            {
                var compilation = compilationAnalysisContext.Compilation;

                AssemblyCache.Instance.Build(compilation);
                this._tooling = AssemblyCache.Instance.Tooling;

                // cast to PortableExecutableReference which has a file path
                var x1 = compilation.References.OfType<PortableExecutableReference>().ToArray();
                var webJobsPath = (from reference in x1
                                   where IsWebJobsSdk(reference)
                                   select reference.FilePath).Single();

                var analyzer = new WebJobsAnalyzer(webJobsPath);
                compilationAnalysisContext.RegisterSyntaxNodeAction(
                    sytaxNodeAnalysisContext => analyzer.Analyze(sytaxNodeAnalysisContext),
                    SyntaxKind.Attribute);
            });


            {
                //Microsoft.Azure.WebJobs.JobHostConfiguration hostConfig = new Microsoft.Azure.WebJobs.JobHostConfiguration();
                //this._tooling = hostConfig.CreateMetadataProvider();
                //this._tooling = AssemblyCache.Instance.Tooling;
            }
        }

        private bool IsWebJobsSdk(PortableExecutableReference reference)
        {
            if (reference.FilePath.EndsWith("Microsoft.Azure.WebJobs.dll"))
            {
                return true;
            }
            return false;
        }

        //  This is called extremely frequently 
        private void AnalyzeNode2(SyntaxNodeAnalysisContext context)
        {
            if (_tooling == null) // Not yet initialized 
            {
                return;
            }
            var methodDecl = (MethodDeclarationSyntax)context.Node;
            var methodName = methodDecl.Identifier.ValueText;


            // Look at referenced assemblies. 
            if (false)
            {
                // $$$ No symbol - from users code. 
                var sym = context.SemanticModel.GetSymbolInfo(methodDecl);
                var sym2 = sym.Symbol;
                var asm = sym2.ContainingAssembly; // Assembly the user's method is defined in.

                foreach (var mod in asm.Modules)
                {
                    // Get referenced assemblies. 
                    foreach (var asm2 in mod.ReferencedAssemblySymbols)
                    {
                        var x = IsSdkAsssembly(asm2);
                    }
                }
            }


            // Go through 
            var parameterList = methodDecl.ParameterList;

            foreach (var param in parameterList.Parameters)
            {
                foreach (var attrList in param.AttributeLists)
                {
                    foreach (var attr in attrList.Attributes)
                    {
                        // For each attr. 
                        //  [Blob("container/blob")]
                        //  [Microsoft.Azure.WebJobs.BlobAttribute("container/blob")]

                                             
                        // Named args?



                        var sym = context.SemanticModel.GetSymbolInfo(attr);

                        var sym2 = sym.Symbol;
                        if (sym2 == null)
                        {
                            return; // compilation error
                        }

                        var attrType = sym2.ContainingType;

                        var attrNamespace = attrType.ContainingNamespace.ToString(); // "Microsoft.Azure.WebJobs"
                        var attrName = attrType.Name; // "BlobAttribute"

                        // if (attrName == "BlobAttribute")
                        {
                            // No symbol for the parameter; just the parameter's type
                            var paramSym = context.SemanticModel.GetSymbolInfo(param.Type);
                            var p2 = paramSym.Symbol;

                            if (p2 == null)
                            {
                                return;
                            }

                            try
                            {
                                Type fakeType = Class1.MakeFakeType(p2); // throws if can't convert. 

                                if (param.IsOutParameter())
                                {
                                    fakeType = fakeType.MakeByRefType();
                                }

                                Attribute result = Class1.MakeAttr(_tooling, context.SemanticModel, attr);

                                // Report errors from invalid attribute properties. 
                                ValidateAttribute(result, context, attr);                                

                                var errors = _tooling.CheckBindingErrors(result, fakeType);

                                if (errors != null)
                                {
                                    var sb = new StringBuilder();
                                    sb.Append($"Can't bind attribute {result.GetType().Name} to parameter type {fakeType.ToString()}. Possible options are:");
                                    foreach (var possible in errors)
                                    {
                                        sb.Append("\n  " + possible);
                                    }
                                    var diagnostic =
                                        Diagnostic.Create(Rule,
                                        param.GetLocation(), sb.ToString());

                                    context.ReportDiagnostic(diagnostic);
                                }
                            }
                            catch (Exception e)
                            {
                                return;
                            }
                        }

                        // check if base type is a binding? 
                        // Scan for [Binding] attribute on this type. 
                        var attr2 = attrType.GetAttributes();
                        foreach (var x in attr2)
                        {
                            var @namespace = x.AttributeClass.ContainingNamespace.ToString();
                            var @name = x.AttributeClass.Name;

                            // If "Binding", then ok. 
                        }
                    }
                }
            }
        }

        // Given an instantiated attribute, run the validators on it and report back any errors. 
        private void ValidateAttribute(Attribute result, SyntaxNodeAnalysisContext context, AttributeSyntax attrSyntax)
        {
            SemanticModel semantics = context.SemanticModel;
            var t = result.GetType();

            IMethodSymbol symAttributeCtor = (IMethodSymbol)semantics.GetSymbolInfo(attrSyntax).Symbol;
            var syntaxParams = symAttributeCtor.Parameters;

            int idx = 0;
            foreach (var arg in attrSyntax.ArgumentList.Arguments)
            {

                string argName = null;
                if (arg.NameColon != null)
                {
                    argName = arg.NameColon.Name.ToString();
                }
                else if (arg.NameEquals != null)
                {
                    argName = arg.NameEquals.Name.ToString();
                }
                else
                {
                    argName = syntaxParams[idx].Name; // Positional 
                }

                var propInfo = t.GetProperty(argName, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
                if (propInfo != null)
                {
                    var value = propInfo.GetValue(result);

                    // Validate 
                    {
                        var attrs = propInfo.GetCustomAttributes<ValidationAttribute>();

                        foreach (var attr in attrs)
                        {
                            try
                            {
                                attr.Validate(value, propInfo.Name);
                            }
                            catch (Exception e)
                            {

                                // throw new InvalidOperationException($"'{propInfo.Name}' can't be '{value}': {e.Message}");
                                var msg = $"{propInfo.Name} can't be value '{value}': {e.Message}";
                                
                                var diagnostic =
                                    Diagnostic.Create(Rule,
                                    arg.GetLocation(), msg);

                                context.ReportDiagnostic(diagnostic);
                            }
                        }
                    }
                }


                idx++;
            }            
        }

        private bool IsSdkAsssembly(IAssemblySymbol asm)
        {
            foreach (var mod in asm.Modules)
            {
                // Get referenced assemblies. 
                foreach (var asm2 in mod.ReferencedAssemblySymbols)
                {
                    // Is SDK? 

                }
            }
            return false;
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var invocationExpr = (InvocationExpressionSyntax)context.Node;

            // Quick syntax tests first. 
            var memberAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;

            if (memberAccessExpr?.Name.ToString() != "Match")
                return;

            var memberSymbol = context.SemanticModel.GetSymbolInfo(memberAccessExpr).Symbol as IMethodSymbol;

            if (!memberSymbol?.ToString().StartsWith("System.Text.RegularExpressions.Regex.Match") ?? true)
                return;

            var argumentList = invocationExpr.ArgumentList as ArgumentListSyntax;
            if ((argumentList?.Arguments.Count ?? 0) < 2)
                return;

            var regexLiteral = argumentList.Arguments[1].Expression as LiteralExpressionSyntax;
            if (regexLiteral == null)
                return;

            var regexOpt = context.SemanticModel.GetConstantValue(regexLiteral);
            if (!regexOpt.HasValue)
                return;
            var regex = regexOpt.Value as string;
            if (regex == null)
                return;

            try
            {
                System.Text.RegularExpressions.Regex.Match("", regex);
            }
            catch (ArgumentException e)
            {
                var diagnostic =
 Diagnostic.Create(Rule,
 regexLiteral.GetLocation(), e.Message);

                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            // TODO: Replace the following code with your own analysis, generating Diagnostic objects for any issues you find
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Find just those named type symbols with names containing lowercase letters.
            if (namedTypeSymbol.Name.ToCharArray().Any(char.IsLower))
            {
                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
