using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

[LoadInSeparateAppDomain]
public class PreSharpBuildTask : AppDomainIsolatedTask {

    [Required]
    public string ConditionalCompilationSymbols { get; set; }

    [Required]
    public ITaskItem[] InPlaceFiles { get; set; }

    [Required]
    public ITaskItem[] TemplateFiles { get; set; }

    [Required]
    public ITaskItem[] TemplateIncludeFiles { get; set; }

    [Required]
    public ITaskItem[] TemplateLibraryFiles { get; set; }

    [Required]
    public ITaskItem[] DependencyPaths { get; set; }

    [Required]
    public string OutputDirectory { get; set; }

    [Required]
    public string ProjectPath { get; set; }

    [Output]
    public ITaskItem[] CompileGeneratedFiles { get; set; }

    [Output]
    public ITaskItem[] EmbeddedResourceGeneratedFiles { get; set; }

    [Output]
    public ITaskItem[] FilesToDelete { get; set; }

    public override bool Execute() {

        TaskLogger logger = new TaskLogger(this);

        List<string> compileGeneratedFiles;
        List<string> embeddedResourceGeneratedFiles;
        List<string> filesToDelete;

        bool debugMode = (ConditionalCompilationSymbols.Contains("PRESHARP_DEBUG") || Environment.GetEnvironmentVariable("PRESHARP_DEBUG") != null) && 
                         !(ConditionalCompilationSymbols.Contains("DISABLE_PRESHARP_DEBUG") || Environment.GetEnvironmentVariable("DISABLE_PRESHARP_DEBUG") != null);

        PreSharpEntryPoint.Process(
            logger,
            InPlaceFiles.Select(taskItem => taskItem.ItemSpec),
            TemplateFiles.Select(taskItem => taskItem.ItemSpec),
            TemplateIncludeFiles.Select(taskItem => taskItem.ItemSpec),
            TemplateLibraryFiles.Select(taskItem => taskItem.ItemSpec),
            DependencyPaths.Select(taskItem => taskItem.ItemSpec),
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ProjectPath), OutputDirectory)),
            out compileGeneratedFiles,
            out embeddedResourceGeneratedFiles,
            out filesToDelete,
            /*createNewAppDomain*/false,
            /*debugMode*/debugMode,
            ConditionalCompilationSymbols);

        CompileGeneratedFiles = compileGeneratedFiles.Select(file => new TaskItem(file)).ToArray();
        EmbeddedResourceGeneratedFiles = embeddedResourceGeneratedFiles.Select(file => new TaskItem(file)).ToArray();
        FilesToDelete = filesToDelete.Select(file => new TaskItem(file)).ToArray();

        return logger.Success;
    }
}