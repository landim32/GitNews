---
title: "Implementação de um Sistema de Monitoramento Orientado a Eventos com .NET 8 e RabbitMQ"
date: 2026-03-05T22:28:46Z
author: "Rodrigo Landim"
category: "Arquitetura"
tags:
  - "Docker"
  - "RabbitMQ"
  - ".NET 8"
  - "GitHub Actions"
slug: "implementacao-de-um-sistema-de-monitoramento-orientado-a-eventos-com-net-8-e-rabbitmq"
---

# Implementação de um Sistema de Monitoramento Orientado a Eventos com .NET 8 e RabbitMQ

O repositório mqMonitor teve recentemente várias adições e modificações que trouxeram novos recursos e melhorias. Vamos analisar os principais tópicos técnicos envolvidos.

## Configuração do Docker Compose

Um dos aspectos mais notáveis é a adição de um arquivo `docker-compose.yml`. Este arquivo define um ambiente Docker com vários serviços, incluindo RabbitMQ e PostgreSQL, além dos serviços do aplicativo Monitor API e Worker. As portas e as credenciais de acesso para o RabbitMQ são parametrizadas através de variáveis de ambiente definidas em um arquivo `.env`.

Veja um exemplo do arquivo `docker-compose.yml`:

diff
@@ -0,0 +1,74 @@
+version: '3.8'
+
+services:
+  rabbitmq:
+    image: rabbitmq:3-management
+    container_name: mqmonitor-rabbitmq
+    ports:
+      - "${RABBITMQ_PORT}:5672"
+      - "${RABBITMQ_MANAGEMENT_PORT}:15672"
+    environment:
+      RABBITMQ_DEFAULT_USER: ${RABBITMQ_DEFAULT_USER}
+    ...


## Criação de Workflows GitHub Actions

Outra novidade é a adição de workflows do GitHub Actions para criação de versão e de release. Esses workflows automatizam o processo de versionamento e liberação do software, o que é essencial em um ambiente de desenvolvimento contínuo.

Veja um exemplo do arquivo de workflow `create-release.yml`:

diff
@@ -0,0 +1,115 @@
+name: Create Release
+
+on:
+  workflow_run:
+    workflows: ["Version and Tag"]
+    types:
+      - completed
+
+jobs:
+  create-release:
+    runs-on: ubuntu-latest
+    if: ${{ github.event.workflow_run.conclusion == 'success' }}
+    permissions:
+      contents: write
+    ...


## Adição de Consumers

Os arquivos `CancelCommandConsumer.cs` e `CompensationConsumer.cs` foram adicionados no projeto. Estes são consumers que lidam com comandos de cancelamento e compensação, respectivamente. Além disso, esses consumers foram registrados como serviços hospedados no arquivo `Program.cs`.

Aqui está um exemplo de como os consumers foram registrados:

diff
@@ -14,6 +14,8 @@
 // Register API-specific hosted services
 builder.Services.AddHostedService<ProcessEventConsumer>();
 builder.Services.AddHostedService<QueueStatsBackgroundService>();
+builder.Services.AddHostedService<CancelCommandConsumer>();
+builder.Services.AddHostedService<CompensationConsumer>();


## Conclusão

As mudanças recentes no repositório mqMonitor demonstram a implementação de um sistema de monitoramento orientado a eventos utilizando .NET 8 e RabbitMQ. A adição de Docker Compose e GitHub Actions workflows também mostra como a infraestrutura e os processos de desenvolvimento do projeto estão automatizados.
