# D2D1.DLL Standalone Makefile
# Supports: x86_64-unix, x86_64-windows, i386-windows, and SysWoW64 mode

.PHONY: all clean help x86_64-unix x86_64-windows i386-windows syswow64

# Build target (can be overridden: make TARGET=x86_64-windows)
TARGET ?= x86_64-unix

# Optimization level (can be overridden: make OPT=-O0)
OPT ?= -O3

# Directories
SRCDIR := src
INCDIR := include
BUILDDIR := build
OUTDIR := $(BUILDDIR)/$(TARGET)

# Source files
ALL_SOURCES := $(wildcard $(SRCDIR)/*.c)
# For Unix builds, exclude guids.c (GUIDs are defined in factory.c via D2D1_INIT_GUID)
SOURCES := $(ALL_SOURCES)
OBJECTS := $(patsubst $(SRCDIR)/%.c,$(OUTDIR)/%.o,$(SOURCES))
SPEC_FILE := $(SRCDIR)/d2d1.spec
RC_FILE := $(SRCDIR)/version.rc

# IDL files that need to be processed
IDL_FILES := $(wildcard $(INCDIR)/windows/*.idl)
IDL_HEADERS := $(patsubst $(INCDIR)/windows/%.idl,$(OUTDIR)/include/%.h,$(IDL_FILES))

# Common flags
CFLAGS := -I$(INCDIR) -I$(INCDIR)/windows -I$(OUTDIR)/include -I$(SRCDIR)
CFLAGS += -Wall $(OPT) -D__WINE__ -D_REENTRANT -DWINE_UNICODE_NATIVE
LDFLAGS :=

# Target-specific configuration
ifeq ($(TARGET),x86_64-unix)
    CC := winegcc
    WIDL := widl
    WINEBUILD := winebuild
    WINEGCC := winegcc
    CFLAGS += -m64 -D__x86_64__ -D__unix__
    LDFLAGS += -m64
    DLL_EXT := .dll.so
    BUILD_MODE := unix
    ARCH := x86_64
    LIBS := -ld3d11 -ldxgi -ld3d10_1 -ld3dcompiler -ldwrite -lxmllite -ldxguid -lgdi32 -luser32 -ladvapi32 -lole32 -luuid
    # Exclude guids.c for Unix builds (GUIDs defined in factory.c with D2D1_INIT_GUID)
    SOURCES := $(filter-out $(SRCDIR)/guids.c,$(ALL_SOURCES))
    OBJECTS := $(patsubst $(SRCDIR)/%.c,$(OUTDIR)/%.o,$(SOURCES))
endif

ifeq ($(TARGET),x86_64-windows)
    CC := x86_64-w64-mingw32-gcc
    WIDL := widl
    WINEBUILD := winebuild
    CFLAGS += -m64 -D__x86_64__
    LDFLAGS += -m64 -shared
    DLL_EXT := .dll
    BUILD_MODE := windows
    ARCH := x86_64
    # Try to use Wine's d3d10_1 if available for x86_64
    ifeq ($(wildcard /usr/lib/wine/x86_64-windows/libd3d10_1.a),)
        D3D10_LIB := -ld3d10
    else
        D3D10_LIB := -L/usr/lib/wine/x86_64-windows -ld3d10_1
    endif
    LIBS := -ld3d11 -ldxgi $(D3D10_LIB) -ld3dcompiler -ldwrite -lgdi32 -luser32 -ladvapi32 -lole32 -lxmllite -luuid -lmsvcrt
endif

ifeq ($(TARGET),i386-windows)
    CC := i686-w64-mingw32-gcc
    WIDL := widl
    WINEBUILD := winebuild
    CFLAGS += -m32 -D__i386__
    LDFLAGS += -m32 -shared
    DLL_EXT := .dll
    BUILD_MODE := windows
    ARCH := i386
    # Try to use Wine's d3d10_1 if available for i386
    ifeq ($(wildcard /usr/lib/wine/i386-windows/libd3d10_1.a),)
        D3D10_LIB := -ld3d10
    else
        D3D10_LIB := -L/usr/lib/wine/i386-windows -ld3d10_1
    endif
    LIBS := -ld3d11 -ldxgi $(D3D10_LIB) -ld3dcompiler -ldwrite -lgdi32 -luser32 -ladvapi32 -lole32 -lxmllite -luuid -lmsvcrt
endif

ifeq ($(TARGET),syswow64)
    CC := i686-w64-mingw32-gcc
    WIDL := widl
    WINEBUILD := winebuild
    CFLAGS += -m32 -D__i386__ -D__WINE_SYSWOW64__
    LDFLAGS += -m32 -shared
    DLL_EXT := .dll
    BUILD_MODE := syswow64
    ARCH := i386
    # Try to use Wine's d3d10_1 if available for syswow64
    ifeq ($(wildcard /usr/lib/wine/i386-windows/libd3d10_1.a),)
        D3D10_LIB := -ld3d10
    else
        D3D10_LIB := -L/usr/lib/wine/i386-windows -ld3d10_1
    endif
    LIBS := -ld3d11 -ldxgi $(D3D10_LIB) -ld3dcompiler -ldwrite -lgdi32 -luser32 -ladvapi32 -lole32 -lxmllite -luuid -lmsvcrt
endif

# Output DLL
OUTPUT_DLL := $(OUTDIR)/d2d1$(DLL_EXT)

# Default target
all: $(TARGET)

# Help target
help:
	@echo "D2D1.DLL Standalone Build System"
	@echo ""
	@echo "Available targets:"
	@echo "  make x86_64-unix      - Build for x86_64 Unix (Wine PE)"
	@echo "  make x86_64-windows   - Build for x86_64 Windows"
	@echo "  make i386-windows     - Build for i386 Windows"
	@echo "  make syswow64         - Build for SysWoW64 mode"
	@echo "  make clean            - Clean build artifacts"
	@echo ""
	@echo "Or set TARGET variable:"
	@echo "  make TARGET=x86_64-windows"

# Build targets
x86_64-unix:
	@$(MAKE) TARGET=x86_64-unix build

x86_64-windows:
	@$(MAKE) TARGET=x86_64-windows build

i386-windows:
	@$(MAKE) TARGET=i386-windows build

syswow64:
	@$(MAKE) TARGET=syswow64 build

# Main build rule
build: $(OUTPUT_DLL)
	@echo "Build complete: $(OUTPUT_DLL)"

# Create output directories
$(OUTDIR):
	@mkdir -p $(OUTDIR)
	@mkdir -p $(OUTDIR)/include

# Process IDL files
$(OUTDIR)/include/%.h: $(INCDIR)/windows/%.idl | $(OUTDIR)
	@echo "IDL: $<"
	@$(WIDL) -I$(INCDIR)/windows -h -o $@ $<

# Compile C files
$(OUTDIR)/%.o: $(SRCDIR)/%.c $(IDL_HEADERS) | $(OUTDIR)
	@echo "CC: $<"
	@$(CC) $(CFLAGS) -c -o $@ $<

# Compile resource file
$(OUTDIR)/version.o: $(RC_FILE) | $(OUTDIR)
	@echo "RC: $<"
ifeq ($(ARCH),i386)
	@i686-w64-mingw32-windres -i $< -o $@
else
	@x86_64-w64-mingw32-windres -i $< -o $@
endif

# Link DLL (Windows mode)
ifeq ($(BUILD_MODE),windows)
$(OUTPUT_DLL): $(OBJECTS) $(OUTDIR)/version.o | $(OUTDIR)
	@echo "LINK: $@"
	@$(CC) $(LDFLAGS) -o $@ $(OBJECTS) $(OUTDIR)/version.o $(LIBS) \
		-Wl,--out-implib,$(OUTDIR)/libd2d1.a
endif

# Link DLL (Unix mode - requires winegcc)
ifeq ($(BUILD_MODE),unix)
$(OUTPUT_DLL): $(OBJECTS) $(SPEC_FILE) | $(OUTDIR)
	@echo "LINK (Unix PE): $@"
	@$(WINEGCC) -m64 -shared $(SPEC_FILE) $(OBJECTS) -o $@ $(LIBS)
endif

# Link DLL (SysWoW64 mode)
ifeq ($(BUILD_MODE),syswow64)
$(OUTPUT_DLL): $(OBJECTS) $(OUTDIR)/version.o | $(OUTDIR)
	@echo "LINK (SysWoW64): $@"
	@$(CC) $(LDFLAGS) -o $@ $(OBJECTS) $(OUTDIR)/version.o $(LIBS) \
		-Wl,--out-implib,$(OUTDIR)/libd2d1.a
endif

# Clean
clean:
	@echo "Cleaning build artifacts..."
	@rm -rf $(BUILDDIR)

# Show configuration
config:
	@echo "Build Configuration:"
	@echo "  TARGET: $(TARGET)"
	@echo "  CC: $(CC)"
	@echo "  WIDL: $(WIDL)"
	@echo "  BUILD_MODE: $(BUILD_MODE)"
	@echo "  ARCH: $(ARCH)"
	@echo "  OUTPUT: $(OUTPUT_DLL)"
