echo off
echo if y'all dun wan do make clean then hey man screw you
rmdir /S /Q bin
rmdir /S /Q obj
rmdir /S /Q Content\bin
rmdir /S /Q Content\obj

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

dotnet build --configuration=release
