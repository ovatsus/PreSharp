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
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CSharp;

internal sealed partial class PreSharpGenerator : MarshalByRefObject {

    private readonly List<string> references = new List<string>();

    public IEnumerable<string> References {
        get { return references; }
    }

    public PreSharpGenerator() {
    
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
    }

    public void AddReference(string reference) {
        references.Add(reference);        
    }

    private Logger logger;
    private bool debugMode;
    private IEnumerable<string> dependencyPaths;
    private string conditionalCompilationSymbols;    

    public void Init(bool debugMode, Logger logger, IEnumerable<string> dependencyPaths, string conditionalCompilationSymbols) {
        this.debugMode = debugMode;
        this.logger = logger;
        this.dependencyPaths = dependencyPaths;
        this.conditionalCompilationSymbols = conditionalCompilationSymbols;        
    }

    private static readonly CSharpCodeProvider codeProvider =
        new CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v3.5" } });


    public bool GenerateLibraryAssemblyFromTemplateLibraryFiles(IEnumerable<string> templateLibraryFiles,
                                                                string outputAssemblyPath,
                                                                List<string> compileGeneratedFiles) {

        List<string> processedTemplateLibraryFiles = new List<string>();

        processInplaceFiles(templateLibraryFiles, (filename, content) => {

            string processedTemplateLibraryFile;

            if (content.Contains("PRESHARP_FILE_IS_TEMPLATE_LIBRARY")) {

                processedTemplateLibraryFile = filename + ".PreSharpDebug.cs";
                File.WriteAllText(processedTemplateLibraryFile, content.Replace("PRESHARP_FILE_IS_TEMPLATE_LIBRARY", "true"));
                logger.LogMessage("Generated debug file '" + processedTemplateLibraryFile + "'.");

            } else {

                processedTemplateLibraryFile = filename;
                 
            }

            processedTemplateLibraryFiles.Add(processedTemplateLibraryFile);

            if (debugMode) {
                compileGeneratedFiles.Add(processedTemplateLibraryFile);
            }
        });

        CompilerParameters compilerOptions = new CompilerParameters();
        compilerOptions.GenerateInMemory = false;
        compilerOptions.GenerateExecutable = false;
        compilerOptions.OutputAssembly = outputAssemblyPath;
        compilerOptions.IncludeDebugInformation = true;
        compilerOptions.CompilerOptions = "/define:" + conditionalCompilationSymbols;
        foreach (string reference in references) {
            compilerOptions.ReferencedAssemblies.Add(reference);
        }

        CompilerResults results = codeProvider.CompileAssemblyFromFile(compilerOptions, processedTemplateLibraryFiles.ToArray());

        foreach (CompilerError error in results.Errors) {
            if (error.IsWarning) {
                logger.LogWarning(error.FileName, error.ErrorNumber, error.ErrorText, error.Line, error.Column);
            } else {
                logger.LogError(error.FileName, error.ErrorNumber, error.ErrorText, error.Line, error.Column);
            }
        }

        return results.Errors.Count == 0;
    }

    public void ProcessInplaceFiles(IEnumerable<string> inPlaceFiles, List<string> compileGeneratedFiles) {
        processInplaceFiles(inPlaceFiles, (file, content) => {
            compileGeneratedFiles.Add(file);
        });
    }

    private void processInplaceFiles(IEnumerable<string> inPlaceFiles, Action<string, string> fileProcessed) {

        if (inPlaceFiles != null) {
            foreach (string inPlaceFile in inPlaceFiles) {

                string[] lines;
                try {
                    lines = File.ReadAllLines(inPlaceFile);
                } catch {
                    logger.LogError(inPlaceFile, null, "Error reading file '" + inPlaceFile + "'", 0, 0);
                    continue;
                }

                string output = processTemplateRegions(lines, inPlaceFile);

                if (string.Join(Environment.NewLine, lines) + Environment.NewLine != output) {
                    File.WriteAllText(inPlaceFile, output);                
                }

                fileProcessed(inPlaceFile, output);                                
            }
        }
    }

    public void ProcessTemplateFiles(IEnumerable<string> templateFiles,
                                     List<string> compileGeneratedFiles,
                                     List<string> embeddedResourceGeneratedFiles) {

        references.Add(Assembly.GetExecutingAssembly().Location);

        if (templateFiles != null) {
            foreach (string templateFile in templateFiles) {

                string templateFileCode;
                try {                    
                    templateFileCode = File.ReadAllText(templateFile);
                } catch {
                    logger.LogError(templateFile, null, "Error reading file '" + templateFile + "'", 0, 0);
                    continue;
                }

                processTemplateFile(templateFile, templateFileCode, compileGeneratedFiles, embeddedResourceGeneratedFiles);                
            }
        }
    }   
}