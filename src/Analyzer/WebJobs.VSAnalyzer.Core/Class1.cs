using ClassLibrary1;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MyAnalyzer
{
    static class Class1
    {
        public static bool TryMapAssembly(IAssemblySymbol asm, out System.Reflection.Assembly asmRef)
        {
            asmRef = null;
#if false
            var t = typeof(Microsoft.Azure.WebJobs.BlobAttribute);
            var asmName = asm.Identity.Name;

            if (asmName == "Microsoft.Azure.WebJobs")
            {
                asmRef = t.Assembly; 
            }
            return (asmRef != null);
#else                        
            return AssemblyCache.Instance.TryMapAssembly(asm, out asmRef);

#endif

        }

        public static System.Reflection.Assembly MapAssembly(IAssemblySymbol asm)
        {
            System.Reflection.Assembly asmRef;
            if (TryMapAssembly(asm, out asmRef))
            {
                return asmRef;
            }

            var asmName = asm.Identity.Name;
            throw new InvalidOperationException("Can't load assembly: " + asmName);
        }

        public static Type GetAttributeType(ITypeSymbol symType)
        {
            var asm = symType.ContainingAssembly;

            var asmReflection = MapAssembly(asm);

            var fullname = symType.GetFullMetadataName();

            var typeRef = asmReflection.GetType(fullname);

            return typeRef;
        }

        public static Attribute MakeAttr(IJobHostMetadataProvider tooling, SemanticModel semantics, AttributeSyntax attrSyntax)
        {
            IMethodSymbol symAttributeCtor = (IMethodSymbol) semantics.GetSymbolInfo(attrSyntax).Symbol;
            var syntaxParams = symAttributeCtor.Parameters;

            var attrType = symAttributeCtor.ContainingType;
            var typeReflection = GetAttributeType(attrType);

            //List<object> positionArgs = new List<object>();
            //Dictionary<string, object> namedArgs = new Dictionary<string, object>();

            JObject args = new JObject();

            int idx = 0;
            foreach (var arg in attrSyntax.ArgumentList.Arguments)
            {
                var val = semantics.GetConstantValue(arg.Expression);
                if (!val.HasValue)
                {
                    return null;
                }
                var v2 = val.Value;

                string argName = null;
                if (arg.NameColon != null)
                {
                    argName = arg.NameColon.Name.ToString();
                }
                else if (arg.NameEquals != null)
                {
                    argName = arg.NameEquals.Name.ToString();
                } else
                {
                    argName = syntaxParams[idx].Name; // Positional 
                }

                /*
                if (argName == null)
                {
                    positionArgs.Add(v2);
                }
                else
                {
                    namedArgs[argName] = v2;
                }*/
                args[argName] = JToken.FromObject(v2);

                idx++;
            }

            var attr = tooling.GetAttribute(typeReflection, args);
            // var attr = Microsoft.Azure.WebJobs.Host.Bindings.AttributeCloner.CreateDirect(typeReflection, args);


            return attr;


        }

            /*
            // Instantiate an attribute from the syntax tree . 
            public static object MakeAttr(ISymbol symAttributeCtor)
            {
                var attrType = symAttributeCtor.ContainingType;
                var typeRef = GetAttributeType(attrType);

                int x = 5;


                // Look at 
                {
                    // IMethodSymbol method = ()
                    var method = (IMethodSymbol)symAttributeCtor;

                    foreach(var p in method.Parameters)
                    {

                    }
                }
                // foreach(var param in symAttributeCtor.Para)



                var attrType = sym.ContainingType;

                var attrNamespace = attrType.ContainingNamespace.ToString(); // "Microsoft.Azure.WebJobs"
                var attrName = attrType.Name; // "BlobAttribute"

                // Load the assembly. 
                string fullMetadataName = attrType.GetFullMetadataName();

                var asm = attrType.ContainingAssembly;

                var asmName = asm.Identity.Name;
                */

            

        // https://stackoverflow.com/questions/27105909/get-fully-qualified-metadata-name-in-roslyn
        public static string GetFullMetadataName(this INamespaceOrTypeSymbol symbol)
        {
            ISymbol s = symbol;
            var sb = new StringBuilder(s.MetadataName);

            var last = s;
            s = s.ContainingSymbol;
            while (!IsRootNamespace(s))
            {
                if (s is ITypeSymbol && last is ITypeSymbol)
                {
                    sb.Insert(0, '+');
                }
                else
                {
                    sb.Insert(0, '.');
                }
                sb.Insert(0, s.MetadataName);
                s = s.ContainingSymbol;
            }

            return sb.ToString();
        }

        // Try to convert a symbol in to a reflection type. 
        // Throw if can't convert. 
        internal static Type MakeFakeType(ISymbol p2)
        {
            if (p2.Kind == SymbolKind.ErrorType)
            {
                // The IDE already has at least one error here that the type is undefined. 
                // So no value in trying to find additional possible WebJobs errors. 
                throw new InvalidOperationException("Error symbol. Can't convert symbol type:" + p2.ToString());
            }
            if (p2.Kind == SymbolKind.ArrayType)
            {
                IArrayTypeSymbol arrayType = p2 as IArrayTypeSymbol;
                var inner = arrayType.ElementType;

                var innerRef = MakeFakeType(inner);

                return innerRef.MakeArrayType();
            }

            if (p2.Kind == SymbolKind.NamedType)
            {
                string name = p2.Name;
                string @namespace = GetFullMetadataName(p2.ContainingNamespace);
                                
                var type = p2 as INamedTypeSymbol;

                if (type.IsGenericType)
                {
                    var typeArgs = new Type[type.TypeArguments.Length];
                    for(int i = 0; i < type.TypeArguments.Length; i++)
                    {
                        typeArgs[i] = MakeFakeType(type.TypeArguments[i]);
                    }

                    // Get Type Definition
                    var asm = p2.ContainingAssembly;

                    Type definition;
                    Assembly asmRef;
                    if (TryMapAssembly(asm, out asmRef))
                    {
                        // Reflection type. Important for unficiation with the binders
                        var metadataName = type.GetFullMetadataName();
                        definition = asmRef.GetType(metadataName);
                    } else
                    {
                        // User generic type. 
                        definition = new FakeType(@namespace, p2.MetadataName, type);
                    }

                    return new ClassLibrary1.GenericFakeType(
                        definition, typeArgs);
                }

                Type result = new FakeType(@namespace, name, type);
         
                return result;
            }

            throw new InvalidOperationException("Can't convert symbol type:" + p2.ToString());
        }   

        private static bool IsRootNamespace(ISymbol s)
        {
            return s is INamespaceSymbol && ((INamespaceSymbol)s).IsGlobalNamespace;
        }


        public static bool IsOutParameter(this ParameterSyntax syntax)
        {
            foreach(var mod in syntax.Modifiers)
            {
                if (mod.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.OutKeyword))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
