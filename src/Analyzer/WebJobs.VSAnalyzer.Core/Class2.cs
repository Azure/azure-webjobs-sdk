using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using System.Reflection;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Config;
using System.IO;
using Microsoft.Azure.WebJobs.Host;

namespace ClassLibrary1
{
    class WebJobsAnalyzer
    {
        private string webJobsPath;

        public WebJobsAnalyzer(string webJobsPath)
        {
            // load the webjobs assembly or whatever you need to do...
            this.webJobsPath = webJobsPath;
        }

        internal void Analyze(SyntaxNodeAnalysisContext sytaxNodeAnalysisContext)
        {
            // analyzes the call site for errors...
            //throw new NotImplementedException();
        }        
    }

    // Only support extensions and WebJobs core.
    // Although extensions may refer to other dlls. 
    public class AssemblyCache
    {
        // Map from assembly identities to full paths 
        public static AssemblyCache Instance = new AssemblyCache();

        bool _registered;

        Dictionary<string, string> _map = new Dictionary<string, string>();

        Dictionary<string, Assembly> _mapRef = new Dictionary<string, Assembly>();

        const string WebJobsAssemblyName = "Microsoft.Azure.WebJobs";
        const string WebJobsHostAssemblyName = "Microsoft.Azure.WebJobs.Host";

        IJobHostMetadataProvider _tooling;

        public IJobHostMetadataProvider Tooling => _tooling;

        internal void Build(Compilation compilation)
        {
            Register();

            lock (this)
            {
                if (_tooling != null)
                {
                    return; // already initialized. 
                }

                foreach (var asm in compilation.References.OfType<PortableExecutableReference>())
                {
                    var dispName = asm.Display;
                    var path = asm.FilePath;

                    _map[dispName] = path;

                }

                // Builtins
                _mapRef["mscorlib"] = typeof(object).Assembly;
                _mapRef[WebJobsAssemblyName] = typeof(Microsoft.Azure.WebJobs.BlobAttribute).Assembly;
                _mapRef[WebJobsHostAssemblyName] = typeof(Microsoft.Azure.WebJobs.JobHost).Assembly;
            }

            // Produce tooling object                 
            var hostConfig = Initialize();
            var jh2 = new JobHost(hostConfig);
            this._tooling = (IJobHostMetadataProvider) jh2.Services.GetService(typeof(IJobHostMetadataProvider));
        }


        private JobHostConfiguration Initialize()
        {
            JobHostConfiguration hostConfig = new Microsoft.Azure.WebJobs.JobHostConfiguration();

            foreach (var path in _map.Values)
            {
                // We don't want to load and reflect over every dll.
                // By convention, restrict to based on filenames.
                var filename = Path.GetFileName(path);
                if (!filename.ToLowerInvariant().Contains("extension"))
                {
                    continue;
                }

                // See GetNugetPackagesPath() for details
                // Script runtime is already setup with assembly resolution hooks, so use LoadFrom
                Assembly assembly = Assembly.LoadFrom(path);
                string asmName = new AssemblyName(assembly.FullName).Name;
                _mapRef[asmName] = assembly;
                LoadExtensions(hostConfig, assembly, path);
            }

            return hostConfig;
        }

        private void LoadExtensions(JobHostConfiguration hostConfig, Assembly assembly, string locationHint)
        {
            foreach (var type in assembly.ExportedTypes)
            {
                if (!typeof(IExtensionConfigProvider).IsAssignableFrom(type))
                {
                    continue;
                }

                try
                {
                    IExtensionConfigProvider instance = (IExtensionConfigProvider)Activator.CreateInstance(type);                    
                    hostConfig.AddExtension(instance);
                }
                catch (Exception e)
                {
                    // this.TraceWriter.Error($"Failed to load custom extension {type} from '{locationHint}'", e);
                }
            }
        }
 

        public bool TryMapAssembly(IAssemblySymbol asm, out System.Reflection.Assembly asmRef)
        {
            // Top-level map only supports mscorlib, webjobs, or extensions 
            var asmName = asm.Identity.Name;
            

            Assembly asm2;
            if (_mapRef.TryGetValue(asmName, out asm2))
            {
                asmRef = asm2;
                return true;
            }

            // Is this an extension? Must have a reference to WebJobs
            bool isWebJobsAssembly = false;
            foreach(var module in asm.Modules)
            {
                foreach(var asmReference in module.ReferencedAssemblies)
                {
                    if (asmReference.Name == WebJobsAssemblyName)
                    {
                        isWebJobsAssembly = true;
                        goto Done;
                    }

                }
            }
            Done: 
            if (!isWebJobsAssembly)
            {
                asmRef = null;
                return false;
            }

            foreach (var kv in _map)
            {
                var path = kv.Value;
                var shortName = System.IO.Path.GetFileNameWithoutExtension(path);

                if (string.Equals(asmName, shortName, StringComparison.OrdinalIgnoreCase))
                {
                    var asm3 = Assembly.LoadFile(path);
                    _mapRef[asmName] = asm3;
                    
                    asmRef = asm3;
                    return true;
                }
            }

            throw new NotImplementedException();
        }

        public void Register()
        {
            if (_registered)
            {
                return;
            }
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            _registered = true;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var context = args.RequestingAssembly;

            if (!_mapRef.ContainsValue(context))
            {
                // Outside assembly? just ignore 
                return null;
            }

            // Assume it's either in the FX, or lives next to this assembly. 
            var an = new AssemblyName(args.Name);

            Assembly asm2;
            if (_mapRef.TryGetValue(an.Name, out asm2))
            {
                return asm2;
            }

            return null;
        }
    }



}
