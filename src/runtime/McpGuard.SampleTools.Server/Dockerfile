FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/runtime/McpGuard.SampleTools.Server/McpGuard.SampleTools.Server.csproj", "src/runtime/McpGuard.SampleTools.Server/"]
COPY ["Directory.Packages.props", "./"]

RUN dotnet restore "src/runtime/McpGuard.SampleTools.Server/McpGuard.SampleTools.Server.csproj"

COPY . .
RUN dotnet publish "src/runtime/McpGuard.SampleTools.Server/McpGuard.SampleTools.Server.csproj" \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "McpGuard.SampleTools.Server.dll"]