echo off
echo if y'all dun wan do make clean then hey man screw you
rmdir /S /Q bin
rmdir /S /Q obj
rmdir /S /Q Content\bin
rmdir /S /Q Content\obj
dotnet build --configuration=release
