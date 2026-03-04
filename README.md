# GitNews

Gerador automático de artigos de blog a partir de repositórios GitHub. O programa acessa a API do GitHub, coleta commits, README e código alterado, e utiliza o ChatGPT para gerar artigos de blog completos em formato Markdown.

## Funcionalidades

- Acessa a API do GitHub via Octokit para coletar dados do repositório
- Busca o README.md do projeto
- Coleta os commits recentes com os diffs de código
- Gera um prompt contextualizado e envia para a API do ChatGPT
- O ChatGPT retorna um artigo com: título, conteúdo, categoria e tags
- Salva o artigo em formato Markdown com front matter YAML

## Estrutura do Projeto

```
GitNews/
├── GitNews.sln
└── src/
    ├── GitNews.Core/                    # Biblioteca principal
    │   ├── Interfaces/
    │   │   ├── IGitHubService.cs        # Contrato do serviço GitHub
    │   │   ├── IBlogGeneratorService.cs # Contrato do gerador de blog
    │   │   └── IMarkdownWriter.cs       # Contrato do gravador Markdown
    │   ├── Models/
    │   │   ├── BlogPost.cs              # Modelo do artigo de blog
    │   │   ├── CommitInfo.cs            # Modelo de commit e alterações
    │   │   ├── GitNewsSettings.cs       # Configurações da aplicação
    │   │   └── RepositoryContext.cs     # Contexto do repositório
    │   ├── Services/
    │   │   ├── GitHubService.cs         # Acesso à API do GitHub
    │   │   ├── BlogGeneratorService.cs  # Integração com ChatGPT
    │   │   └── MarkdownWriter.cs        # Gravação em Markdown
    │   └── Extensions/
    │       └── ServiceCollectionExtensions.cs
    └── GitNews.Console/                 # Aplicação console
        ├── Program.cs
        └── appsettings.json
```

## Pré-requisitos

- .NET 8.0 SDK
- Token de acesso do GitHub (com permissão de leitura de repositórios)
- API Key do OpenAI

## Configuração

### Via `appsettings.json`

Edite o arquivo `src/GitNews.Console/appsettings.json`:

```json
{
  "GitHub": {
    "Token": "ghp_seu_token_aqui",
    "Owner": "dono_do_repositorio",
    "Repository": "nome_do_repositorio",
    "MaxCommits": 30
  },
  "OpenAI": {
    "ApiKey": "sk-sua_chave_aqui",
    "Model": "gpt-4"
  },
  "Output": {
    "OutputDirectory": "./output"
  }
}
```

### Via variáveis de ambiente

```bash
export GITNEWS_GITHUB__TOKEN=ghp_seu_token_aqui
export GITNEWS_GITHUB__OWNER=dono_do_repositorio
export GITNEWS_GITHUB__REPOSITORY=nome_do_repositorio
export GITNEWS_OPENAI__APIKEY=sk-sua_chave_aqui
```

### Via linha de comando

```bash
dotnet run --project src/GitNews.Console -- \
  --owner microsoft \
  --repo vscode \
  --github-token ghp_xxx \
  --openai-key sk-xxx \
  --output ./meus-artigos
```

## Como executar

```bash
# Restaurar dependências
dotnet restore

# Compilar
dotnet build

# Executar
dotnet run --project src/GitNews.Console
```

## Saída

O artigo gerado é salvo como um arquivo `.md` no diretório de saída com o seguinte formato:

```markdown
---
title: "Título do Artigo"
date: 2026-03-04T12:00:00Z
author: "Nome do Autor"
category: "Categoria"
tags:
  - "tag1"
  - "tag2"
slug: "titulo-do-artigo"
---

Conteúdo do artigo em Markdown...
```

## Licença

MIT License - veja [LICENSE](LICENSE) para detalhes.
