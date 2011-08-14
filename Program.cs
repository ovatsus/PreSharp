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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Win32;
using System.Collections.Generic;

namespace PreSharp {

    internal class Program {

        public static int Main(string[] args) {

            try {
                
                if (args.Length == 0) {

                    string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    string preSharpDir = Path.Combine(programFiles, "PreSharp");
                    string preSharpTargetsFile = Path.Combine(preSharpDir, "PreSharp.targets");
                    string preSharpExecutable = Path.Combine(preSharpDir, "PreSharp.exe");
                    string msBuildExtensionsDir = Path.Combine(programFiles, @"MsBuild\v3.5");
                    string customAfterMicrosoftCommonTargetsFile = Path.Combine(msBuildExtensionsDir, "Custom.After.Microsoft.Common.targets");

                    Directory.CreateDirectory(preSharpDir);

                    File.Copy(Assembly.GetExecutingAssembly().Location, preSharpExecutable, true);

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

                    return 0;

                } else {

                    CommandLineLogger logger = new CommandLineLogger();

                    List<string> filesToCompile;
                    List<string> filesToCleanUp;
                    PreSharpGenerator.Process(logger, new List<string>(args), null, null, null, out filesToCompile, out filesToCleanUp, true, false, null);

                    if (filesToCleanUp != null) {
                        foreach (string file in filesToCleanUp) {
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
    }
}
