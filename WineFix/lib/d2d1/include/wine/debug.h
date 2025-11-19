/*
 * Wine debugging interface - Simplified for standalone builds
 *
 * This is a simplified version of Wine's debug.h that works with MinGW headers
 * For full Wine debug functionality, use the complete Wine build system
 */

#ifndef __WINE_WINE_DEBUG_H
#define __WINE_WINE_DEBUG_H

#include <stdarg.h>
#include <stdio.h>
#include <windows.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Debug channel structure */
struct __wine_debug_channel
{
    unsigned char flags;
    char name[15];
};

/* Debug classes */
enum __wine_debug_class
{
    __WINE_DBCL_FIXME,
    __WINE_DBCL_ERR,
    __WINE_DBCL_WARN,
    __WINE_DBCL_TRACE,
    __WINE_DBCL_INIT = 7
};

/* Simplified debug macros for standalone build */
#define WINE_DECLARE_DEBUG_CHANNEL(chan) \
    static struct __wine_debug_channel __wine_dbch_##chan = { 0, #chan }

#define WINE_DEFAULT_DEBUG_CHANNEL(chan) \
    static struct __wine_debug_channel __wine_dbch_##chan = { 0, #chan }; \
    static struct __wine_debug_channel * const __wine_dbch___default = &__wine_dbch_##chan

/* Stub implementations - print to stderr for now */
static inline int __wine_dbg_vprintf(const char *format, va_list args)
{
    return vfprintf(stderr, format, args);
}

static inline int __wine_dbg_printf(const char *format, ...)
{
    va_list args;
    int ret;
    va_start(args, format);
    ret = __wine_dbg_vprintf(format, args);
    va_end(args);
    return ret;
}

static inline const char* wine_dbg_sprintf(const char *format, ...)
{
    static char buffer[512];
    va_list args;
    va_start(args, format);
    vsnprintf(buffer, sizeof(buffer), format, args);
    va_end(args);
    return buffer;
}

static inline const char* wine_dbgstr_a(const char *str)
{
    if (!str) return "(null)";
    return wine_dbg_sprintf("\"%s\"", str);
}

static inline const char* wine_dbgstr_w(const WCHAR *str)
{
    if (!str) return "(null)";
    static char buffer[512];
    WideCharToMultiByte(CP_UTF8, 0, str, -1, buffer, sizeof(buffer), NULL, NULL);
    return wine_dbg_sprintf("\"%s\"", buffer);
}

static inline const char* wine_dbgstr_guid(const GUID *guid)
{
    if (!guid) return "(null)";
    return wine_dbg_sprintf("{%08lx-%04x-%04x-%02x%02x-%02x%02x%02x%02x%02x%02x}",
        guid->Data1, guid->Data2, guid->Data3,
        guid->Data4[0], guid->Data4[1], guid->Data4[2], guid->Data4[3],
        guid->Data4[4], guid->Data4[5], guid->Data4[6], guid->Data4[7]);
}

static inline const char* wine_dbgstr_longlong(LONGLONG ll)
{
    return wine_dbg_sprintf("%lld", (long long)ll);
}

static inline const char* debugstr_wn(const WCHAR *str, int n)
{
    if (!str) return "(null)";
    static char buffer[512];
    int len = n < 0 ? -1 : n;
    if (len > 255) len = 255;
    WideCharToMultiByte(CP_UTF8, 0, str, len, buffer, sizeof(buffer), NULL, NULL);
    return wine_dbg_sprintf("\"%s\"", buffer);
}

static inline const char* wine_dbgstr_rect(const RECT *rect)
{
    if (!rect) return "(null)";
    return wine_dbg_sprintf("(%ld,%ld)-(%ld,%ld)",
        rect->left, rect->top, rect->right, rect->bottom);
}

static inline const char* debugstr_a(const char *str) { return wine_dbgstr_a(str); }
static inline const char* debugstr_w(const WCHAR *str) { return wine_dbgstr_w(str); }
static inline const char* debugstr_guid(const GUID *guid) { return wine_dbgstr_guid(guid); }

/* Debug output control - simplified stubs */
static inline unsigned char __wine_dbg_get_channel_flags(struct __wine_debug_channel *channel)
{
    return 0; /* Debugging disabled by default in standalone builds */
}

