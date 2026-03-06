---
title: "Gerenciamento de versão e configuração de CI/CD no projeto VoxMeet"
date: 2026-03-05T22:32:25Z
author: "github-actions[bot]"
category: "DevOps"
tags:
  - "CI/CD"
  - "GitVersion"
  - "GitHub Actions"
  - "Versionamento"
  - "Automatização"
slug: "gerenciamento-de-versao-e-configuracao-de-cicd-no-projeto-voxmeet"
---

Neste artigo, iremos explorar as recentes implementações técnicas feitas no projeto VoxMeet, mais especificamente, a adoção de práticas de DevOps para gerenciamento de versão e configuração de CI/CD.

## Gerenciamento de versão com GitVersion

Primeiramente, foi implementado o gerenciamento de versão utilizando a ferramenta GitVersion. O GitVersion é uma ferramenta de linha de comando que gera números de versão semântica para repositórios com base em tags, branches e histórico de commits.

yml
mode: ContinuousDelivery
branches:
  main:
    increment: Patch
    regex: ^master$|^main$
    tag: ''
    track-merge-target: false
    source-branches: ['develop', 'release']
    is-release-branch: true
...


Nesse arquivo de configuração do GitVersion, podemos ver que a versão é incrementada a cada novo commit na branch main. Isso facilita o controle de versões, uma vez que cada alteração no código se reflete em uma nova versão do software.

## Configuração de CI/CD com GitHub Actions

O projeto também implementou pipelines de CI/CD utilizando GitHub Actions. Foram implementadas duas pipelines principais: uma para versionamento e criação de tags, e outra para criação de releases.

A pipeline de versionamento e criação de tags é disparada a cada push na branch main ou quando a pipeline é manualmente disparada. Ela usa o GitVersion para gerar uma nova versão e cria uma nova tag no repositório com essa versão.

yml
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
      ...


Já a pipeline de criação de releases é disparada quando a pipeline de versionamento e criação de tags é concluída com sucesso. Essa pipeline cria uma nova release no GitHub com a versão gerada.

yml
name: Create Release

on:
  workflow_run:
    workflows: ['Version and Tag']
    types:
      - completed

jobs:
  create-release:
    runs-on: ubuntu-latest
    if: ${{ github.event.workflow_run.conclusion == 'success' }}
    permissions:
      contents: write
   ...


Essas implementações permitem a entrega contínua de novas versões do software, facilitando a distribuição e o uso pelos usuários finais.
