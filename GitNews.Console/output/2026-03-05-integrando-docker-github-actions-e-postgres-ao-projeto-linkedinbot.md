---
title: "Integrando Docker, GitHub Actions e Postgres ao Projeto LinkedinBot"
date: 2026-03-05T22:27:34Z
author: "Rodrigo Landim"
category: "DevOps"
tags:
  - "docker"
  - "github-actions"
  - "postgres"
  - "repository-pattern"
slug: "integrando-docker-github-actions-e-postgres-ao-projeto-linkedinbot"
---

# Integrando Docker, GitHub Actions e Postgres ao Projeto LinkedinBot

Ao analisar os commits recentes do projeto LinkedinBot, encontramos diversas novidades técnicas relevantes, incluindo a integração do Docker, GitHub Actions e a implementação de um repositório Postgres. Vamos explorar cada uma delas em detalhes.

## Integração com Docker

O projeto LinkedinBot adotou o Docker para criar um ambiente de desenvolvimento isolado e replicável. Através do Dockerfile, o projeto pode ser construído e executado em qualquer ambiente com o Docker instalado. Veja um trecho do Dockerfile adicionado:

dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT=LinkedinBot.Console
WORKDIR /src

# Copy solution and ALL project files (better layer caching)
COPY LinkedinBot.sln .
COPY LinkedinBot.DTO/LinkedinBot.DTO.csproj LinkedinBot.DTO/
COPY LinkedinBot.Do


Este trecho define o ambiente de construção usando a imagem do SDK .NET 8.0. Em seguida, ele define o diretório de trabalho e copia os arquivos do projeto para este diretório.

## Implementação de GitHub Actions

O projeto também adotou o GitHub Actions para automatizar o processo de criação de versão e lançamento. Aqui está um trecho do arquivo de workflow do GitHub Actions:

yaml
name: Version and Tag

on:
  push:
    branches:
      - main
  workflow_dispatch:

jobs:
  create-version-tag:
    runs-on: ubuntu-latest
    permissions:
      contents: write
    
    outputs:
      version: ${{ steps.gitversion.outputs.nuGetVersionV2 }}
      se


Este workflow é acionado quando há um push na branch main ou quando é disparado manualmente. Ele cria uma versão e uma tag para o commit atual.

## Implementação de Repositório Postgres

Finalmente, o projeto implementou um repositório Postgres. Esta é uma aplicação do padrão Repository, que abstrai os detalhes do acesso aos dados, tornando o código mais limpo e mais fácil de manter. Aqui está um trecho do código da implementação do repositório:

csharp
using LinkedinBot.Domain.Services;
using LinkedinBot.Domain.Services.Interfaces;
using LinkedinBot.DTO.Models;
using LinkedinBot.Infra.Data;
using LinkedinBot.Infra.Interfaces.Services;
using LinkedinBot.Infra.Repositories;
using LinkedinBot.Infra.Services;
using Microsoft.E


Este trecho mostra a importação de várias classes, incluindo classes de serviço e repositórios. Essas classes são usadas para implementar a lógica de negócios e o acesso aos dados, respectivamente.
