/*
 * Wine heap memory allocation wrappers
 *
 * Copyright 2006 Jacek Caban for CodeWeavers
 * Copyright 2013, 2018 Michael Stefaniuc
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA 02110-1301, USA
 */

#ifndef __WINE_WINE_HEAP_H
#define __WINE_WINE_HEAP_H

#include <windows.h>
#include <wchar.h>
#include <stdlib.h>

/* Compatibility: wcsdup vs _wcsdup handling */
#ifdef __unix__
/* On Unix/Linux, use standard wcsdup, map _wcsdup to it */
#ifndef _wcsdup
#define _wcsdup wcsdup
#endif
#else
/* On Windows, wcsdup might not be available, use _wcsdup */
#ifndef wcsdup
#define wcsdup _wcsdup
#endif
#endif

/* Common Wine utility macros */
#ifndef ARRAY_SIZE
#define ARRAY_SIZE(x) (sizeof(x) / sizeof((x)[0]))
#endif

#ifndef CONTAINING_RECORD
#define CONTAINING_RECORD(address, type, field) \
    ((type *)((char *)(address) - (unsigned long)(&((type *)0)->field)))
#endif

/* COM helper macros - simplified versions */
#ifndef IUnknown_QueryInterface
#define IUnknown_QueryInterface(p,a,b) (p)->lpVtbl->QueryInterface(p,a,b)
#endif

#ifndef IUnknown_AddRef
#define IUnknown_AddRef(p) (p)->lpVtbl->AddRef(p)
#endif

#ifndef IUnknown_Release
#define IUnknown_Release(p) (p)->lpVtbl->Release(p)
#endif

#ifndef IStream_Write
#define IStream_Write(p,a,b,c) (p)->lpVtbl->Write(p,a,b,c)
#endif

#ifndef IStream_Read
#define IStream_Read(p,a,b,c) (p)->lpVtbl->Read(p,a,b,c)
#endif

#ifndef IStream_Seek
#define IStream_Seek(p,a,b,c) (p)->lpVtbl->Seek(p,a,b,c)
#endif

#ifndef IStream_Release
#define IStream_Release(p) (p)->lpVtbl->Release(p)
#endif

/* GCC attribute macros */
#if defined(__GNUC__) || defined(__clang__)
#define __WINE_ALLOC_SIZE(...) __attribute__((alloc_size(__VA_ARGS__)))
#define __WINE_MALLOC __attribute__((malloc))
#else
#define __WINE_ALLOC_SIZE(...)
#define __WINE_MALLOC
#endif

static inline void * __WINE_ALLOC_SIZE(1) __WINE_MALLOC heap_alloc(SIZE_T len)
{
    return HeapAlloc(GetProcessHeap(), 0, len);
}

static inline void * __WINE_ALLOC_SIZE(1) __WINE_MALLOC heap_alloc_zero(SIZE_T len)
{
    return HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, len);
}

static inline void * __WINE_ALLOC_SIZE(2) heap_realloc(void *mem, SIZE_T len)
{
    if (!mem)
        return HeapAlloc(GetProcessHeap(), 0, len);
    return HeapReAlloc(GetProcessHeap(), 0, mem, len);
}

static inline void heap_free(void *mem)
{
    HeapFree(GetProcessHeap(), 0, mem);
}

static inline void * __WINE_ALLOC_SIZE(1,2) __WINE_MALLOC heap_calloc(SIZE_T count, SIZE_T size)
{
    SIZE_T len = count * size;

    if (size && len / size != count)
        return NULL;
    return HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, len);
}

#endif  /* __WINE_WINE_HEAP_H */
