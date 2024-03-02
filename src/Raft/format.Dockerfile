FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS development
COPY . /src
WORKDIR /src/
# cache nuget packages
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages
