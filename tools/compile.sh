rm *.exe
rm */*.exe
mkdir loader
gcc -o loader/loader.exe -DLOADER dir2exe.c
mkdir packer
gcc -o packer/packer.exe -DPACKER dir2exe.c
mv */*.exe .
rmdir *