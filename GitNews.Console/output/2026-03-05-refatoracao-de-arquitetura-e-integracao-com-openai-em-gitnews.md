---
title: "Refatoração de Arquitetura e Integração com OpenAI em GitNews"
date: 2026-03-05T22:26:47Z
author: "Rodrigo Landim"
category: "Arquitetura"
tags:
  - "Clean Architecture"
  - "Pacotes"
  - "OpenAI"
  - "Refatoração"
slug: "refatoracao-de-arquitetura-e-integracao-com-openai-em-gitnews"
---

# Refatoração de Arquitetura e Integração com OpenAI em GitNews

Este artigo explora algumas das mudanças técnicas recentes implementadas no projeto GitNews. As principais alterações destacadas são a refatoração de arquitetura, a adição de novos pacotes e bibliotecas e a integração com a API do OpenAI para geração de conteúdo.

## Refatoração de Arquitetura

No commit '287cd66', vemos uma grande refatoração na arquitetura do projeto. A alteração mais notável é a implementação da Clean Architecture, que é evidente nos guias de habilidade (skills) que foram adicionados, como visto nos arquivos '.claude/skills/dotnet-arch-entity/SKILL.md' e '.claude/skills/dotnet-fluent-validation/SKILL.md'.

Aqui está um exemplo desse código:

diff
+name: dotnet-arch-entity
+description: Guides the implementation of a new entity following Clean Architecture in a .NET 8 project.


Além disso, também podemos ver a aplicação do padrão de Repositório, que é um padrão de design que permite que os dados sejam acessados de maneira consistente, independentemente de sua origem.

## Adição de Novos Pacotes

O projeto também viu a adição de novos pacotes, como evidenciado pela modificação do arquivo 'GitNews.Console.csproj' no commit '003c087'. Neste caso, 'Microsoft.Extensions.Configuration.Binder' foi adicionado como uma nova dependência.

diff
+<PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="8.0.2" />


Este pacote é uma biblioteca que fornece funcionalidades para ligar tipos de dados a configurações.

## Integração com OpenAI

Finalmente, um dos commits recentes, '9ce69bb', destaca a implementação de um gerador de blog que utiliza a API do OpenAI. A aplicação acessa a API do GitHub para coletar commits, READMEs e diffs de código, gera um prompt contextualizado e envia para a API do ChatGPT para criar artigos de blog.

diff
+using GitNews.Core.Extensions;
+using GitNews.Core.Interfaces;
+using GitNews.Core.Models;
+using Microsoft.Extensions.Configuration;
+using Microsoft.Extensions.DependencyInjection;

+namespace GitNews.Console;


Esta é uma abordagem interessante e inovadora para a geração automatizada de conteúdo de blog a partir de dados de repositório do GitHub.
