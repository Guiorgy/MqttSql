using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;

namespace EmbeddedHeaderParser
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0) throw new ArgumentException("Path to the include directory must be passed as the first argument!");

            var code = new StringBuilder()
                .AppendLine("#ifndef EMBEDDED_RESOURCES_MQTTSQL_HEADER_FILE")
                .AppendLine("#define EMBEDDED_RESOURCES_MQTTSQL_HEADER_FILE")
                .AppendLine()
                .AppendLine("#include \"embedded-extractor.hpp\"")
                .AppendLine()
                .AppendLine("namespace embedded")
                .AppendLine("{")
                .AppendLine("\tnamespace mqttsql")
                .AppendLine("\t{")
                .AppendLine();

            string? runtime = null;
            string? config = null;
            string? exe = null;
            List<string> dependencies = new();

            var text = File.ReadAllText(Path.Combine(args[0], "embedded-extractor.hpp"));
            text = text[text.IndexOf("void extractAll(std::string const outputDir = \".\", bool verbose = false)")..];
            var matches = Regex.Matches(text, "extract_(.*)\\(outputDir, verbose\\);");
            foreach (Match match in matches)
            {
                var res = match.Groups[1].Value;

                if (res.StartsWith("Runtime_dirSep_"))
                {
                    if (runtime != null) throw new GenerationException("Multiple .NET runtimes included.");
                    runtime = res;
                    continue;
                }

                if (res == "config_json")
                {
                    if (config != null) throw new GenerationException("Multiple configuration files included.");
                    config = res;
                    continue;
                }

                if (res == "MqttSql_exe")
                {
                    if (exe != null) throw new GenerationException("Multiple executables included.");
                    exe = res;
                    continue;
                }

                dependencies.Add(res);
            }

            code.AppendLine("\t\tvoid extractDotNetRuntime(std::string const outputDir = \".\", bool verbose = false) {");
            if (runtime != null)
                code.Append("\t\t\textract_").Append(runtime).AppendLine("(outputDir, verbose);");
            else
                code.AppendLine("\t\t\t// No runtime embedded");
            code.AppendLine("\t\t}")
                .Append("\t\tstd::string_view dotNetRuntimePath = ").Append(runtime != null ? $"{runtime}_name" : "\"\"").AppendLine(";")
                .AppendLine();

            code.AppendLine("\t\tvoid extractConfigFile(std::string const outputDir = \".\", bool verbose = false) {");
            if (config != null)
                code.Append("\t\t\textract_").Append(config).AppendLine("(outputDir, verbose);");
            else
                code.AppendLine("\t\t\t// No configuration file embedded");
            code.AppendLine("\t\t}")
                .Append("\t\tstd::string_view configFilePath = ").Append(config != null ? $"{config}_name" : "\"\"").AppendLine(";")
                .AppendLine();

            code.AppendLine("\t\tvoid extractExecutable(std::string const outputDir = \".\", bool verbose = false) {");
            if (exe != null)
                code.Append("\t\t\textract_").Append(exe).AppendLine("(outputDir, verbose);");
            else
                code.AppendLine("\t\t\t// No executable embedded");
            code.AppendLine("\t\t}")
                .Append("\t\tstd::string_view executablePath = ").Append(exe != null ? $"{exe}_name" : "\"\"").AppendLine(";")
                .AppendLine();

            code.AppendLine("\t\tvoid extractDependencies(std::string const outputDir = \".\", bool verbose = false) {");
            foreach (var dep in dependencies)
                code.Append("\t\t\textract_").Append(dep).AppendLine("(outputDir, verbose);");
            code.AppendLine("\t\t}")
                .AppendLine();

            code.AppendLine("\t}")
                .AppendLine("}")
                .AppendLine()
                .AppendLine("#endif");

            File.WriteAllText(Path.Combine(args[0], "embedded-mqttsql.hpp"), code.ToString());
        }

        [Serializable]
        public class GenerationException : Exception
        {
            public GenerationException() { }
            public GenerationException(string? message) : base(message) { }
            public GenerationException(string? message, Exception? innerException) : base(message, innerException) { }
            protected GenerationException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }
    }
}