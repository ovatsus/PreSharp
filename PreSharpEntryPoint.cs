using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Win32;
using System.Text.RegularExpressions;

internal sealed class PreSharpEntryPoint {

    private static int Main(string[] args) {

        try {

            if (args.Length == 0) {

                Install();
                return 0;

            } else {

                CommandLineLogger logger = new CommandLineLogger();

                List<string> compileGeneratedFiles;
                List<string> embeddedResourceGeneratedFiles;
                List<string> filesToDelete;

                Process(
                    logger,
                    args,
                    /*templateFiles*/null,
                    /*templateIncludeFiles*/null,
                    /*templateLibraryFiles*/null,
                    /*dependencyPaths*/null,
                    /*absoluteOutputDir*/null,
                    out compileGeneratedFiles,
                    out embeddedResourceGeneratedFiles,
                    out filesToDelete,
                    /*createNewAppDomain*/true,
                    /*debugMode*/false,
                    /*conditionalCompilationSymbols*/null);

                if (filesToDelete != null) {
                    foreach (string file in filesToDelete) {
                        File.Delete(file);
                    }
                }

                return logger.Success ? 0 : 1;
            }

        } catch (Exception e) {

            Console.Error.WriteLine(e);
            return -1;
        }
    }

    private static void Install() {

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string preSharpDir = Path.Combine(programFiles, "PreSharp");
        string preSharpTargetsFile = Path.Combine(preSharpDir, "PreSharp.targets");
        string preSharpExecutable = Path.Combine(preSharpDir, "PreSharp.exe");
        var versionsDic = new Dictionary<string, string> { { "v3.5", "9.0" }, { "v4.0", "10.0" } };

        //Setup PreSharp
        Directory.CreateDirectory(preSharpDir);

        if (!AssemblyUtils.CopyTo(preSharpExecutable)) {
            Console.WriteLine("PreSharp " + AssemblyUtils.GetVersion().ToString() + " is already installed. Only updating system configuration.");
        }

        string preSharpTargetsFileContents = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("PreSharp.PreSharp.targets")).ReadToEnd();
        File.WriteAllText(preSharpTargetsFile, preSharpTargetsFileContents);

