# MqttSql

 A simple .Net Core Windows/systemd service that subscribes to MQTT brokers and writes the messages into a local SQLite database.

## Configuration

 The service will look for a `config.json` file in the same directory with the executable. A sample of the configuration file:

 ```json
[
  {
    "host": "192.168.0.101",
    "port": 1883,
    "user": "user1",
    "password": "password1",
    "topic": "some/topic/1",
    "table": "table1"
  },
  {
    "host": "192.168.0.102",
    "port": 1883,
    "user": "user2",
    "password": "password2",
    "topic": "some/topic/2",
    "table": "table2"
  }
]
 ```

 Where `table` is the name of the table to create and write to in the SQLite database. The database will also be placed in the same directory with the name `database.sqlite`.

## Installation

 Make sure that [.Net Core 3.1](https://dotnet.microsoft.com/download/dotnet) or higher is installed. Example for Ubuntu 20.04:

 ```sh
 wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
 sudo dpkg -i packages-microsoft-prod.deb
 sudo rm packages-microsoft-prod.deb
 sudo apt update
 sudo apt install -y dotnet-runtime-3.1=3.1.21-1
 ```

 This project uses [Topshelf](https://github.com/Topshelf/Topshelf) to manage the Windows service. The Linux systemd installation will be the same as with Topshelf.

 To install the service, open an elevated shell window and navigate to the directory with the executable, for example:

 `cd "C:\Program Files\MqttSql` for Windows and `cd ~/MqttSql` for Linux.

 And run the executable with `install` and `start` arguments, for example:

 `.\MqttSql.exe install start` for Windows and `sudo ./MqttSql install --user user1 start` for Linux, where `-u` or `--user` arguments can be user to set the user the service should run with (default `root`).

 To unregister and uninstall the service, run the executable with the `uninstall` argument, for example:

 `.\MqttSql.exe uninstall` for Windows and `sudo ./MqttSql uninstall` for Linux.

 Alternatively on Windows, you can use the Extractor tool included in the project, which will put all the necesary files into the target directory, install the .Net Core runtime if necesary and install the service.

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
