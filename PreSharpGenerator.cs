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

// Parts of this file were based on Eric Parker's code available at 
// http://web.archive.org/web/20060726075638/http://www.inmetrix.com/

/*	Copyright (C) 2003 Inmetrix Corp. (www.inmetrix.com);
 *	All rights reserved.
 * 
 *	LICENSE
 *	You may use or modify the code in this file for your own projects, either
 *  personal or commerical, with the following restrictions:
 *  
 *		1) You may not directly sell this code as part of a code generation library or product.
 *		2) You must include the above copyright notice in any derived products.
 *		3) NO WARRANTY.  This code is provided on an "AS IS" basis.  
 *      4) Inmetrix Corp. shall not be liable to licensee or to any other party in any way as a result of using this code.
 */

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CSharp;

namespace PreSharp  {

    internal class PreSharpGenerator : MarshalByRefObject {

        public static List<string> Process(Logger logger, 
                                           List<string> inPlaceFiles,
                                           List<string> templateFiles, 
                                           List<string> templaceLibraryFiles,
                                           List<string> dependencyPaths,
                                           out List<string> filesToCompile,                               
                                           out List<string> filesToCleanup,
                                           bool createNewAppDomain,
                                           bool debugMode,
                                           string defineConstants) {

            List<string> generatedLibraryAssemblies = new List<string>();
            List<string> references = new List<string>();
            filesToCompile = new List<string>();

            try {

                PreSharpGenerator generator = new PreSharpGenerator();
                generator.SetLogger(logger);
                if (debugMode) {
                    generator.SetDebugMode();
                }

                if (templaceLibraryFiles != null && templaceLibraryFiles.Any()) {
                    string libraryAssembly = Path.GetTempFileName();
                    File.Delete(libraryAssembly);
                    libraryAssembly = Path.ChangeExtension(libraryAssembly, ".dll");
                                        
                    if (generator.generateLibraryAssemblyFromTemplateLibraryFiles(templaceLibraryFiles, libraryAssembly, references, dependencyPaths, defineConstants, filesToCompile)) {
                        generatedLibraryAssemblies.Add(libraryAssembly);
                        references.Add(libraryAssembly);
                    }
                    foreach (string file in templaceLibraryFiles) {
                        logger.LogMessage("Processed template library file '" + file + "'.");
                    }
                }
                
                if (createNewAppDomain) {

                    AppDomain domain = AppDomain.CreateDomain("PreSharp Temporary Domain");

                    try {

                        PreSharpGenerator newGenerator = (PreSharpGenerator)domain.CreateInstanceFromAndUnwrap(
                            Assembly.GetExecutingAssembly().Location,
                            typeof(PreSharpGenerator).FullName);

                        newGenerator.SetLogger(logger);
                        if (debugMode) {
                            newGenerator.SetDebugMode();
                        }

                        newGenerator.processFiles(inPlaceFiles, templateFiles, references, dependencyPaths, defineConstants, filesToCompile);

                    } finally {
                     
                        AppDomain.Unload(domain);
                    }

                } else {

                    generator.processFiles(inPlaceFiles, templateFiles, references, dependencyPaths, defineConstants, filesToCompile);
                }

            } catch (Exception e) {

                logger.LogException(null, e);

            } finally {

                filesToCleanup = generatedLibraryAssemblies;
            }

            foreach (string file in filesToCompile) {
                logger.LogMessage("File '" + file + "' was added to compile list.");
            }

            return filesToCompile;
        }

        public PreSharpGenerator() { }

        public void SetLogger(Logger logger) {
            this.logger = logger;
        }

        public void SetDebugMode() {
            this.debugMode = true;
        }

        private Logger logger;
        private bool debugMode;

        private void processFiles(List<string> inPlaceFiles, 
                                  List<string> templateFiles, 
                                  List<string> references,
                                  List<string> dependencyPaths,
                                  string defineConstants,
                                  List<string> filesToCompile) {

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                int commaPos = args.Name.IndexOf(",");
                string assemblyName = commaPos == -1 ? args.Name : args.Name.Substring(0, commaPos);
                string path = references.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == assemblyName);
                if (path != null) {
                    return Assembly.LoadFrom(path);
                } else {
                    return null;
                }
            };

