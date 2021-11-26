# Extractor

 A simple .Net Framework 3.5 tool that extracts embeeded files into the target directory, installs the .Net Core runtime if necesary and installs the Windows service.

## Preparation

 Put the service executable and all other necesary files into the `Embeeded` directory, and change their `Build Action` in the `Properties` to `Embedded Resource`.

 Modify the `Extractor.Program` fields as needed:

 ```cs
static readonly string DotNetRuntime = "DotNetCore-3.1.21-win-x64.exe"; // the .Net Core runtime MqttSql service uses
static readonly string ServiceConfig = "config.json"; // the service configuration file
static readonly string[] Dependencies = new string[] { // the rest of the files created when building MqttSql.
        "Microsoft.Win32.SystemEvents.dll",
        "MQTTnet.dll",
        "MqttSql.deps.json",
        "MqttSql.dll",
        "MqttSql.runtimeconfig.json",
        "Newtonsoft.Json.dll",
        "SQLite.Interop.dll",
        "System.Data.SQLite.dll",
        "System.Diagnostics.EventLog.dll",
        "System.ServiceProcess.ServiceController.dll",
        "Topshelf.dll",
        "TopShelf.ServiceInstaller.dll"
    };
static readonly string Service = "MqttSql.exe";
 ```

 If any of the file names are changed, you'll need to remove the old file from the `Solution Explorer`, add the new file and change the `Build Action` in the `Properties` to `Embedded Resource`.

 If a different .Net runtime was used to build MqttSql, then first repeat the step above and after modify the next line with the apropriate version:

 `if (dotnetinfo == null || !dotnetinfo.Contains("3.1.21"))`

 Build the project.

## Installation

 Just run the executable as admin. Alternatively, you may pass the path to the target directory as the first argument from the command line, for example:

 `.\Extractor.exe C:\MqttSql`

## MIT License

Copyright (c) 2021 Guiorgy

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
