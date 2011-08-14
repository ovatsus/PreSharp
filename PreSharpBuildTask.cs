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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;

[LoadInSeparateAppDomain]
public class PreSharpBuildTask : AppDomainIsolatedTask {

    [Required]
    public string ConditionalCompilationSymbols { get; set; }

    [Required]
    public ITaskItem[] InPlaceFiles { get; set; }

    [Required]
    public ITaskItem[] TemplateFiles { get; set; }

    [Required]
    public ITaskItem[] TemplateLibraryFiles { get; set; }

    [Required]
    public ITaskItem[] DependencyPaths { get; set; }

    [Output]
    public ITaskItem[] FilesToDelete { get; set; }

    public override bool Execute() {

        TaskLogger logger = new TaskLogger(this);

        List<string> compileGeneratedFiles;
        List<string> embeddedResourceGeneratedFiles;
        List<string> filesToDelete;

        PreSharpEntryPoint.Process(
            logger,
            InPlaceFiles.Select(taskItem => taskItem.ItemSpec),
            TemplateFiles.Select(taskItem => taskItem.ItemSpec),
            TemplateLibraryFiles.Select(taskItem => taskItem.ItemSpec),
            DependencyPaths.Select(taskItem => taskItem.ItemSpec),
            out compileGeneratedFiles,
            out embeddedResourceGeneratedFiles,
            out filesToDelete,
            /*createNewAppDomain*/false,
            /*debugMode*/ConditionalCompilationSymbols.Contains("DEBUG"),
            ConditionalCompilationSymbols);

        using (var compileGeneratedFilesCache = new StreamWriter("PreSharp.CompileGeneratedFiles.cache")) {
            foreach (var compileGeneratedFile in compileGeneratedFiles) {
                compileGeneratedFilesCache.WriteLine(compileGeneratedFile);
            }
        }

        using (var embeddedResourceGeneratedFilesCache = new StreamWriter("PreSharp.EmbeddedResourceGeneratedFiles.cache")) {
            foreach (var embeddedResourceGeneratedFile in embeddedResourceGeneratedFiles) {
                embeddedResourceGeneratedFilesCache.WriteLine(embeddedResourceGeneratedFile);
            }
        }
        
        FilesToDelete = filesToDelete.Select(file => new TaskItem(file)).ToArray();

        return logger.Success;
    }
}