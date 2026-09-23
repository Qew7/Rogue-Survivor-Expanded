FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
RUN apt-get update && apt-get install -y --no-install-recommends mono-devel ruby ruby-rexml \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src/WRogue
COPY WRogue/ ./
COPY docker/build.rb /build.rb
COPY docker/prepare.rb /prepare.rb
COPY docker/csc /usr/local/bin/csc
RUN chmod +x /usr/local/bin/csc && ruby /build.rb \
    && MONO_IOMAP=all xbuild RogueSurvivor.Linux.csproj /p:Configuration=Release \
       /p:CscToolPath=/usr/local/bin /p:CscToolExe=csc /verbosity:minimal

FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends \
    mono-runtime libmono-system-windows-forms4.0-cil \
    libmono-system-runtime-serialization-formatters-soap4.0-cil libgdiplus \
    xvfb x11vnc x11-utils openbox novnc websockify fonts-dejavu-core \
    curl tini \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --uid 1000 player
WORKDIR /opt/game
COPY --from=build /src/WRogue/bin/Release/ ./
COPY docker/entrypoint.sh /usr/local/bin/start-game
COPY docker/index.html /usr/share/novnc/index.html
RUN chmod +x /usr/local/bin/start-game \
    && mkdir -p /opt/game/Config && chown player:player /opt/game/Config
ENV DISPLAY=:99 MONO_IOMAP=all
USER player
EXPOSE 6080
HEALTHCHECK --interval=15s --timeout=3s --start-period=30s \
    CMD curl --fail --silent http://localhost:6080/ >/dev/null || exit 1
ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/start-game"]