        //For each .net version
        foreach (var version in versionsDic) {
            string msBuildExtensionsDir = Path.Combine(programFiles, @"MsBuild\" + version.Key);
            string customAfterMicrosoftCommonTargetsFile = Path.Combine(msBuildExtensionsDir, "Custom.After.Microsoft.Common.targets");
            string visualStudioKeyPath = @"SOFTWARE\Microsoft\VisualStudio\" + version.Value;

            //They dont seem to decide where to keep the stuff...
            string[] dotNetKeyPaths = new string[] { @"SOFTWARE\Microsoft\NET Framework Setup\NDP\" + version.Key,
                                                     @"SOFTWARE\Microsoft\NET Framework Setup\NDP\" + version.Key.Substring(0, Math.Max(version.Key.LastIndexOf('.'), 0)) + @"\Full\" };

            bool foundDotNet = false;
            foreach (string dotNetKeyPath in dotNetKeyPaths) {
                using (var dotNetKey = Registry.LocalMachine.OpenSubKey(dotNetKeyPath, false)) {
                    if (dotNetKey != null && ((1).Equals(dotNetKey.GetValue("Install")) || (1).Equals(dotNetKey.GetValue("Full")))) {
                        foundDotNet = true;
                        break;
                    }
                }
            }
            if (!foundDotNet) {
                continue;
            }

            //Setup MSBUILD
            Directory.CreateDirectory(msBuildExtensionsDir);

            XElement import = new XElement(XName.Get("Import", string.Empty),
                        new XAttribute("Project", @"$(ProgramFiles)\PreSharp\PreSharp.targets"),
                        new XAttribute("Condition", @" Exists('$(ProgramFiles)\PreSharp\PreSharp.targets') and Exists('$(ProgramFiles)\PreSharp\PreSharp.exe') and '$(DISABLE_PRESHARP)' == '' "));

            XElement project;
            if (!File.Exists(customAfterMicrosoftCommonTargetsFile)) {
                project = new XElement("Project", import);
            } else {
                project = XElement.Parse(Regex.Replace(File.ReadAllText(customAfterMicrosoftCommonTargetsFile), @"<Project\s*xmlns\s*=\s*""http://[^""]*""\s*>", "<Project>"));
                if (!project.Elements("Import").Attributes("Project").Where(attr => attr.Value == @"$(ProgramFiles)\PreSharp\PreSharp.targets").Any()) {
                    project.Add(import);
                }
            }
            File.WriteAllText(customAfterMicrosoftCommonTargetsFile, project.ToString().Replace("<Project>", "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"));

            //Setup VS
            bool installedInVs = false;
            using (var vsKey = Registry.LocalMachine.OpenSubKey(visualStudioKeyPath, false)) {
                if (vsKey != null && vsKey.GetValue("InstallDir") != null) {

                    using (var key = Registry.LocalMachine.OpenSubKey(visualStudioKeyPath + @"\MSBuild\SafeImports", true)) {
                        if (key != null) {
                            key.SetValue("PreSharp", preSharpTargetsFile);
                        }
                    }

                    using (var key = Registry.LocalMachine.OpenSubKey(visualStudioKeyPath + @"\Languages\File Extensions\.cst", true)) {
                        if (key == null) {
                            using (var newKey = Registry.LocalMachine.OpenSubKey(visualStudioKeyPath + @"\Languages\File Extensions", true).CreateSubKey(".cst")) {
                                newKey.SetValue(null, "{694DD9B6-B865-4C5B-AD85-86356E9C88DC}");
                            }
                        }
                    }

                    installedInVs = true;
                }
            }

            Console.WriteLine("PreSharp " + AssemblyUtils.GetVersion().ToString() + " was successfully installed in .net " + version.Key + (installedInVs ? "" : " (no Visual Studio)"));
        }
    }

    public static void Process(Logger logger,
                               IEnumerable<string> inPlaceFiles,
                               IEnumerable<string> templateFiles,
                               IEnumerable<string> templateIncludeFiles,
                               IEnumerable<string> templaceLibraryFiles,
                               IEnumerable<string> dependencyPaths,
                               string absoluteOutputDir,
                               out List<string> compileGeneratedFiles,
                               out List<string> embeddedResourceGeneratedFiles,
                               out List<string> filesToDelete,
                               bool createNewAppDomain,
                               bool debugMode,
                               string conditionalCompilationSymbols) {


        compileGeneratedFiles = new List<string>();
        embeddedResourceGeneratedFiles = new List<string>();
        filesToDelete = new List<string>();

        try {

            PreSharpGenerator generator = new PreSharpGenerator();
            generator.Init(debugMode, logger, dependencyPaths, conditionalCompilationSymbols, absoluteOutputDir);

            if (templaceLibraryFiles != null && templaceLibraryFiles.Any()) {
                string libraryAssembly = Path.GetTempFileName();
                File.Delete(libraryAssembly);
                libraryAssembly = Path.ChangeExtension(libraryAssembly, ".dll");

                if (generator.GenerateLibraryAssemblyFromTemplateLibraryFiles(templaceLibraryFiles, libraryAssembly, compileGeneratedFiles)) {
                    filesToDelete.Add(libraryAssembly);
                    generator.AddReference(libraryAssembly);
                }
            }

            if (createNewAppDomain) {

                AppDomain domain = AppDomain.CreateDomain("PreSharp Temporary Domain");

                try {

                    PreSharpGenerator newGenerator = (PreSharpGenerator)domain.CreateInstanceFromAndUnwrap(
                        Assembly.GetExecutingAssembly().Location,
                        typeof(PreSharpGenerator).FullName);

                    newGenerator.Init(debugMode, logger, dependencyPaths, conditionalCompilationSymbols, absoluteOutputDir);
                    foreach (var reference in generator.References) {
                        newGenerator.AddReference(reference);
                    }
                    newGenerator.ProcessInplaceFiles(inPlaceFiles, compileGeneratedFiles);
                    newGenerator.ProcessTemplateFiles(templateFiles, templateIncludeFiles, compileGeneratedFiles, embeddedResourceGeneratedFiles);

                } finally {

                    AppDomain.Unload(domain);
                }

            } else {

                generator.ProcessInplaceFiles(inPlaceFiles, compileGeneratedFiles);
                generator.ProcessTemplateFiles(templateFiles, templateIncludeFiles, compileGeneratedFiles, embeddedResourceGeneratedFiles);
            }

        } catch (Exception e) {

            logger.LogException(null, e);

        }
    }
}