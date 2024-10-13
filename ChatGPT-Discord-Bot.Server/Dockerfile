# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS with-node
RUN apt-get update
RUN apt-get install curl
RUN curl -sL https://deb.nodesource.com/setup_20.17 | bash
RUN apt-get -y install nodejs


FROM with-node AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ChatGPT-Discord-Bot.Server/ChatGPT-Discord-Bot.Server.csproj", "ChatGPT-Discord-Bot.Server/"]
COPY ["chatgpt-discord-bot.client/chatgpt-discord-bot.client.esproj", "chatgpt-discord-bot.client/"]
RUN dotnet restore "./ChatGPT-Discord-Bot.Server/ChatGPT-Discord-Bot.Server.csproj"
COPY . .
WORKDIR "/src/ChatGPT-Discord-Bot.Server"
RUN dotnet build "./ChatGPT-Discord-Bot.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ChatGPT-Discord-Bot.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ChatGPT-Discord-Bot.Server.dll"]
