#ifdef _MSC_VER
#define _CRT_SECURE_NO_WARNINGS
#endif
#include <stdio.h>
int main()
{
    printf("Hello World\n");
#ifdef __cplusplus
    printf("C++ Language Specification Version: %ld\n", (long)__cplusplus);
#endif
#if defined(__clang__)
    printf("Compiler: Clang, Version=%ld.%ld.%ld\n", (long)__clang_major__, (long)__clang_minor__, (long)__clang_patchlevel__);
#elif defined(__GNUC__)
    printf("Compiler: Clang, Version=%ld.%ld.%ld\n", (long)__GNUC__, (long)__GNUC_MINOR__, (long)__GNUC_PATCHLEVEL__);
#elif defined(_MSC_VER)
    printf("Compiler: MSVC, Version=%ld\n", (long)_MSC_VER);
#endif
    return 0;
}