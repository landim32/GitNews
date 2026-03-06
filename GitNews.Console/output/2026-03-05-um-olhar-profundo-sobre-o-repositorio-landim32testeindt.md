---
title: "Um olhar profundo sobre o repositório landim32/TesteINDT"
date: 2026-03-05T14:50:44Z
author: "Rodrigo Landim"
category: "Desenvolvimento de Software"
tags:
  - ".NET"
  - "Microservices"
  - "Arquitetura Hexagonal"
  - "DDD"
  - "SOLID"
  - "Clean Code"
slug: "um-olhar-profundo-sobre-o-repositorio-landim32testeindt"
---

# 🏥 Sistema de Propostas de Seguro: Um Projeto de Microserviços com .NET 8

O repositório landim32/TesteINDT é liderado por Rodrigo Landim e é um exemplo notável de uma implementação prática de microserviços usando .NET 8. O projeto é um sistema de gerenciamento de propostas de seguro, construído seguindo os princípios da Arquitetura Hexagonal (Ports & Adapters), DDD, SOLID e Clean Code.

## 🏗️ Arquitetura do sistema

O sistema é composto por dois microserviços independentes:

1. **PropostaService**: Gerencia o ciclo de vida das propostas de seguro, incluindo a criação de propostas, listagem, atualização de status e publicação de eventos no RabbitMQ.

2. **ContratacaoService**: Gerencia a contratação de seguros aprovados, incluindo a criação de contratos para propostas aprovadas, consulta de status de propostas via HTTP, consumo de eventos do RabbitMQ e implementação do Saga Pattern para transações distribuídas.

O sistema utiliza várias tecnologias, incluindo .NET 8, PostgreSQL 16, RabbitMQ 3.13, Entity Framework Core, MediatR, AutoMapper, FluentValidation, xUnit, Moq, FluentAssertions, Docker e Docker Compose.

## 📝 Mudanças recentes

Recentemente, o repositório viu algumas atualizações, incluindo a correção do nome do arquivo de diagrama. Esta pequena, mas importante correção garante que o arquivo do diagrama seja localizado e carregado corretamente. Além disso, vários arquivos foram adicionados no primeiro commit, estabelecendo a base do projeto.

## 🎯 Impacto

Esta implementação de microserviços é um excelente recurso para desenvolvedores que querem aprender mais sobre a criação de sistemas de microserviços com .NET. Este projeto demonstra como construir microserviços independentes que interagem entre si, utilizando práticas recomendadas e padrões de design modernos. Além disso, a recente correção do nome do arquivo de diagrama melhora a usabilidade do repositório, garantindo que os usuários possam acessar todos os recursos projetados para ajudá-los a entender a arquitetura do sistema.

Acompanhar as mudanças neste repositório pode fornecer insights valiosos para qualquer pessoa interessada em desenvolvimento de software, arquitetura de microserviços ou .NET.
