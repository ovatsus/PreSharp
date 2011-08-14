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
using System.Text.RegularExpressions;

partial class PreSharpGenerator {

    private void processTemplateFile(string templateFile,
                                     string templateFileCode,
                                     List<string> compileGeneratedFiles,
                                     List<string> embeddedResourceGeneratedFiles) {
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

        string generatedClassName = "GeneratedClass_" + Guid.NewGuid().ToString("N");
        string suffix = "\r\n" +
            "\r\n" +
            "internal static class " + generatedClassName + " {\r\n" +
            "\r\n" +
            "    private static void DumpOutput() {\r\n" +
            "        " + entryPoint + "\r\n" +
            "    }\r\n" +
            "}\r\n";

        templateFileCode = templateFileCode.Replace("#if PRESHARP_TEMPLATE\r\n", "\r\n%>");
        templateFileCode = templateFileCode.Replace("#if PRESHARP_TEMPLATE\n", "\n%>");
        templateFileCode = templateFileCode.Replace("\r\n#endif", "<%\r\n");
        templateFileCode = templateFileCode.Replace("\n#endif", "<%\n");
        templateFileCode = templateFileCode.Replace("#endif", "<%");

        try {
            string templateLibraryCode;
            Assembly generatedAssembly = generateTemplateLibraryAssembly(templateFile,
                                                                         "<%" + templateFileCode + "%>",
                                                                         string.Empty,
                                                                         suffix,
                                                                         fileAndLineMatcher,
                                                                         lineNumberDelta,
                                                                         "PreSharp.Writer",
                                                                         out templateLibraryCode);
            
            if (debugMode) {
                string processedTemplateFile = templateFile + ".PreSharpDebug.cs";
                File.WriteAllText(processedTemplateFile, templateLibraryCode);
                logger.LogMessage("Generated debug file '" + processedTemplateFile + "'.");
            }

            if (generatedAssembly == null) {
                return;
            }

            try {
                Type generatedClassType = generatedAssembly.GetType(generatedClassName);
                generatedClassType.GetMethod("DumpOutput", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

                List<string> compileGeneratedFilesFromThisTemplate = PreSharp.CompileGeneratedFiles;
                List<string> embeddedResourceGeneratedFilesFromThisTemplate = PreSharp.EmbeddedResourceGeneratedFiles;

                if (compileGeneratedFilesFromThisTemplate.Count == 0 && embeddedResourceGeneratedFilesFromThisTemplate.Count == 0) {
                    logger.LogError(templateFile, null, "At least one template output must be specified by using PreSharp.SetOutput(string outputFile, PreSharp.OutputType outputType)", 0, 0);
                } else {
                    foreach (string compileGeneratedFile in compileGeneratedFilesFromThisTemplate) {
                        logger.LogMessage("Generated file '" + compileGeneratedFile + "' from template '" + templateFile + "'.");
                        compileGeneratedFiles.Add(compileGeneratedFile);
                    }
                    foreach (string embeddedResourceGeneratedFile in embeddedResourceGeneratedFilesFromThisTemplate) {
                        logger.LogMessage("Generated file '" + embeddedResourceGeneratedFile + "' from template '" + templateFile + "'.");
                        embeddedResourceGeneratedFiles.Add(embeddedResourceGeneratedFile);
                    }
                }
            } finally {
                PreSharp.Flush();
                PreSharp.CompileGeneratedFiles.Clear();
                PreSharp.EmbeddedResourceGeneratedFiles.Clear();
            }
        } catch (TargetInvocationException exception) {
            logger.LogException(templateFile, exception.InnerException);
        } catch (Exception exception) {
            logger.LogException(templateFile, exception);
        }
    }
}