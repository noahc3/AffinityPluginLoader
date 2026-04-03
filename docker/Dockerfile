FROM ubuntu:24.04

ENV DEBIAN_FRONTEND=noninteractive \
    GIN_PATH=/gin \
    PATH="/gin/bin:${PATH}" \
    WINE_RUNNER=ElementalWarriorWine-7.9-amd64-wow64

RUN apt update && apt install -y \
    unzip wget git build-essential ca-certificates \
    mingw-w64 mingw-w64-tools dotnet-sdk-8.0 zip xz-utils \
    && apt clean && rm -rf /var/lib/apt/lists/*

# Install gin + Wine runner
RUN git clone https://github.com/noahc3/gin.git "$GIN_PATH" \
    && "$GIN_PATH/bin/gincfg" init \
    && "$GIN_PATH/bin/gincfg" meta link \
    && "$GIN_PATH/bin/gincfg" meta link-extra \
    && "$GIN_PATH/bin/gincfg" runner install --set $WINE_RUNNER \
       https://github.com/seapear/AffinityOnLinux/releases/download/Legacy/ElementalWarriorWine-x86_64.tar.gz

# Add Wine runner tools (winegcc, winebuild, widl) to PATH
ENV PATH="/gin/runners/${WINE_RUNNER}/bin:${PATH}"

# Set up mingw cross-compilation with Wine headers
RUN cp "$GIN_PATH/runners/$WINE_RUNNER/include/wine/windows/metahost.h" \
       /usr/x86_64-w64-mingw32/include/ \
    && cd /tmp \
    && cp "$GIN_PATH/runners/$WINE_RUNNER/lib/wine/x86_64-windows/mscoree.dll" . \
    && gendef mscoree.dll \
    && x86_64-w64-mingw32-dlltool -d mscoree.def -D ./mscoree.dll -l libmscoree.a \
    && cp libmscoree.a /usr/x86_64-w64-mingw32/lib/ \
    && rm -f mscoree.dll mscoree.def libmscoree.a

WORKDIR /src
