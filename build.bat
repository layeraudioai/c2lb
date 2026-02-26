echo off
cls
echo if y'all dun wan do make clean then hey man screw you
for %%f in (%*) do (
    if "%%f"=="clean" (
        rmdir /S /Q bin
        rmdir /S /Q obj
        rmdir /S /Q Content\bin
        rmdir /S /Q Content\obj
        echo clean complete
        pause
    )
    if "%%f"=="content" (
        cd Content
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
        pause
    )

)
cls
dotnet publish --configuration=release --sc /p:PublishSingleFile=true --p:PublishTrimmed=true --p:PublishReadyToRun=true
echo Build complete!