// dir2exe.c
// A dual-purpose C file for creating self-extracting executables on Windows.
//
// To compile:
// 1. For the loader: cl.exe /DLOADER dir2exe.c /o loader.exe
// 2. For the packer: cl.exe dir2exe.c /o packer.exe
//
// You need the Visual C++ compiler (cl.exe) from the Visual Studio Build Tools.
//
// Usage for packer.exe:
// packer.exe <directory_to_pack> <main_executable_to_run> <output_packed_exe_name>
//
// Example:
// packer.exe Content ToyConEngine.exe MyGame.exe

#define _CRT_SECURE_NO_WARNINGS
#include <windows.h>
#include <stdio.h>
#include <stdint.h>
#include <string.h>
#include <stdlib.h>


#define MAGIC_BYTES "DIR2EXE" // 7 bytes + null

// This footer is written at the very end of the packed file.
// It allows the loader to find the manifest of packed files.
#pragma pack(push, 1)
typedef struct {
    uint64_t manifest_offset;
    char     magic[8];
} ArchiveFooter;

// This describes a single file within the archive.
typedef struct {
    uint64_t offset;
    uint64_t size;
    uint8_t  is_executable; // 1 if this is the main exe, 0 otherwise
    char     relative_path[MAX_PATH];
} ManifestEntry;
#pragma pack(pop)


#ifdef LOADER
// =================================================================================================
// LOADER IMPLEMENTATION
// This code runs when the final, packed executable is launched.
// =================================================================================================

// Recursively deletes a directory and all its contents.
BOOL recursive_delete_directory(const char* path) {
    char search_path[MAX_PATH];
    snprintf(search_path, MAX_PATH, "%s\\*.*", path);

    WIN32_FIND_DATAA find_data;
    HANDLE h_find = FindFirstFileA(search_path, &find_data);

    if (h_find == INVALID_HANDLE_VALUE) {
        return FALSE;
    }

    do {
        if (strcmp(find_data.cFileName, ".") != 0 && strcmp(find_data.cFileName, "..") != 0) {
            char full_path[MAX_PATH];
            snprintf(full_path, MAX_PATH, "%s\\%s", path, find_data.cFileName);

            if (find_data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
                recursive_delete_directory(full_path);
            } else {
                DeleteFileA(full_path);
            }
        }
    } while (FindNextFileA(h_find, &find_data));

    FindClose(h_find);
    return RemoveDirectoryA(path);
}

int main(int argc, char* argv[]) {
    printf("booting.... please wait until the window opens\n");
    char self_path[MAX_PATH];
    GetModuleFileNameA(NULL, self_path, MAX_PATH);

    FILE* f_self = fopen(self_path, "rb");
    if (!f_self) {
        return 1;
    }

    // 1. Find and read the footer to locate the manifest.
    fseek(f_self, -sizeof(ArchiveFooter), SEEK_END);
    ArchiveFooter footer;
    fread(&footer, sizeof(ArchiveFooter), 1, f_self);

    if (strcmp(footer.magic, MAGIC_BYTES) != 0) {
        fclose(f_self);
        return 2; // Not a valid archive
    }

    // 2. Read the manifest.
    fseek(f_self, footer.manifest_offset, SEEK_SET);
    uint64_t entry_count;
    fread(&entry_count, sizeof(uint64_t), 1, f_self);

    ManifestEntry* manifest = (ManifestEntry*)malloc(sizeof(ManifestEntry) * entry_count);
    fread(manifest, sizeof(ManifestEntry), entry_count, f_self);

    // 3. Create a unique temporary directory.
    char temp_path[MAX_PATH];
    char temp_dir[MAX_PATH];
    GetTempPathA(MAX_PATH, temp_path);
    GetTempFileNameA(temp_path, "d2e", 0, temp_dir);
    DeleteFileA(temp_dir); // GetTempFileName creates a file, we want a directory
    CreateDirectoryA(temp_dir, NULL);

    char main_exe_path[MAX_PATH] = { 0 };

    // 4. Extract all files from the manifest.
    char* buffer = (char*)malloc(1024 * 1024); // 1MB buffer
    for (uint64_t i = 0; i < entry_count; ++i) {
        char out_path[MAX_PATH];
        snprintf(out_path, MAX_PATH, "%s\\%s", temp_dir, manifest[i].relative_path);

        // Create subdirectories if necessary
        for (char* p = out_path + strlen(temp_dir) + 1; *p; ++p) {
            if (*p == '\\' || *p == '/') {
                *p = '\0';
                CreateDirectoryA(out_path, NULL);
                *p = '\\';
            }
        }

        FILE* f_out = fopen(out_path, "wb");
        if (!f_out) continue;

        fseek(f_self, manifest[i].offset, SEEK_SET);
        uint64_t remaining = manifest[i].size;
        while (remaining > 0) {
            size_t to_read = (remaining > 1024 * 1024) ? 1024 * 1024 : remaining;
            fread(buffer, 1, to_read, f_self);
            fwrite(buffer, 1, to_read, f_out);
            remaining -= to_read;
        }
        fclose(f_out);

        if (manifest[i].is_executable) {
            strcpy(main_exe_path, out_path);
        }
    }
    free(buffer);
    fclose(f_self);

    DWORD exit_code = 1;

    // 5. Run the main executable and wait for it to finish.
    if (strlen(main_exe_path) > 0) {
        STARTUPINFOA si = { 0 };
        PROCESS_INFORMATION pi = { 0 };
        si.cb = sizeof(si);

        // Pass our command line arguments to the child process
        char* cmd_line = GetCommandLineA();

        if (CreateProcessA(main_exe_path, cmd_line, NULL, NULL, FALSE, 0, NULL, temp_dir, &si, &pi)) {
            WaitForSingleObject(pi.hProcess, INFINITE);
            GetExitCodeProcess(pi.hProcess, &exit_code);
            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);
        }
    }

    // 6. Clean up the temporary directory.
    recursive_delete_directory(temp_dir);

    free(manifest);
    return exit_code;
}
#endif
#ifdef PACKER
// =================================================================================================
// PACKER IMPLEMENTATION
// This code runs when you use the 'packer.exe' tool.
// =================================================================================================

