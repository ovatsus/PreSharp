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

namespace PreSharp {

    internal class CommandLineLogger : Logger {
               
        public override void LogMessage(string message) {
            Console.WriteLine(message);
        }

        public override void LogWarning(string file, string errorCode, string message, int line, int column) {
            Console.WriteLine("{0}({1},{2}): {3} {4}: {5}",
                              file,
                              line,
                              column,
                              "warning",
                              errorCode,
                              message);
        }

        public override void LogError(string file, string errorCode, string message, int line, int column) {
            Console.WriteLine("{0}({1},{2}): {3} {4}: {5}",
                              file,
                              line,
                              column,
                              "error",
                              errorCode,
                              message);
            Success = false;
        }

        public override void LogException(string file, Exception exception) {
            Console.WriteLine("{0}: {1}", file, exception);
            Success = false;
        }
    }
}
