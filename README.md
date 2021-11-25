# MqttSql

 A simple .Net Core Windows service that subscribes to MQTT brokers and writes the messages into a local SQLite database

## Configuration

 The service will look for a `config.json` file in the same directory with the executable. A sample of the configuration file:

 ```json
[
  {
    "host": "192.168.0.101",
    "port": 1883,
    "user": "user1",
    "password": "password1",
    "topic": "some/tpic/1",
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

 This project uses [TopShelf](https://github.com/Topshelf/Topshelf) to manage the Windows service.

 To install the windows service, open an elevated shell window using navigate to the directory with the executable, for example:

 `cd "C:\Program Files\MqttSql`

 And run the executable with `install` and `start` arguments, for example:

 `.\MqttSql.exe install start`

 To unregister and uninstall the service, run the executable with the `uninstall` argument, for example:

 `.\MqttSql.exe uninstall`

 Alternatively, you can use the Extractor tool included in the project, which will put all the necesary files into the target directory, install the .Net Core runtime if necesary and install the service.

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
