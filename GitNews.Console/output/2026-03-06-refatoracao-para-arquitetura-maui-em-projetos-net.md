---
title: "Refatoração para arquitetura MAUI em projetos .NET"
date: 2026-03-06T13:10:26Z
author: "Rodrigo Landim"
category: "Arquitetura"
tags:
  - "MAUI"
  - "Refatoração"
  - "Arquitetura"
slug: "refatoracao-para-arquitetura-maui-em-projetos-net"
---

# Introdução
No projeto VoiceNotesAI, uma aplicação de notas de voz com IA, foram feitas alterações significativas na arquitetura do software, migrando para uma estrutura MAUI do .NET. Este artigo discute os detalhes da refatoração.

# Mudança para a arquitetura MAUI
A arquitetura MAUI (Multi-platform App UI) é um framework moderno de interface de usuário que permite a criação de aplicativos para diferentes plataformas usando um único projeto .NET.

diff
+using Microsoft.Extensions.DependencyInjection;
+using VoiceNotesAI.AppServices;
+using VoiceNotesAI.Context;
+using VoiceNotesAI.Repository;
+using VoiceNotesAI.Services;
+using VoiceNotesAI.Services.Interfaces;
+
+namespace VoiceNotesAI;
+
+public static class DependencyInjection


No exemplo de código acima, a implementação de injeção de dependência está sendo usada para registrar serviços e repositórios. Isso facilita a substituição de implementações e melhora a capacidade de teste.

# Nova estrutura de projeto

O projeto foi reestruturado em quatro partes:
- VoiceNotesAI.Domain: Contém os modelos (Note, Category, Comment, etc.) e Helpers.
- VoiceNotesAI.Infra.Interfaces: Contém as interfaces de serviço (IAIService, INoteRepository, etc.).
- VoiceNotesAI.Infra: Contém as implementações de serviço, repositórios e AppDatabase.
- VoiceNotesAI (MAUI): A camada de UI com Páginas, ViewModels, Conversores, AudioService.

Esta estrutura de projeto permite um desenvolvimento mais modular e uma separação clara de responsabilidades.

# Pipeline de documentação

Para manter a documentação atualizada, foi adicionado um pipeline para gerar a documentação da API automaticamente.

diff
+name: Generate API Docs
+
+on:
+  push:
+    branches: [main]
+    paths:
+      - 'VoiceNotesAI.Domain/**'
+      - 'VoiceNotesAI.Infra/**'
+      - 'VoiceNotesAI.Infra.Interfaces/**'
+  workflow_dispatch:
+
+concurrency:
+  group: wiki
+  cancel-in-progress: true


O trecho de código acima é uma parte do pipeline que é ativado sempre que um push é feito para a branch principal e gera a documentação da API automaticamente.

# Conclusão

A refatoração para uma arquitetura MAUI e uma estrutura de projeto mais modular permite um desenvolvimento mais eficiente e uma manutenção mais fácil. Além disso, a adição de um pipeline de documentação garante que a documentação esteja sempre atualizada.
