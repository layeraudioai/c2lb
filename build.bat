@echo off
setlocal
echo if y'all dun wan do make clean then hey man screw you
set CCFLAGS=-Wall -Wextra -Werror
set "C_COMPILER="
where cl.exe >nul 2>&1 && set "C_COMPILER=cl.exe"
if "%C_COMPILER%"=="" where cc.exe >nul 2>&1 && set "C_COMPILER=cc.exe"
if "%C_COMPILER%"=="" where gcc.exe >nul 2>&1 && set "C_COMPILER=gcc.exe"
if "%C_COMPILER%"=="" (
    echo Error: No C compiler found on PATH. Install gcc or Visual C++ Build Tools and rerun.
    exit /b 1
)
if /I "%C_COMPILER%"=="cl.exe" (
    set "COMPILE_LOADER=cl /DLOADER /Fe:tools\loader\loader.exe tools\dir2exe.c"
    set "COMPILE_PACKER=cl /DPACKER /Fe:tools\packer\packer.exe tools\dir2exe.c"
) else (
    set "COMPILE_LOADER=%C_COMPILER% %CCFLAGS% -o tools\loader\loader.exe -DLOADER tools\dir2exe.c"
    set "COMPILE_PACKER=%C_COMPILER% %CCFLAGS% -o tools\packer\packer.exe -DPACKER tools\dir2exe.c"
)
for %%f in (%*%) do (
    if "%%f"=="tools" (
        if exist tools\loader rmdir /S /Q tools\loader
        if exist tools\packer rmdir /S /Q tools\packer
        if not exist tools mkdir tools
        mkdir tools\loader 2>nul
        mkdir tools\packer 2>nul
        %COMPILE_LOADER%
        if errorlevel 1 exit /b %ERRORLEVEL%
        %COMPILE_PACKER%
        if errorlevel 1 exit /b %ERRORLEVEL%
        move tools\loader\loader.exe tools
        move tools\packer\packer.exe tools
        if exist tools\loader rmdir /S /Q tools\loader
        if exist tools\packer rmdir /S /Q tools\packer
	echo tools ready
    ) 
    if "%%f"=="samples" (
	cd Content 
        for /R /d %%d in (*) do  (
             cd %%d
	     for /R /d %%x in (*) do  (
	         rmdir /s /q %%x
             )
             cd ..
             rmdir /s /q %%d
        )
        cd ..
        echo Building SampleGenerator...
        echo Building SampleGenerator with dotnet... 
        dotnet build src/Tools/SampleGenerator.csproj -c Release -o bin\SampleGen || (echo **ERROR** building SampleGenerator && exit /b %ERRORLEVEL%)
        echo Running SampleGenerator...
        copy MidiInstrumentSamplesList.md bin\SampleGen
	cd tools
	packer.exe ..\bin\SampleGen ..\bin\SampleGen\SampleGenerator.exe SampGenXBCX.exe
	cd ..
        if errorlevel 1 (
            echo **ERROR** building SampleGenerator
            exit /b %ERRORLEVEL%
        )
	tools\SampGenXBCX.exe
        echo Running SampleGenerator...
    )
    if "%%f"=="content" (
        cd Content
        del Content.mgcb
        copy Template.mgcb Content.mgcb
        for %%f in (*.spritefont) do (
            findstr /I /L /C:"#begin %%f" Content.mgcb >nul || (
                echo #begin %%f>> Content.mgcb
                echo /importer:FontDescriptionImporter>> Content.mgcb
                echo /processor:FontDescriptionProcessor>> Content.mgcb
                echo /build:%%f>> Content.mgcb
                echo Added %%f to Content.mgcb
            )
        )
        for /R %%f in (*.wav) do (
            findstr /I /L /C:"#begin %%f" Content.mgcb >nul || (
               echo #begin %%f>> Content.mgcb
               echo /importer:WavImporter>> Content.mgcb
               echo /processor:SoundEffectProcessor>> Content.mgcb
               echo /build:%%f>> Content.mgcb
               echo Added %%f to Content.mgcb
            )
	)
        cd ..
        echo content ready
    )
    if "%%f"=="clean" (
        rmdir /S /Q bin
        rmdir /S /Q obj
        rmdir /S /Q Content\bin
        rmdir /S /Q Content\obj
        echo clean complete
    )
)
dotnet build --configuration=release /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /warnaserror /p:TreatWarningsAsErrors=true || exit /b %ERRORLEVEL%
echo BUILD COMPLETE!
for %%f in (%*%) do (
    if "%%f"=="pack" (
        mkdir bin\Release\net8.0\tools
        copy tools\*.exe bin\Release\net8.0\tools
        cd tools
        packer ..\bin\Release\net8.0\ ..\bin\Release\net8.0\ToyConEngine.exe ..\LABOx64.exe
        cd ..
        echo Packing complete!
        echo all done!
    )
)