static inline int wine_dbg_log(enum __wine_debug_class cls,
                               struct __wine_debug_channel *channel,
                               const char *func, const char *format, ...)
{
    /* Simplified: only print errors and fixmes */
    if (cls == __WINE_DBCL_ERR || cls == __WINE_DBCL_FIXME)
    {
        const char *class_name = (cls == __WINE_DBCL_ERR) ? "err" : "fixme";
        fprintf(stderr, "%s:%s:%s ", class_name, channel->name, func);

        va_list args;
        va_start(args, format);
        vfprintf(stderr, format, args);
        va_end(args);
    }
    return 0;
}

/* Debug macros */
#ifndef WINE_NO_TRACE_MSGS
#define __WINE_GET_DEBUGGING_TRACE(dbch) 0
#else
#define __WINE_GET_DEBUGGING_TRACE(dbch) 0
#endif

#ifndef WINE_NO_DEBUG_MSGS
#define __WINE_GET_DEBUGGING_WARN(dbch)  0
#define __WINE_GET_DEBUGGING_FIXME(dbch) 1
#else
#define __WINE_GET_DEBUGGING_WARN(dbch)  0
#define __WINE_GET_DEBUGGING_FIXME(dbch) 0
#endif

#define __WINE_GET_DEBUGGING_ERR(dbch)  1

#define __WINE_GET_DEBUGGING(dbcl,dbch)  __WINE_GET_DEBUGGING##dbcl(dbch)

#define __WINE_IS_DEBUG_ON(dbcl,dbch) \
    (__WINE_GET_DEBUGGING##dbcl(dbch))

#define __WINE_DPRINTF(dbcl,dbch) \
    do { if(__WINE_GET_DEBUGGING(dbcl,(dbch))) { \
       struct __wine_debug_channel * const __dbch = (dbch); \
       const enum __wine_debug_class __dbcl = __WINE_DBCL##dbcl; \
       __WINE_DBG_LOG

#define __WINE_DBG_LOG(...) \
   wine_dbg_log( __dbcl, __dbch, __func__, __VA_ARGS__); } } while(0)

#define __WINE_PRINTF_ATTR(fmt,args)

#ifdef WINE_NO_TRACE_MSGS
#define WINE_TRACE(...) do { } while(0)
#define WINE_TRACE_(ch) WINE_TRACE
#endif

#ifdef WINE_NO_DEBUG_MSGS
#define WINE_WARN(...) do { } while(0)
#define WINE_WARN_(ch) WINE_WARN
#define WINE_FIXME(...) do { } while(0)
#define WINE_FIXME_(ch) WINE_FIXME
#endif

#ifndef WINE_TRACE
#define WINE_TRACE __WINE_DPRINTF(_TRACE,__wine_dbch___default)
#define WINE_TRACE_(ch) __WINE_DPRINTF(_TRACE,&__wine_dbch_##ch)
#endif

#ifndef WINE_WARN
#define WINE_WARN __WINE_DPRINTF(_WARN,__wine_dbch___default)
#define WINE_WARN_(ch) __WINE_DPRINTF(_WARN,&__wine_dbch_##ch)
#endif

#ifndef WINE_FIXME
#define WINE_FIXME __WINE_DPRINTF(_FIXME,__wine_dbch___default)
#define WINE_FIXME_(ch) __WINE_DPRINTF(_FIXME,&__wine_dbch_##ch)
#endif

#define WINE_ERR __WINE_DPRINTF(_ERR,__wine_dbch___default)
#define WINE_ERR_(ch) __WINE_DPRINTF(_ERR,&__wine_dbch_##ch)

/* Shorthand macros */
#define TRACE WINE_TRACE
#define TRACE_(ch) WINE_TRACE_(ch)
#define WARN WINE_WARN
#define WARN_(ch) WINE_WARN_(ch)
#define FIXME WINE_FIXME
#define FIXME_(ch) WINE_FIXME_(ch)
#define ERR WINE_ERR
#define ERR_(ch) WINE_ERR_(ch)

#ifdef __cplusplus
}
#endif

#endif /* __WINE_WINE_DEBUG_H */
