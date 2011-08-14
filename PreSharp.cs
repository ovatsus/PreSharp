//Copyright (c) 2008 Gustavo Guerra

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the Software), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in
//all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//THE SOFTWARE.

using System.Collections.Generic;
using System.IO;

public static class PreSharp {

    private static TextWriter writer;
    public static TextWriter Writer { get { return writer; } }

    internal static readonly List<string> CompileGeneratedFiles = new List<string>();
    internal static readonly List<string> EmbeddedResourceGeneratedFiles = new List<string>();

    private static string currentFile;

    public enum OutputType { Compile, EmbeddedResource }

    public static void Flush() {
        if (currentFile != null) {
            string output = writer.ToString();
            if (!File.Exists(currentFile) || output != File.ReadAllText(currentFile)) {
                File.WriteAllText(currentFile, output);
            }
            currentFile = null;
            writer = null;
        }
    }

    public static void SetOutput(string outputFile, OutputType outputType) {
        Flush();
        currentFile = outputFile;
        writer = new StringWriter();
        if (outputType == OutputType.Compile) {
            CompileGeneratedFiles.Add(outputFile);
        } else {
            EmbeddedResourceGeneratedFiles.Add(outputFile);
        }
    }
}