cmake_minimum_required(VERSION 3.16)
project(d2d1 VERSION 10.18 LANGUAGES C)

# Options for build configuration
option(BUILD_FOR_UNIX "Build Unix PE format (requires winegcc)" OFF)
option(BUILD_SYSWOW64 "Build for SysWoW64 mode" OFF)
set(TARGET_ARCH "x86_64" CACHE STRING "Target architecture (x86_64 or i386)")

# Set compiler based on architecture
if(TARGET_ARCH STREQUAL "i386")
    set(CMAKE_C_COMPILER i686-w64-mingw32-gcc)
    set(CMAKE_RC_COMPILER i686-w64-mingw32-windres)
    set(ARCH_FLAGS -m32 -D__i386__)
else()
    set(CMAKE_C_COMPILER x86_64-w64-mingw32-gcc)
    set(CMAKE_RC_COMPILER x86_64-w64-mingw32-windres)
    set(ARCH_FLAGS -m64 -D__x86_64__)
endif()

# Source files
set(D2D1_SOURCES
    src/bitmap.c
    src/bitmap_render_target.c
    src/brush.c
    src/command_list.c
    src/dc_render_target.c
    src/device.c
    src/effect.c
    src/factory.c
    src/geometry.c
    src/hwnd_render_target.c
    src/layer.c
    src/mesh.c
    src/state_block.c
    src/stroke.c
    src/wic_render_target.c
)

# IDL files to process
file(GLOB IDL_FILES "include/windows/*.idl")

# Create custom command to process IDL files
set(IDL_HEADERS "")
foreach(IDL_FILE ${IDL_FILES})
    get_filename_component(IDL_NAME ${IDL_FILE} NAME_WE)
    set(IDL_HEADER "${CMAKE_BINARY_DIR}/include/${IDL_NAME}.h")
    add_custom_command(
        OUTPUT ${IDL_HEADER}
        COMMAND widl -I${CMAKE_SOURCE_DIR}/include/windows -h -o ${IDL_HEADER} ${IDL_FILE}
        DEPENDS ${IDL_FILE}
        COMMENT "Processing IDL: ${IDL_NAME}.idl"
    )
    list(APPEND IDL_HEADERS ${IDL_HEADER})
endforeach()

# Create a custom target for IDL headers
add_custom_target(idl_headers DEPENDS ${IDL_HEADERS})

# Create the DLL
add_library(d2d1 SHARED ${D2D1_SOURCES})

# Make sure IDL headers are generated before compiling
add_dependencies(d2d1 idl_headers)

# Include directories
target_include_directories(d2d1 PRIVATE
    ${CMAKE_SOURCE_DIR}/include/wine
    ${CMAKE_SOURCE_DIR}/include/windows
    ${CMAKE_BINARY_DIR}/include
    ${CMAKE_SOURCE_DIR}/src
)

# Compiler definitions
target_compile_definitions(d2d1 PRIVATE
    __WINE__
    _REENTRANT
    WINE_UNICODE_NATIVE
    $<$<BOOL:${BUILD_FOR_UNIX}>:__unix__>
    $<$<BOOL:${BUILD_SYSWOW64}>:__WINE_SYSWOW64__>
)

# Compiler flags
target_compile_options(d2d1 PRIVATE
    ${ARCH_FLAGS}
    -Wall
    -O2
)

# Linker flags
target_link_options(d2d1 PRIVATE
    ${ARCH_FLAGS}
    $<$<NOT:$<BOOL:${BUILD_FOR_UNIX}>>:-shared>
)

# Link libraries
target_link_libraries(d2d1 PRIVATE
    d3d11
    dxgi
    d3d10_1
    d3dcompiler
    dwrite
    gdi32
    user32
    advapi32
    ole32
    xmllite
    uuid
)

# Set output name
set_target_properties(d2d1 PROPERTIES
    OUTPUT_NAME "d2d1"
    PREFIX ""
    SUFFIX ".dll"
)

# Resource file handling
if(EXISTS "${CMAKE_SOURCE_DIR}/src/version.rc")
    target_sources(d2d1 PRIVATE src/version.rc)
endif()

# Install rules
install(TARGETS d2d1
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)

# Print configuration
message(STATUS "D2D1 Build Configuration:")
message(STATUS "  Architecture: ${TARGET_ARCH}")
message(STATUS "  Build for Unix PE: ${BUILD_FOR_UNIX}")
message(STATUS "  SysWoW64 mode: ${BUILD_SYSWOW64}")
message(STATUS "  Compiler: ${CMAKE_C_COMPILER}")
