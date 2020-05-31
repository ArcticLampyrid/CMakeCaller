#include<iostream>
using namespace std;
int main()
{
    cout<<"Hello World"<<endl;
#ifdef __cplusplus
    cout<<"C++ Language Specification Version: "<<__cplusplus<<endl;
#endif
#if defined(__clang__)
    cout<<"Compiler: Clang, Version="<<__clang_major__<<"."<<__clang_minor__<<"."<<__clang_patchlevel__<<endl;
#elif defined(__GNUC__)
    cout<<"Compiler: GCC, Version="<<__GNUC__<<"."<<__GNUC_MINOR__<<"."<<__GNUC_PATCHLEVEL__<<endl;
#elif defined(_MSC_VER)
    cout<<"Compiler: MSVC, Version="<<_MSC_VER<<endl;
#endif
    return 0;
}