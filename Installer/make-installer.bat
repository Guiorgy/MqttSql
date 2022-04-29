@echo off
.\CppEmbeddedHeaderGenerator.exe -e ".\..\Publish" -o ".\include"
.\EmbeddedHeaderParser.exe ".\include"
REM 	assuming VS2022 x64 was installed in the default directory.
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall" amd64
cd bin
echo #include "windows.h" > installer.rc
echo #define IDR_EXE_ICON       101 >> installer.rc
echo IDR_EXE_ICON	ICON	"..\\installer.ico" >> installer.rc
rc installer.rc
cl /Os /std:c++17 /Zc:externConstexpr /Zc:__cplusplus /EHsc /cgthreads4 /D_RELEASE .\..\Installer.cpp /link /MANIFEST:EMBED /MANIFESTUAC:level='requireAdministrator' installer.res
cd ..