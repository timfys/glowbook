FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/GlowBook.Web/GlowBook.Web.csproj", "src/GlowBook.Web/"]
RUN dotnet restore "src/GlowBook.Web/GlowBook.Web.csproj"
COPY . .
WORKDIR /src/src/GlowBook.Web
RUN dotnet publish "GlowBook.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/Data
ENTRYPOINT ["dotnet", "GlowBook.Web.dll"]