typedef struct ManifestNode {
    ManifestEntry entry;
    struct ManifestNode* next;
} ManifestNode;

// Appends a file to the archive and fills its manifest entry.
void append_file_to_archive(FILE* f_archive, const char* file_path, ManifestEntry* entry) {
    FILE* f_in = fopen(file_path, "rb");
    if (!f_in) {
        printf("Warning: Could not open file %s\n", file_path);
        entry->size = 0;
        return;
    }

    entry->offset = ftell(f_archive);

    fseek(f_in, 0, SEEK_END);
    entry->size = ftell(f_in);
    fseek(f_in, 0, SEEK_SET);

    char* buffer = (char*)malloc(1024 * 1024); // 1MB buffer
    size_t bytes_read;
    while ((bytes_read = fread(buffer, 1, 1024 * 1024, f_in)) > 0) {
        fwrite(buffer, 1, bytes_read, f_archive);
    }
    free(buffer);
    fclose(f_in);
}

// Recursively walks a directory and adds files to the manifest list.
void walk_directory(const char* base_path, const char* current_path, FILE* f_archive, ManifestNode** head) {
    char search_path[MAX_PATH];
    snprintf(search_path, MAX_PATH, "%s\\%s\\*", base_path, current_path);

    WIN32_FIND_DATAA find_data;
    HANDLE h_find = FindFirstFileA(search_path, &find_data);

    if (h_find == INVALID_HANDLE_VALUE) return;

    do {
        if (strcmp(find_data.cFileName, ".") != 0 && strcmp(find_data.cFileName, "..") != 0) {
            char full_path[MAX_PATH];
            char relative_path[MAX_PATH];

            snprintf(full_path, MAX_PATH, "%s\\%s\\%s", base_path, current_path, find_data.cFileName);
            snprintf(relative_path, MAX_PATH, "%s%s%s", current_path, (strlen(current_path) > 0 ? "\\" : ""), find_data.cFileName);

            if (find_data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
                walk_directory(base_path, relative_path, f_archive, head);
            } else {
                printf("Packing: %s\n", relative_path);
                ManifestNode* new_node = (ManifestNode*)malloc(sizeof(ManifestNode));
                new_node->next = *head;
                *head = new_node;

                strcpy(new_node->entry.relative_path, relative_path);
                new_node->entry.is_executable = 0;
                append_file_to_archive(f_archive, full_path, &new_node->entry);
            }
        }
    } while (FindNextFileA(h_find, &find_data));

    FindClose(h_find);
}

int main(int argc, char* argv[]) {
    if (argc != 4) {
        printf("Usage: packer.exe <directory_to_pack> <main_executable> <output_exe>\n");
        return 1;
    }

    const char* pack_dir = argv[1];
    const char* main_exe = argv[2];
    const char* out_exe = argv[3];

    FILE* f_out = fopen(out_exe, "wb");
    if (!f_out) {
        printf("Error: Could not create output file %s\n", out_exe);
        return 1;
    }

    // 1. Start with the loader stub.
    printf("Writing loader stub...\n");
    ManifestEntry loader_entry;
    append_file_to_archive(f_out, "loader.exe", &loader_entry);
    if (loader_entry.size == 0) {
        printf("Error: loader.exe not found or is empty. Compile it first.\n");
        fclose(f_out);
        return 1;
    }

    ManifestNode* manifest_head = NULL;
    uint64_t entry_count = 0;

    // 2. Walk the directory and append all its files.
    walk_directory(pack_dir, "", f_out, &manifest_head);

    // 3. Append the main executable.
    printf("Packing main executable: %s\n", main_exe);
    ManifestNode* exe_node = (ManifestNode*)malloc(sizeof(ManifestNode));
    exe_node->next = manifest_head;
    manifest_head = exe_node;
    strcpy(exe_node->entry.relative_path, strrchr(main_exe, '\\') ? strrchr(main_exe, '\\') + 1 : main_exe);
    exe_node->entry.is_executable = 1;
    append_file_to_archive(f_out, main_exe, &exe_node->entry);

    // 4. Write the manifest.
    uint64_t manifest_offset = ftell(f_out);
    
    // Count entries
    for (ManifestNode* p = manifest_head; p != NULL; p = p->next) {
        entry_count++;
    }
    fwrite(&entry_count, sizeof(uint64_t), 1, f_out);

    // Write entries and free the list
    ManifestNode* current = manifest_head;
    while (current != NULL) {
        fwrite(&current->entry, sizeof(ManifestEntry), 1, f_out);
        ManifestNode* next = current->next;
        free(current);
        current = next;
    }

    // 5. Write the footer.
    ArchiveFooter footer;
    footer.manifest_offset = manifest_offset;
    strcpy(footer.magic, MAGIC_BYTES);
    fwrite(&footer, sizeof(ArchiveFooter), 1, f_out);

    fclose(f_out);
    printf("\nSuccessfully created %s\n", out_exe);

    return 0;
}

#endif
