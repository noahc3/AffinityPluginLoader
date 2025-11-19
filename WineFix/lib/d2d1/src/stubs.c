/*
 * Compatibility stubs for missing C runtime functions
 */

#include <windows.h>
#include <wchar.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>

/* wcsdup might not be available in all MinGW distributions */
#ifndef _WIN32
wchar_t *wcsdup(const wchar_t *str)
{
    size_t len;
    wchar_t *copy;

    if (!str) return NULL;

    len = wcslen(str) + 1;
    copy = malloc(len * sizeof(wchar_t));
    if (copy)
        memcpy(copy, str, len * sizeof(wchar_t));

    return copy;
}
#endif

/* strtof - provide implementation if missing */
float strtof(const char *str, char **endptr)
{
    return (float)strtod(str, endptr);
}
