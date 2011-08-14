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

namespace PreSharp {

    public static class Utils {

        public static string TemplateParametersDeclaration(int number_of_T_parameters,
                                                           params string[] first_template_parameters) {
            string parameters = string.Empty;

            for (int i = 0; i < first_template_parameters.Length; ++i) {
                if (parameters != string.Empty) {
                    parameters += ", ";
                }
                parameters += first_template_parameters[i];
            }

            for (int i = 1; i <= number_of_T_parameters; ++i) {
                if (parameters != string.Empty) {
                    parameters += ", ";
                }
                parameters += "T" + i;
            }

            if (number_of_T_parameters + first_template_parameters.Length != 0) {
                parameters = "<" + parameters + ">";
            }

            return parameters;
        }

        public static string TemplateParametersTypeArray(int number_of_T_parameters,
                                                         params string[] first_template_parameters) {

            string parameters = string.Empty;

            for (int i = 0; i < first_template_parameters.Length; ++i) {
                if (parameters != string.Empty) {
                    parameters += ", ";
                }
                parameters += "typeof(" + first_template_parameters[i] + ")";
            }

            for (int i = 1; i <= number_of_T_parameters; ++i) {
                if (parameters != string.Empty) {
                    parameters += ", ";
                }
                parameters += "typeof(T" + i + ")";
            }

            if (number_of_T_parameters + first_template_parameters.Length != 0) {
                parameters = "new Type[] { " + parameters + " }";
            } else {
                parameters = "new Type[0]";
            }
            return parameters;
        }
    }
}