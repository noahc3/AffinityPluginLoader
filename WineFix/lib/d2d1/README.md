# D2D1.DLL Standalone Build

This repository contains a standalone build system for `d2d1.dll` extracted from Wine 10.18 with changes applied for the WineFix plugin project. This standalone source is provided to allow building the Direct2D implementation without requiring the full Wine source.

This source includes modifications compared to upstream Wine:
- **geometry.c**: implement support for drawing cubic beziers using a subdivision algorithm to approximate cubic beziers using multiple quadratic beziers

The native Unix output is distributed with WineFix.

## Supported Targets

- **x86_64-unix**: 64-bit Unix PE format (for Wine)
- **x86_64-windows**: 64-bit native Windows
- **i386-windows**: 32-bit native Windows
- **syswow64**: 32-bit for SysWoW64 mode (64-bit Windows running 32-bit apps)

## License

Code extracted from Wine and all modifications to the code as described above are licensed under the GNU Lesser General Public License (LGPL) version 2.1 or later. A copy of the license is provided in the LICENSE file.

## Source

Original source of Wine 10.18: <https://gitlab.winehq.org/wine/wine/-/tree/wine-10.18?ref_type=tags>

The relevant files are under `dlls/d2d1/` in each repository.

## Build System Details

### Makefile Targets

- `all` - Build default target (x86_64-unix)
- `x86_64-unix` - Build 64-bit Unix PE
- `x86_64-windows` - Build 64-bit Windows
- `i386-windows` - Build 32-bit Windows
- `syswow64` - Build SysWoW64 mode
- `clean` - Clean all build artifacts
- `config` - Show current build configuration
- `help` - Show help message

### Environment Variables

You can customize the build by setting these variables:

```bash
# Use a different compiler
make CC=clang TARGET=x86_64-windows

# Add custom flags
make CFLAGS="-O3 -march=native" TARGET=x86_64-windows

# Use custom Wine tools
make WIDL=/custom/path/widl WINEGCC=/custom/path/winegcc TARGET=x86_64-unix
```
