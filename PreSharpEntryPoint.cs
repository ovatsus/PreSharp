using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Globalization;

internal sealed class PreSharpEntryPoint {

    private static readonly string ProgramFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

    private static int Main(string[] args) {

        try {
            bool isInstall = args.Length == 0;
            bool force64Install = false;
            if (args.Length == 1) {
                force64Install = args[0].Trim('/', '-') == "x64";
                isInstall |= force64Install;
            }

            if (isInstall)  {
                if (!AssemblyUtils.IsRunningAt32Bit()) {
                    Console.WriteLine();
                    LaunchInstallIn32Bits();
                }
                
                if (AssemblyUtils.IsRunningAt32Bit() || force64Install) {
                    
                    Console.WriteLine();
                    Install();
                }

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

    public static void Install() {
        
        Console.WriteLine("Installing PreSharp in " + (AssemblyUtils.IsRunningAt32Bit() ? "32" : "64") + "bits.");

        string preSharpDir = Path.Combine(ProgramFilesPath, "PreSharp");
        string preSharpTargetsFile = Path.Combine(preSharpDir, "PreSharp.targets");
        string preSharpExecutable = Path.Combine(preSharpDir, "PreSharp.exe");
        var versionsDic = new Dictionary<string, string[]> { { "v3.5", new String[] { "9.0" } }, { "v4.0", new String[] { "10.0", "11.0", "12.0", "14.0" } } };

        //Setup PreSharp
        Directory.CreateDirectory(preSharpDir);

        if (!AssemblyUtils.CopyTo(preSharpExecutable)) {
            Console.WriteLine("PreSharp " + AssemblyUtils.GetVersion().ToString() + " is already installed. Only updating system configuration.");
        }

        string preSharpTargetsFileContents = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("PreSharp.PreSharp.targets")).ReadToEnd();
        File.WriteAllText(preSharpTargetsFile, preSharpTargetsFileContents);

        //For each .net version
        foreach (var version in versionsDic) {
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

            CreateCustomTargets(version.Key);

            string installedVsString = string.Empty;
            foreach (var vsVersion in version.Value) {
                string visualStudioKeyPath = @"SOFTWARE\Microsoft\VisualStudio\" + vsVersion;
                //Setup VS
                using (var vsKey = Registry.LocalMachine.OpenSubKey(visualStudioKeyPath, false)) {
                    if (vsKey != null && vsKey.GetValue("InstallDir") != null) {

                        if (float.Parse(vsVersion, CultureInfo.InvariantCulture) >= 12) {
                            CreateCustomTargets("v" + vsVersion);
                        }

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

                        installedVsString += " (VS " + vsVersion + ")";
                    }
                }
            }

            Console.WriteLine("PreSharp " + AssemblyUtils.GetVersion().ToString() + " was successfully installed in .net " + version.Key + (installedVsString == string.Empty? " (no Visual Studio)" : installedVsString));
        }
    }

    private static void CreateCustomTargets(string version) {
        string msBuildExtensionsDir = Path.Combine(ProgramFilesPath, @"MsBuild\" + version);
        string customAfterMicrosoftCommonTargetsFile = Path.Combine(msBuildExtensionsDir, "Custom.After.Microsoft.Common.targets");

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
    }

    private static void LaunchInstallIn32Bits() {
        if (AssemblyUtils.IsRunningAt32Bit()) {
            return; // No need, already running at 32bits
        }

        if (File.Exists("PreSharp32.exe")) {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo("PreSharp32.exe");
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            
            try {
                if (!process.WaitForExit(15000)) {
                    Console.Error.WriteLine("Failed to install PreSharp in 32 bits. Timeout.");
                    process.Kill();
                    return;
                }
            } catch { }

            string output = (process.StandardOutput.ReadToEnd() ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(output)) {
                Console.WriteLine(output);
            }

            string errors = (process.StandardError.ReadToEnd() ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(errors)) {
                Console.WriteLine(errors);
            }

            if (string.IsNullOrEmpty(errors) && string.IsNullOrEmpty(output)) {
                Console.Error.WriteLine("Failed to install PreSharp in 32 bits. No output from process.");
            }

        } else {
            Console.Error.WriteLine("Failed to install PreSharp in 32 bits. PreSharp32.exe was not found.");
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