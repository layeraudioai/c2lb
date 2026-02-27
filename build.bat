echo off
cls
echo if y'all dun wan do make clean then hey man screw you
for %%f in (%*%) do (
    if "%%f"=="tools" (
        del tools\*.exe
        rmdir /S /Q tools\*\
        cc -o loader/loader.exe -DLOADER dir2exe.c
        cc -o packer/packer.exe -DPACKER dir2exe.c
        move tools\*.exe .
    )
    if "%%f"=="clean" (
        rmdir /S /Q bin
        rmdir /S /Q obj
        rmdir /S /Q Content\bin
        rmdir /S /Q Content\obj
        echo clean complete
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
        for %%f in (*.wav) do (
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
)
cls
dotnet build --configuration=release /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
echo Build complete!
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