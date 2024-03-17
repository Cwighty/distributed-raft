FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS base 
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global --no-cache dotnet-subset --version 0.3.2

FROM base as restore
WORKDIR /src
COPY . .
RUN dotnet subset restore Raft.sln --root-directory /src --output /src/restored

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS development
COPY --from=restore /src/restored .
RUN dotnet restore

COPY . /src
WORKDIR /src/
