ECHO OFF
echo if y'all dun wan do make clean then hey man screw you
rmdir /S bin
rmdir /S obj
rmdir /S Content\bin
rmdir /S Content\obj
dotnet build --configuration=release
