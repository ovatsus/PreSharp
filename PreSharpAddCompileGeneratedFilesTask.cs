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

using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

[LoadInSeparateAppDomain]
public class PreSharpAddCompileGeneratedFilesTask : AppDomainIsolatedTask {

    [Output]
    public ITaskItem[] CompileGeneratedFiles { get; set; }

    public override bool Execute() {

        TaskLogger logger = new TaskLogger(this);

        if (File.Exists("PreSharp.CompileGeneratedFiles.cache")) {
            CompileGeneratedFiles = File.ReadAllLines("PreSharp.CompileGeneratedFiles.cache").Select(file => new TaskItem(file)).ToArray();
            foreach (var file in CompileGeneratedFiles) {
                logger.LogMessage("File '" + file + "' was added to compile list");
            }
        }

        return true;
    }
}