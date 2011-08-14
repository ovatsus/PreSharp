//Copyright (c) 2008 Gustavo Guerra

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in
//all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Win32;

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
                    /*templateLibraryFiles*/null,
                    /*dependencyPaths*/null,
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
        string msBuildExtensionsDir = Path.Combine(programFiles, @"MsBuild\v3.5");
        string customAfterMicrosoftCommonTargetsFile = Path.Combine(msBuildExtensionsDir, "Custom.After.Microsoft.Common.targets");

        Directory.CreateDirectory(preSharpDir);

        if (!Assembly.GetExecutingAssembly().Location.Equals(preSharpExecutable, StringComparison.OrdinalIgnoreCase)) {
            File.Copy(Assembly.GetExecutingAssembly().Location, preSharpExecutable, true);
        }

        string preSharpTargetsFileContents = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("PreSharp.PreSharp.targets")).ReadToEnd();
        File.WriteAllText(preSharpTargetsFile, preSharpTargetsFileContents);

        RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\9.0\MSBuild\SafeImports", true);
        if (key != null) {
            key.SetValue("PreSharp", preSharpTargetsFile);
        }

        key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\9.0\Languages\File Extensions\.cst", true);
        if (key == null) {
            key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\9.0\Languages\File Extensions", true).CreateSubKey(".cst");
            key.SetValue(null, "{694DD9B6-B865-4C5B-AD85-86356E9C88DC}");
        }

        Directory.CreateDirectory(msBuildExtensionsDir);

        XElement import = new XElement(XName.Get("Import", string.Empty),
                    new XAttribute("Project", @"$(ProgramFiles)\PreSharp\PreSharp.targets"),
                    new XAttribute("Condition", @" Exists('$(ProgramFiles)\PreSharp\PreSharp.targets') and Exists('$(ProgramFiles)\PreSharp\PreSharp.exe') and '$(DISABLE_PRESHARP)' == '' "));

        XElement project;
        if (!File.Exists(customAfterMicrosoftCommonTargetsFile)) {
            project = new XElement("Project", import);
        } else {
            project = XElement.Parse(File.ReadAllText(customAfterMicrosoftCommonTargetsFile).Replace("<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">", "<Project>"));
            if (!project.Elements("Import").Attributes("Project").Where(attr => attr.Value == @"$(ProgramFiles)\PreSharp\PreSharp.targets").Any()) {
                project.Add(import);
            }
        }
        File.WriteAllText(customAfterMicrosoftCommonTargetsFile, project.ToString().Replace("<Project>", "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"));

        Console.WriteLine("PreSharp " + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " was successfully installed");

    }

    public static void Process(Logger logger,
                               IEnumerable<string> inPlaceFiles,
                               IEnumerable<string> templateFiles,
                               IEnumerable<string> templaceLibraryFiles,
                               IEnumerable<string> dependencyPaths,
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
            generator.Init(debugMode, logger, dependencyPaths, conditionalCompilationSymbols);

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

                    newGenerator.Init(debugMode, logger, dependencyPaths, conditionalCompilationSymbols);
                    foreach (var reference in generator.References) {
                        newGenerator.AddReference(reference);
                    }
                    newGenerator.ProcessInplaceFiles(inPlaceFiles, compileGeneratedFiles);
                    newGenerator.ProcessTemplateFiles(templateFiles, compileGeneratedFiles, embeddedResourceGeneratedFiles);                                              

                } finally {

                    AppDomain.Unload(domain);
                }

            } else {

                generator.ProcessInplaceFiles(inPlaceFiles, compileGeneratedFiles);
                generator.ProcessTemplateFiles(templateFiles, compileGeneratedFiles, embeddedResourceGeneratedFiles);
            }

        } catch (Exception e) {

            logger.LogException(null, e);

        }
    }
}