            if (inPlaceFiles != null) {
                foreach (string file in inPlaceFiles) {

                    string[] lines;
                    try {
                        lines = File.ReadAllLines(file);
                    } catch {
                        logger.LogError(file, null, "Error reading file '" + file + "'", 0, 0);
                        continue;
                    }

                    filesToCompile.Add(file);
                    string output = processTemplateRegions(lines, file, references, dependencyPaths, defineConstants);

                    logger.LogMessage("Processed in-place file '" + file + "'.");
                    
                    if (string.Join(Environment.NewLine, lines) + Environment.NewLine != output) {
                        File.WriteAllText(file, output);                        
                    }
                } 
            }

            if (templateFiles != null) {
                foreach (string file in templateFiles) {

                    string templateFileCode;
                    try {
                        templateFileCode = File.ReadAllText(file);
                    } catch {
                        logger.LogError(file, null, "Error reading file '" + file + "'", 0, 0);
                        continue;
                    }

                    generateTemplateFileOutput(templateFileCode, references, dependencyPaths, file, defineConstants, filesToCompile);

                    logger.LogMessage("Processed template file '" + file + "'.");
                }
            }
        }
       
        private static readonly string PRESHARP_TEMPLATE_MARKER = "#if PRESHARP_TEMPLATE";
        private static readonly string PRESHARP_TEMPLATE_LIBRARY_MARKER = "#if PRESHARP_TEMPLATE_LIBRARY";

        private string processTemplateRegions(string[] lines, 
                                              string file, 
                                              List<string> references,
                                              List<string> dependencyPaths,
                                              string defineConstants) {

            StringBuilder outputBuilder = new StringBuilder();
            IEnumerator<string> lineIterator = lines.Cast<string>().GetEnumerator();

            int lineNumberBeforeScript = -1;
            int ifNesting = 0;

            while (lineIterator.MoveNext()) {
                string currentLine = lineIterator.Current;
                string trimmedLine = currentLine.Trim();
                lineNumberBeforeScript++;

                outputBuilder.AppendLine(currentLine);

                bool regionIsTemplate = trimmedLine == PRESHARP_TEMPLATE_MARKER;
                bool regionIsTemplateLibrary = trimmedLine == PRESHARP_TEMPLATE_LIBRARY_MARKER || trimmedLine.StartsWith(PRESHARP_TEMPLATE_LIBRARY_MARKER + "_");
                string writerName = "writer";
                if (regionIsTemplateLibrary) {
                    if (trimmedLine.Length > PRESHARP_TEMPLATE_LIBRARY_MARKER.Length) {
                        writerName = trimmedLine.Substring(PRESHARP_TEMPLATE_LIBRARY_MARKER.Length + 1);
                    }
                }

                if (regionIsTemplate || regionIsTemplateLibrary) {
                    int linesOfThisScript = 0;
                    bool afterElse = false;

                    ifNesting++;

                    StringBuilder region = new StringBuilder();
                    while (ifNesting > 0 && lineIterator.MoveNext()) {
                        currentLine = lineIterator.Current;
                        trimmedLine = currentLine.TrimStart();
                        linesOfThisScript++;

                        if (trimmedLine.StartsWith("#if")) {
                            ifNesting++;
                        } else if (trimmedLine.StartsWith("#endif")) {
                            ifNesting--;
                        } else if (trimmedLine.StartsWith("#else") && ifNesting == 1) {
                            afterElse = true;
                        }

                        if (ifNesting > 0 && !afterElse) {
                            region.AppendLine(currentLine);
                            outputBuilder.AppendLine(currentLine);
                        }
                    }

                    if (ifNesting == 0) {
                        string generatedCode;
                        if (regionIsTemplateLibrary) {
                            string templateCode = TrimLastNewLine(region.ToString());
                            generatedCode = generateTemplateLibraryCode(new StringReader(templateCode), writerName);
                        } else {
                            generatedCode = generateTemplateRegionOutput(region.ToString(), references, dependencyPaths, file, lineNumberBeforeScript, defineConstants);
                        }

                        if (generatedCode != null) {
                            outputBuilder.AppendLine("#else");
                            outputBuilder.AppendLine("#region PreSharp Generated");
                            if (regionIsTemplateLibrary) {
                                outputBuilder.AppendLine(generatedCode);
                            } else {
                                outputBuilder.Append(generatedCode);
                            }
                            outputBuilder.AppendLine("#endregion");
                        }
                        outputBuilder.AppendLine("#endif");

                        lineNumberBeforeScript += linesOfThisScript;
                    }
                }
            }

            return outputBuilder.ToString();
        }

        public delegate void FileAndLineMatcher(string originalFile, int originalLine, out string file, out int line);

        public static void DefaultFileAndLineMatcher(string originalFile, int originalLine, out string file, out int line) {
            file = originalFile;
            line = originalLine;
        }

        private static readonly CSharpCodeProvider codeProvider =
            new CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v3.5" } });

        private Assembly compileTemplateLibraryCode(List<string> references,
                                                    string originalFile,
                                                    int lineNumberDelta,
                                                    string templateLibraryCode,
                                                    string defineConstants,
                                                    FileAndLineMatcher fileAndLineMatcher) {

            CompilerParameters compilerOptions = new CompilerParameters();
            compilerOptions.GenerateInMemory = true;
            compilerOptions.GenerateExecutable = false;
            compilerOptions.IncludeDebugInformation = true;
            compilerOptions.CompilerOptions = "/define:" + defineConstants;

            foreach (string reference in references) {                    
                compilerOptions.ReferencedAssemblies.Add(reference);
            }

            CompilerResults results = codeProvider.CompileAssemblyFromSource(compilerOptions, templateLibraryCode);

            foreach (CompilerError error in results.Errors) {
                string file;
                int line;
                fileAndLineMatcher(originalFile, error.Line, out file, out line);
                if (error.IsWarning) {
                    logger.LogWarning(file, error.ErrorNumber, error.ErrorText, line + lineNumberDelta + 1, error.Column);
                } else {
                    logger.LogError(file, error.ErrorNumber, error.ErrorText, line + lineNumberDelta + 1, error.Column);
                }
            }

            if (results.Errors.Count == 0) {
                return results.CompiledAssembly;
            } else {
                return null;
            }
        }

        private bool generateLibraryAssemblyFromTemplateLibraryFiles(List<string> templateLibraryFiles, 
                                                                     string outputAssemblyPath,
                                                                     List<string> references,
                                                                     List<string> dependencyPaths,
                                                                     string defineConstants,
                                                                     List<string> filesToCompile) {

            processFiles(templateLibraryFiles, null, references, dependencyPaths, defineConstants, new List<string>());

            string[] processedFiles = new string[templateLibraryFiles.Count];
            for (int i = 0; i < processedFiles.Length; ++i) {
                string file = templateLibraryFiles[i];
                string processedFileContents = File.ReadAllText(file);
                string processedFile;
                if (processedFileContents.Contains("PRESHARP_FILE_IS_TEMPLATE_LIBRARY")) {
                    processedFileContents = processedFileContents.Replace("PRESHARP_FILE_IS_TEMPLATE_LIBRARY", "true");
                    processedFile = file + ".PreSharpDebug.cs";
                    logger.LogMessage("Generated debug file '" + processedFile + "'.");
                    File.WriteAllText(processedFile, processedFileContents);
                } else {
                    processedFile = file;
                }
                processedFiles[i] = processedFile;
                if (debugMode) {
                    filesToCompile.Add(processedFile);
                }
            }

            CompilerParameters compilerOptions = new CompilerParameters();
            compilerOptions.GenerateInMemory = false;
            compilerOptions.GenerateExecutable = false;
            compilerOptions.OutputAssembly = outputAssemblyPath;
            compilerOptions.IncludeDebugInformation = true;
            compilerOptions.CompilerOptions = "/define:" + defineConstants;

            foreach (string reference in references) {
                compilerOptions.ReferencedAssemblies.Add(reference);
            }

            CompilerResults results = codeProvider.CompileAssemblyFromFile(compilerOptions, processedFiles);

            foreach (CompilerError error in results.Errors) {
                if (error.IsWarning) {
                    logger.LogWarning(error.FileName, error.ErrorNumber, error.ErrorText, error.Line, error.Column);
                } else {
                    logger.LogError(error.FileName, error.ErrorNumber, error.ErrorText, error.Line, error.Column);
                }
            }

            return results.Errors.Count == 0;
        }
        
        private string generateTemplateRegionOutput(string templateRegionCode,
                                                    List<string> references,
                                                    List<string> dependencyPaths,
                                                    string file,
                                                    int lineNumberDelta,
                                                    string defineConstants) {
            string prefix =
                "\r\n" +
                "using System;\r\n" +
                "using System.IO;\r\n" +
                "\r\n" +
                "namespace PreSharp {\r\n" +
                "\r\n" +
                "    public static class Generator {\r\n" +
                "\r\n" +
                "        public static StringWriter Generate() {\r\n" +
                "\r\n" +
                "            StringWriter writer = new StringWriter();\r\n";

            string suffix = "\r\n" +
               "            return writer;\r\n" +
               "        }\r\n" +
               "    }\r\n" +
               "}\r\n";

            string templateLibraryCode;
            Assembly generatedAssembly = generateTemplateLibraryAssembly(templateRegionCode,
                                                                         prefix,
                                                                         suffix,
                                                                         references,
                                                                         dependencyPaths,
                                                                         file,
                                                                         DefaultFileAndLineMatcher,
                                                                         lineNumberDelta - 11,
                                                                         defineConstants,
                                                                         "writer",
                                                                         out templateLibraryCode);

            if (generatedAssembly == null) {
                return null;
            }

            try {                
                StringWriter writer = (StringWriter) generatedAssembly.GetType("PreSharp.Generator").GetMethod("Generate", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
                return writer.ToString();
            } catch (Exception exception) {
                logger.LogException(file, exception);
                return null;
            }
        }

        private void generateTemplateFileOutput(string templateFileCode,
                                                List<string> references,
                                                List<string> dependencyPaths,
                                                string templateFile,
                                                string defineConstants,
                                                List<string> filesToCompile) {            
            int lineNumberDelta = -1;
            string entryPoint = null;

            Regex entryPointRegex = new Regex("(\\s*)<%@\\s*EntryPoint\\s+Statement=\"([^\"]+)\"\\s*%>(\\s*)");
            MatchCollection matches = entryPointRegex.Matches(templateFileCode);
            
            if (matches.Count == 1) {
                entryPoint = matches[0].Groups[2].Value;
                lineNumberDelta += matches[0].Groups[1].Value.ToCharArray().Count(c => c == '\n');
                lineNumberDelta += matches[0].Groups[3].Value.ToCharArray().Count(c => c == '\n');
                templateFileCode = entryPointRegex.Replace(templateFileCode, string.Empty);
            } else if (matches.Count == 0) {
                logger.LogError(templateFile, null, "The entry point must be specified by using <%@ EntryPoint Statement=\"...\" %>", 0, 0);
                return;
            } else {
                logger.LogError(templateFile, null, "Only one entry point can be defined per file.", 0, 0);
                return;
            }

            List<int> fileStarts = new List<int>();
            List<int> fileEnds = new List<int>();
            List<string> fileNames = new List<string>();
            fileStarts.Add(0);
            fileEnds.Add(templateFileCode.ToCharArray().Count(c => c == '\n'));
            fileNames.Add(templateFile);

            Regex includesRegex = new Regex("(\\s*)<%@\\s*Include\\s+Path=\"([^\"]+)\"\\s*%>(\\s*)");
            var includeMatches = includesRegex.Matches(templateFileCode);
            templateFileCode = includesRegex.Replace(templateFileCode, string.Empty);
            foreach (Match m in includeMatches) {
                string path = m.Groups[2].Value;
                lineNumberDelta += m.Groups[1].Value.ToCharArray().Count(c => c == '\n');
                lineNumberDelta += m.Groups[3].Value.ToCharArray().Count(c => c == '\n');
                string includedFile = File.ReadAllText(path);
                templateFileCode += includedFile;
                int previous = fileEnds.Last() + 1;
                fileStarts.Add(previous);
                fileEnds.Add(previous + includedFile.ToCharArray().Count(c => c == '\n'));
                fileNames.Add(path);
            }

            FileAndLineMatcher fileAndLineMatcher = delegate(string originalFile, int originalLine, out string file, out int line) {
                file = originalFile;
                line = originalLine;
                for (int i = 0; i < fileEnds.Count; ++i) {
                    if (originalLine >= fileStarts[i] && originalLine <= fileEnds[i]) {
                        file = fileNames[i];
                        line = originalLine - fileStarts[i];
                    }
                }
            };

            string suffix = "\r\n" +
                "\r\n" +
                "internal static class PreSharp {\r\n" +
                "\r\n" +
                "    private static System.IO.TextWriter writer = new System.IO.StringWriter();\r\n" +
                "    public static System.IO.TextWriter Writer { get { return writer; } }\r\n" +
                "\r\n" +
                "    private static System.Collections.Generic.List<string> generatedFiles = new System.Collections.Generic.List<string>();\r\n" +
                "\r\n" +
                "    private static string currentFile;\r\n" + 
                "\r\n" +
                "    public static void SetOutput(string outputFile) {\r\n" +
                "        writer.Close();\r\n" +
                "        if (currentFile != null) {\r\n" +
                "            string output = writer.ToString();\r\n" +
                "            if (!System.IO.File.Exists(currentFile) || output != System.IO.File.ReadAllText(currentFile)) {\r\n" +
                "                System.IO.File.WriteAllText(currentFile, output);\r\n" +
                "            }\r\n" + 
                "        }\r\n" +
                "        currentFile = outputFile;\r\n" +
                "        writer = new System.IO.StringWriter();\r\n" +
                "        generatedFiles.Add(outputFile);\r\n" +
                "    }\r\n" +
                "\r\n" +
                "    public static System.Collections.Generic.List<string> DumpOutput() {\r\n" +
                "        " + entryPoint + "\r\n" +
                "        writer.Close();\r\n" +
                "        if (currentFile != null) {\r\n" +
                "            string output = writer.ToString();\r\n" +
                "            if (!System.IO.File.Exists(currentFile) || output != System.IO.File.ReadAllText(currentFile)) {\r\n" +
                "                System.IO.File.WriteAllText(currentFile, output);\r\n" +
                "            }\r\n" + 
                "        }\r\n" +
                "        return generatedFiles;\r\n" + 
                "    }\r\n" +
                "}\r\n";

            templateFileCode = templateFileCode.Replace("#if PRESHARP_TEMPLATE\r\n", "\r\n%>");
            templateFileCode = templateFileCode.Replace("#if PRESHARP_TEMPLATE\n", "\n%>");
            templateFileCode = templateFileCode.Replace("\r\n#endif", "<%\r\n");
            templateFileCode = templateFileCode.Replace("\n#endif", "<%\n");
            templateFileCode = templateFileCode.Replace("#endif", "<%");

            try {
                string templateLibrayCode;
                Assembly generatedAssembly = generateTemplateLibraryAssembly("<%" + templateFileCode + "%>",
                                                                             string.Empty,
                                                                             suffix,
                                                                             references,
                                                                             dependencyPaths,
                                                                             templateFile,
                                                                             fileAndLineMatcher,
                                                                             lineNumberDelta,
                                                                             defineConstants,
                                                                             "PreSharp.Writer",
                                                                             out templateLibrayCode);

                File.WriteAllText(templateFile + ".PreSharpDebug.cs", templateLibrayCode);
                logger.LogMessage("Generated debug file '" + templateFile + ".PreSharpDebug.cs'.");
                if (debugMode) {
                    filesToCompile.Add(templateFile);
                }

                if (generatedAssembly == null) {
                    return;
                }

                List<string> generatedFiles = (List<string>) generatedAssembly.GetType("PreSharp").GetMethod("DumpOutput", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
                if (generatedFiles.Count == 0) {
                    logger.LogError(templateFile, null, "At least one template output must be specified by using PreSharp.SetOutput(string outputFile)", 0, 0);
                } else {
                    foreach (string generatedFile in generatedFiles) {
                        logger.LogMessage("Generated file '" + generatedFile + "' from template '" + templateFile + "'.");
                    }
                }
            } catch (TargetInvocationException exception) {
                logger.LogException(templateFile, exception.InnerException);
            } catch (Exception exception) {
                logger.LogException(templateFile, exception);
            }
        }

        private Assembly generateTemplateLibraryAssembly(string templateCode,
                                                         string prefixCode,
                                                         string suffixCode,
                                                         List<string> references,
                                                         List<string> dependencyPaths,
                                                         string file,
                                                         FileAndLineMatcher fileAndLineMatcher,
                                                         int lineNumberDelta,
                                                         string defineConstants,
                                                         string writer,
                                                         out string templateLibraryCode) {

            Regex assembliesRegex = new Regex("(\\s*)<%@\\s*Assembly\\s+Name=\"([^\"]+)\"\\s*%>(\\s*)");
            foreach (Match m in assembliesRegex.Matches(templateCode)) {
                string assemblyName = m.Groups[2].Value;

                Assembly assembly = null;
                try {
#pragma warning disable 0618
                    assembly = Assembly.LoadWithPartialName(assemblyName);
#pragma warning restore 0618
                } catch {
                }
                if (dependencyPaths != null) {
                    if (assembly == null) {
                        string assemblyPath = dependencyPaths.SingleOrDefault(path => path.EndsWith(assemblyName + ".dll"));
                        if (assemblyPath != null) {
                            try {
                                assembly = Assembly.LoadFrom(assemblyPath);
                            } catch { }
                        }
                    }
                    if (assembly == null) {
                        string assemblyPath = dependencyPaths.SingleOrDefault(path => path.EndsWith(assemblyName + ".exe"));
                        if (assemblyPath != null) {
                            try {
                                assembly = Assembly.LoadFrom(assemblyPath);
                            } catch { }
                        }
                    }
                }
                if (assembly == null) {
                    logger.LogWarning(file, null, "Could not find assembly '" + assemblyName + "'.", 0, 0);
                } else {
                    references.Add(assembly.Location);
                }
                lineNumberDelta += m.Groups[1].Value.ToCharArray().Count(c => c == '\n');
                lineNumberDelta += m.Groups[3].Value.ToCharArray().Count(c => c == '\n');
            }
            templateCode = assembliesRegex.Replace(templateCode, string.Empty);

            Regex importsRegex = new Regex("(\\s*)<%@\\s*Import\\s+Namespace=\"([^\"]+)\"\\s*%>(\\s*)");
            foreach (Match m in importsRegex.Matches(templateCode)) {
                string @namespace = m.Groups[2].Value;
                prefixCode = "using " + @namespace + ";\r\n" + prefixCode;
                lineNumberDelta -= 1;
                lineNumberDelta += m.Groups[1].Value.ToCharArray().Count(c => c == '\n');
                lineNumberDelta += m.Groups[3].Value.ToCharArray().Count(c => c == '\n');
            }
            templateCode = importsRegex.Replace(templateCode, string.Empty);

            Regex codeTemplateRegex = new Regex("(\\s*)<%@\\s*CodeTemplate\\s*Language=\"C#\"\\s+TargetLanguage=\"(?:[^\"]+)\"\\s*%>(\\s*)");
            foreach (Match m in importsRegex.Matches(templateCode)) {
                lineNumberDelta += m.Groups[1].Value.ToCharArray().Count(c => c == '\n');
                lineNumberDelta += m.Groups[2].Value.ToCharArray().Count(c => c == '\n');
            }
            templateCode = codeTemplateRegex.Replace(templateCode, string.Empty);

            templateLibraryCode = prefixCode +
                generateTemplateLibraryCode(new StringReader(templateCode), writer) +
                suffixCode;

            return compileTemplateLibraryCode(references, file, lineNumberDelta, templateLibraryCode, defineConstants, fileAndLineMatcher);
        }

        private static string generateTemplateLibraryCode(TextReader reader, string writerName) {

            State state = State.TemplateMode;

            StringBuilder temp = new StringBuilder();
            StringBuilder code = new StringBuilder();

            string indentation = string.Empty;
            code.Append(indentation);
            while (reader.Peek() > -1) {
                state = processChar(ref indentation, (char)reader.Read(), state, temp, code, writerName);
            }
            dump(state, temp, code, writerName);

            return code.ToString().TrimEnd();
        }

        private enum State {
            TemplateMode,
            ScriptMode,
            ScriptEval,
            LeftAngle,
            LeftAnglePercent,
            Percent,
            EvalPercent,
        };

        private static void dump(State state, StringBuilder temp, StringBuilder code, string writerName) {

            if (temp.Length != 0) {
                switch (state) {
                    case State.TemplateMode:
                    case State.LeftAngle:
                    case State.LeftAnglePercent:
                        code.Append(string.Format("{0}.Write(\"{1}\");", writerName, temp));
                        break;
                    case State.ScriptEval:
                    case State.EvalPercent:
                        code.Append(string.Format("{0}.Write({1});", writerName, temp));
                        break;
                    default:
                        code.Append(temp);
                        break;
                }
                temp.Length = 0;
            }
        }
        
        private static State processChar(ref string indentation, char ch, State state, StringBuilder temp, StringBuilder code, string writerName) {

            switch (state) {
                case State.TemplateMode:
                    if (ch == '<') {
                        state = State.LeftAngle;
                    } else {
                        accumulateTemplateChar(ref indentation, ch, state, temp, code, writerName);
                    }
                    break;

                case State.LeftAngle:
                    if (ch == '%') {
                        state = State.LeftAnglePercent;
                    } else {
                        accumulateTemplateChar(ref indentation, '<', state, temp, code, writerName);
                        state = State.TemplateMode;
                        state = processChar(ref indentation, ch, state, temp, code, writerName);
                    }
                    break;

                case State.LeftAnglePercent:
                    if (ch == '=') {
                        dump(state, temp, code, writerName);
                        state = State.ScriptEval;
                    } else {
                        code.Append("  ");
                        if (code.ToString().EndsWith(indentation)) {
                            code.Remove(code.Length - indentation.Length, indentation.Length);
                        }
                        dump(state, temp, code, writerName);
                        state = State.ScriptMode;
                        goto case State.ScriptMode;
                    }
                    break;

                case State.ScriptMode:
                    if (ch == '%') {
                        state = State.Percent;
                    } else {
                        if (ch == '{') {
                            indentation += "    ";
                        } else if (ch == '}') {
                            if (indentation.Length >= 4) {
                                indentation = indentation.Substring(0, indentation.Length - 4);
                            }
                        }
                        temp.Append(ch);
                    }
                    break;

                case State.ScriptEval:
                    if (ch == '%') {
                        state = State.EvalPercent;
                    } else {
                        temp.Append(ch);
                    }
                    break;

                case State.EvalPercent:
                case State.Percent:
                    if (ch == '>') {
                        dump(state, temp, code, writerName);
                        if (state == State.Percent) {
                            code.Append(indentation);
                        }
                        state = State.TemplateMode;
                    } else {
                        temp.Append(ch);
                    }
                    break;
            }

            return state;
        }

        private static void accumulateTemplateChar(ref string indentation, char ch, State state, StringBuilder temp, StringBuilder code, string writerName) {
            switch (ch) {
                case '\\':
                    temp.Append("\\\\");
                    break;

                case '\r':
                    temp.Append("\\r");
                    break;

                case '\n':
                    temp.Append("\\n");
                    break;

                case '"':
                    temp.Append("\\\"");
                    break;

                case '\t':
                    temp.Append("\\t");
                    break;
                
                default:
                    temp.Append(ch);
                    break;
            }

            if (ch == '\n') {                
                dump(state, temp, code, writerName);
                code.AppendLine();
                code.Append(indentation);
            }
        }

        private static string TrimLastNewLine(string s) {
            if (s.Length >= 2) {
                return s.Substring(0, s.Length - 2);
            } else {
                return s;
            }
        }
    }
}