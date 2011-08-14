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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

partial class PreSharpGenerator {

    private static readonly string PRESHARP_TEMPLATE_MARKER = "#if PRESHARP_TEMPLATE";
    private static readonly string PRESHARP_TEMPLATE_LIBRARY_MARKER = "#if PRESHARP_TEMPLATE_LIBRARY";

    private string processTemplateRegions(string[] lines,
                                          string file) {

        StringBuilder outputBuilder = new StringBuilder();
        IEnumerator<string> lineIterator = lines.Cast<string>().GetEnumerator();

        int lineNumberBeforeScript = -1;
        int ifNesting = 0;

        while (lineIterator.MoveNext()) {
            string currentLine = lineIterator.Current;
            string trimmedLine = currentLine.Trim();
            lineNumberBeforeScript += 1;

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

                ifNesting += 1;

                StringBuilder region = new StringBuilder();
                while (ifNesting > 0 && lineIterator.MoveNext()) {
                    currentLine = lineIterator.Current;
                    trimmedLine = currentLine.TrimStart();
                    linesOfThisScript += 1;

                    if (trimmedLine.StartsWith("#if")) {
                        ifNesting += 1;
                    } else if (trimmedLine.StartsWith("#endif")) {
                        ifNesting -= 1;
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
                        generatedCode = generateTemplateLibraryCode(new StringReader(region.ToString()), writerName);
                    } else {
                        generatedCode = generateTemplateRegionOutput(file, region.ToString(), lineNumberBeforeScript);
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

    private string generateTemplateRegionOutput(string file,
                                                string templateRegionCode,
                                                int lineNumberDelta) {
        string prefix =
            "\r\n" +
            "using System;\r\n" +
            "using System.IO;\r\n" +
            "\r\n" +
            "namespace PreSharp {\r\n" +
            "\r\n" +
            "    internal static class Generator {\r\n" +
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
        Assembly generatedAssembly = generateTemplateLibraryAssembly(file,
                                                                     templateRegionCode,
                                                                     prefix,
                                                                     suffix,
                                                                     defaultFileAndLineMatcher,
                                                                     lineNumberDelta - 11,
                                                                     "writer",
                                                                     out templateLibraryCode);

        if (generatedAssembly == null) {
            return null;
        }

        try {
            StringWriter writer = (StringWriter)generatedAssembly.GetType("PreSharp.Generator").GetMethod("Generate", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
            return writer.ToString();
        } catch (Exception exception) {
            logger.LogException(file, exception);
            return null;
        }
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
   
    private static string TrimLastNewLine(string s) {
        if (s.Length >= 2) {
            return s.Substring(0, s.Length - 2);
        } else {
            return s;
        }
    }
}