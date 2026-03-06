---
title: "Refatoração da arquitetura e implementação de assistente AI no FamilyFinance"
date: 2026-03-05T22:26:23Z
author: "Rodrigo Landim"
category: "Arquitetura"
tags:
  - "MVVM"
  - "AI"
  - "Refatoração"
slug: "refatoracao-da-arquitetura-e-implementacao-de-assistente-ai-no-familyfinance"
---

No repositório FamilyFinance, podemos identificar várias novidades técnicas em commits recentes, incluindo uma significativa refatoração da arquitetura e a adição de um assistente AI. 

## Refatoração de Arquitetura

Uma importante mudança arquitetural identificada no commit `d14f126` é a remoção e adição de alguns arquivos de habilidades do Claude, com destaque para a adição do arquivo `maui-architecture`. Essa mudança sugere uma nova orientação arquitetural para a aplicação, seguindo o padrão MVVM (Model-View-ViewModel) e utilizando a tecnologia .NET MAUI.

diff
@@ -0,0 +1,539 @@
+---
+name: maui-architecture
+description: Guides the implementation of a new entity following the MVVM + layered architecture for .NET MAUI apps. Covers all layers from SQLite Model to Page, including Repository pattern, Rich Domain entities, Domain Services, AppServices, DTOs wi


Além disso, o arquivo `FamilyFinance.Application/DependencyInjection.cs` foi adicionado, demonstrando a implementação de injeção de dependência, um padrão de design amplamente utilizado para maior modularidade e testabilidade.

diff
@@ -0,0 +1,14 @@
+using Microsoft.Extensions.DependencyInjection;
+
+namespace FamilyFinance.AppConfiguration;
+
+public static class DependencyInjection
+{
+    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
+    {
+        // Domain Services
+        // s


## Assistente AI

No commit `58542b0`, foi integrado um assistente AI ao projeto. Este assistente, aparentemente com base na API OpenAI, foi projetado para entender prompts de linguagem natural e automaticamente criar registros na base de dados.

diff
+    <FlyoutItem Title="AI Assistant" Icon="ai_icon.png">
+        <ShellContent ContentTemplate="{DataTemplate views:AiPage}" Route="AiPage" />
+    </FlyoutItem>


No mesmo commit, observamos também a adição de permissões para gravação de áudio, possivelmente para permitir interações por voz com o assistente AI.

## Conclusão

Em resumo, as principais novidades técnicas no projeto FamilyFinance são a refatoração da arquitetura e a implementação de um assistente AI. Estas mudanças demonstram um esforço para melhorar a modularidade e testabilidade do aplicativo, além de melhorar a experiência do usuário com interações mais naturais.
