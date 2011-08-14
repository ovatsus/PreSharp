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

using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

partial class PreSharpGenerator {

    public delegate void FileAndLineMatcher(string originalFile, int originalLine, out string file, out int line);

    private static void defaultFileAndLineMatcher(string originalFile, int originalLine, out string file, out int line) {
        file = originalFile;
        line = originalLine;
    }

    private Assembly generateTemplateLibraryAssembly(string templateFile,
                                                     string templateCode,
                                                     string prefixCode,
                                                     string suffixCode,                                                     
                                                     FileAndLineMatcher fileAndLineMatcher,
                                                     int lineNumberDelta,
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
                logger.LogError(templateFile, null, "Could not find assembly '" + assemblyName + "'.", 0, 0);
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


        CompilerParameters compilerOptions = new CompilerParameters();
        compilerOptions.GenerateInMemory = true;
        compilerOptions.GenerateExecutable = false;
        compilerOptions.IncludeDebugInformation = true;
        compilerOptions.CompilerOptions = "/define:" + conditionalCompilationSymbols;

        foreach (string reference in references) {
            compilerOptions.ReferencedAssemblies.Add(reference);
        }

        CompilerResults results = codeProvider.CompileAssemblyFromSource(compilerOptions, templateLibraryCode);

        foreach (CompilerError error in results.Errors) {
            string file;
            int line;
            fileAndLineMatcher(templateFile, error.Line, out file, out line);
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
}