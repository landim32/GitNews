---
title: "Integrando Actions do Github e GitVersion para automação de versionamento"
date: 2026-03-06T13:11:06Z
author: "github-actions[bot]"
category: "DevOps"
tags:
  - "Github Actions"
  - "GitVersion"
  - "Automated Versioning"
slug: "integrando-actions-do-github-e-gitversion-para-automacao-de-versionamento"
---

# Integrando Actions do Github e GitVersion para automação de versionamento
No projeto VoxMeet, foi implementada uma solução interessante para a automação do versionamento de software utilizando Github Actions e a ferramenta GitVersion. Isso torna o controle de versões do software muito mais eficiente e reduz a possibilidade de erros humanos.

## Github Actions para versionamento
O Github Actions é utilizado para criar um pipeline de CI/CD que automatiza o versionamento. No commit analisado, foram adicionados dois workflows do Github Actions: `create-release.yml` e `version-tag.yml`. O primeiro é responsável por criar uma nova release no Github sempre que um novo versionamento é concluído com sucesso. O segundo, por sua vez, é responsável por criar um novo tag de versão sempre que ocorre um push na branch main ou quando o workflow é disparado manualmente.

diff
+name: Version and Tag
+
on:
+  push:
+    branches:
+      - main
+  workflow_dispatch:

+jobs:
+  create-version-tag:
+    runs-on: ubuntu-latest
+    permissions:
+      contents: write

+    outputs:
+      version: ${{ steps.gitversion.outputs.nuGetVersionV2 }}
+      semver: ${{ steps.gitversion.outputs.semVer }}



## GitVersion para versionamento semântico
O GitVersion é uma ferramenta que implementa o versionamento semântico (SemVer) para projetos. No projeto VoxMeet, foi adicionado um arquivo `GitVersion.yml` que configura a ferramenta para seguir o modelo de versionamento contínuo (Continuous Delivery). Isso significa que a versão do software é incrementada automaticamente a cada commit na branch main.

diff
+mode: ContinuousDelivery
+branches:
+  main:
+    increment: Patch
+    regex: ^master$|^main$
+    tag: ''
+    track-merge-target: false
+    source-branches: ['develop', 'release']
+    is-release-branch: true
+  develop:
+    increment: Minor
+    regex: ^dev(elop)?(ment)?$


Com essa abordagem, o versionamento do software se torna uma tarefa automática e precisa, deixando os desenvolvedores livres para focar em outros aspectos do projeto.
