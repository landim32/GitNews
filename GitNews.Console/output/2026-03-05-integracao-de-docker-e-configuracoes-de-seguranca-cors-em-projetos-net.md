---
title: "Integração de Docker e Configurações de Segurança CORS em Projetos .NET"
date: 2026-03-05T22:29:21Z
author: "Rodrigo Landim"
category: "DevOps"
tags:
  - "Docker"
  - "CORS"
  - ".NET"
slug: "integracao-de-docker-e-configuracoes-de-seguranca-cors-em-projetos-net"
---

# Integração de Docker e Configurações de Segurança CORS em Projetos .NET

Neste artigo, vamos explorar algumas novidades técnicas implementadas recentemente em um projeto do GitHub de Rodrigo Landim, chamado `TesteINDT`. As principais melhorias técnicas que encontramos envolvem a integração do Docker e a configuração de segurança CORS.

## Integração do Docker

A integração do Docker foi realizada através do arquivo `.dockerignore` adicionado no commit `13e32c2`. Este arquivo é usado para ignorar arquivos e pastas que não devem ser incluídos no contexto do Docker. Veja o exemplo de código abaixo:

diff
+**/.classpath
+**/.dockerignore
+**/.env
+**/.git
+**/.gitignore
+**/.project
+**/.settings
+**/.toolstarget
+**/.vs
+**/.vscode
+**/*.*proj.user
+**/*.dbmdl
+**/*.jfm
+**/azds.yaml
+**/bin
+**/charts
+**/docker-compose*
+**/Dockerfile*
+**/node_modules
+**/npm-debug.log
+**/obj
+*


## Configuração de segurança CORS

A configuração de segurança CORS foi realizada no arquivo `CorsConfiguration.cs` adicionado no commit `13e32c2`. CORS (Cross-Origin Resource Sharing) é um mecanismo que permite que muitos recursos (por exemplo, fontes, JavaScript, etc.) em uma página da web sejam solicitados de outro domínio fora do domínio da qual a fonte originária foi servida.

diff
+namespace ContratacaoService.Api.Configuration;
+
+public static class CorsConfiguration
+{
+    private const string PolicyName = "CorsPolicy";
+
+    public static IServiceCollection AddCorsConfiguration(this IServiceCollection services)
+    {
+        services.AddCors(options =


Essas melhorias técnicas ajudam a melhorar a segurança e a escalabilidade do projeto, tornando-o mais robusto e confiável.
