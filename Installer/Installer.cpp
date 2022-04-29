#include "include/command.hpp"

#ifdef _RELEASE
#include "include/embedded-mqttsql.hpp"
#else
#include <filesystem>
#include <iostream>

namespace embedded
{
    namespace mqttsql
    {
        void extractDotNetRuntime(std::string const outputDir = ".", bool verbose = false) {}
        std::string_view dotNetRuntimePath = "";
		void extractConfigFile(std::string const outputDir = ".", bool verbose = false) {}
        std::string_view configFilePath = "";
		void extractExecutable(std::string const outputDir = ".", bool verbose = false) {}
        std::string_view executablePath = "";
		void extractDependencies(std::string const outputDir = ".", bool verbose = false) {}
    }
}
#endif
using namespace embedded::mqttsql;


const std::string DotNetVersion = "6.0.2";

int main(int argc, char* argv[])
{
    using namespace command;

    std::string targetDir = argc > 1 ? argv[1] : "C:\\Program Files\\MqttSql\\";
    if (targetDir[targetDir.length() - 1] != '\\'
        && targetDir[targetDir.length() - 1] != '/')
        targetDir += '\\';
    std::cout << "Target directory set to \"" << targetDir << '"' << std::endl;

    if (!std::filesystem::exists(targetDir)) {
        std::cout << "Creating directory \"" << targetDir << '"' << std::endl;
        std::filesystem::create_directory(targetDir);
    }

    auto dotnetinfo = Command::exec("dotnet --info");
    if (dotnetinfo.exitstatus != 0 || dotnetinfo.output.find(DotNetVersion) == std::string::npos) {
        std::cout << "Extracting the .Net Runtime \"" << configFilePath << '"' << std::endl;
        extractDotNetRuntime(targetDir);
        if (dotNetRuntimePath != "") {
            auto dotnetinstall = Command::exec(targetDir + dotNetRuntimePath.data() + " /install /quiet /norestart");
            if (dotnetinstall.exitstatus != 0) std::cout << ".NET Runtime installation failed!" << std::endl;
        }
    }

    if (configFilePath != "") {
        std::cout << "Extracting the service configuration configuration \"" << configFilePath << '"' << std::endl;
        extractConfigFile(targetDir);
    }

    std::cout << "Extracting the service dependencies" << std::endl;
    extractDependencies(targetDir);

    if (executablePath != "") {
        std::cout << "Extracting the service executable \"" << executablePath << '"' << std::endl;
        extractExecutable(targetDir);
        std::cout << "Installing \"" << executablePath << '"' << "service" << std::endl;
        Command::exec(targetDir + executablePath.data() + " install");
    }
}